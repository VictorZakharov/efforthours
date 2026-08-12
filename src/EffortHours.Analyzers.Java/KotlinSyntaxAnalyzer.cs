namespace EffortHours.Analyzers.Java;

internal static class KotlinSyntaxAnalyzer
{
    public static KotlinSyntaxAnalysis Analyze(string source, string path)
    {
        KotlinTokenization tokenization = KotlinTokenizer.Tokenize(source);
        IReadOnlyList<KotlinToken> tokens = tokenization.Tokens;
        KotlinImportContext imports = KotlinImportAnalysis.Read(tokens);
        KotlinSourceMetrics metrics = new()
        {
            Imports = imports.ImportsSeen.Count,
            IsScript = Path.GetExtension(path).Equals(".kts", StringComparison.OrdinalIgnoreCase),
        };
        metrics.Technologies.UnionWith(imports.Technologies);
        ReadPackage(tokens, metrics);
        IReadOnlyList<KotlinDeclarationRange> types = ReadTypesAndAnnotations(tokens, metrics);
        IReadOnlyList<KotlinFunctionDeclaration> functions = ReadFunctions(tokens, types);
        ApplyFunctions(functions, metrics);
        ReadPublicProperties(tokens, functions, types, metrics);
        ReadControlAndNullability(tokens, metrics);
        if (metrics.IsScript && tokens.Count > 1) metrics.EntryPoints++;
        KotlinSemanticAnalyzer.Analyze(tokens, imports, path, metrics);
        return new KotlinSyntaxAnalysis(tokenization.Confidence, imports, metrics);
    }

    private static void ReadPackage(IReadOnlyList<KotlinToken> tokens, KotlinSourceMetrics metrics)
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Text != "package") continue;
            metrics.PackageName = KotlinTokenUtilities.QualifiedName(tokens, index + 1);
            return;
        }
    }

    private static List<KotlinDeclarationRange> ReadTypesAndAnnotations(
        IReadOnlyList<KotlinToken> tokens,
        KotlinSourceMetrics metrics)
    {
        List<KotlinDeclarationRange> ranges = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "@") ReadAnnotation(tokens, index, metrics);
            if (tokens[index].Text is not ("class" or "interface" or "object" or "typealias")) continue;
            if (tokens[index].Text == "object" && index > 0 && tokens[index - 1].Text == ":") continue;
            int nameIndex = index + 1;
            bool unnamedCompanion = tokens[index].Text == "object" &&
                KotlinTokenUtilities.HasModifierBefore(tokens, index, "companion") &&
                nameIndex < tokens.Count && tokens[nameIndex].Text == "{";
            if (!unnamedCompanion &&
                (nameIndex >= tokens.Count || tokens[nameIndex].Kind != KotlinTokenKind.Identifier)) continue;
            metrics.Types++;
            if (tokens[index].Text == "interface") metrics.Interfaces++;
            else if (tokens[index].Text == "object") metrics.Objects++;
            else if (tokens[index].Text == "class")
            {
                metrics.Classes++;
                if (KotlinTokenUtilities.HasModifierBefore(tokens, index, "data")) metrics.DataClasses++;
                if (KotlinTokenUtilities.HasModifierBefore(tokens, index, "enum")) metrics.Enums++;
            }

            if (KotlinTokenUtilities.HasModifierBefore(tokens, index, "sealed")) metrics.SealedTypes++;

            if (!KotlinTokenUtilities.IsRestrictedVisibility(tokens, index)) metrics.PublicSymbols++;
            if (!unnamedCompanion && nameIndex + 1 < tokens.Count && tokens[nameIndex + 1].Text == "<")
                metrics.GenericDeclarations++;
            int declarationEnd = unnamedCompanion ? index + 1 : nameIndex + 1;
            ReadBaseTypes(tokens, declarationEnd, metrics);
            int bodyStart = FindTypeBody(tokens, declarationEnd);
            if (bodyStart >= 0)
            {
                int bodyEnd = KotlinTokenUtilities.FindMatching(tokens, bodyStart, "{", "}");
                if (bodyEnd > bodyStart) ranges.Add(new KotlinDeclarationRange(bodyStart, bodyEnd));
            }
        }

        return ranges;
    }

    private static void ReadAnnotation(
        IReadOnlyList<KotlinToken> tokens,
        int index,
        KotlinSourceMetrics metrics)
    {
        int start = index + 1;
        if (start + 1 < tokens.Count && tokens[start + 1].Text == ":") start += 2;
        if (start >= tokens.Count || tokens[start].Kind != KotlinTokenKind.Identifier) return;
        string annotation = KotlinTokenUtilities.QualifiedName(tokens, start);
        if (annotation.Length == 0) return;
        metrics.Annotations++;
        metrics.AnnotationsSeen.Add(annotation);
    }

    private static void ReadBaseTypes(
        IReadOnlyList<KotlinToken> tokens,
        int start,
        KotlinSourceMetrics metrics)
    {
        int parentheses = 0;
        bool readingBases = false;
        for (int index = start; index < tokens.Count && index < start + 160; index++)
        {
            string text = tokens[index].Text;
            if (text == "(") parentheses++;
            else if (text == ")") parentheses = Math.Max(0, parentheses - 1);
            else if (text == ":" && parentheses == 0) readingBases = true;
            else if (parentheses == 0 && (text is "{" or ";" or "where")) break;
            else if (readingBases && tokens[index].Kind == KotlinTokenKind.Identifier)
            {
                string name = KotlinTokenUtilities.QualifiedName(tokens, index);
                if (name.Length > 0) metrics.BaseTypesSeen.Add(name);
                index += Math.Max(0, KotlinTokenUtilities.QualifiedNameLength(tokens, index) - 1);
            }
        }
    }

    private static int FindTypeBody(IReadOnlyList<KotlinToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count && index < start + 200; index++)
        {
            if (tokens[index].Text == "{") return index;
            if (tokens[index].Text is ";" or "=" ||
                index > start && tokens[index].Line > tokens[start].Line + 12) return -1;
        }

        return -1;
    }

    private static List<KotlinFunctionDeclaration> ReadFunctions(
        IReadOnlyList<KotlinToken> tokens,
        IReadOnlyList<KotlinDeclarationRange> types)
    {
        List<KotlinFunctionDeclaration> functions = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text != "fun" || index + 1 >= tokens.Count || tokens[index + 1].Text == "interface")
                continue;
            int open = FindFunctionOpen(tokens, index + 1);
            if (open < 0) continue;
            int nameIndex = FindFunctionName(tokens, index + 1, open);
            if (nameIndex < 0) continue;
            int close = KotlinTokenUtilities.FindMatching(tokens, open, "(", ")");
            if (close < 0) continue;
            (int bodyStart, int bodyEnd) = FindFunctionBody(tokens, close);
            bool method = types.Any(range => index > range.Start && index < range.End);
            bool extension = nameIndex > index + 1 && tokens[nameIndex - 1].Text == ".";
            bool suspend = KotlinTokenUtilities.HasModifierBefore(tokens, index, "suspend");
            bool generics = tokens.Skip(index + 1).Take(Math.Max(0, nameIndex - index - 1))
                .Any(token => token.Text == "<");
            functions.Add(new KotlinFunctionDeclaration(
                index,
                bodyStart,
                bodyEnd,
                tokens[nameIndex].Text,
                method,
                extension,
                suspend,
                generics,
                KotlinTokenUtilities.IsRestrictedVisibility(tokens, index)));
        }

        return functions;
    }

    private static int FindFunctionOpen(IReadOnlyList<KotlinToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count && index < start + 100; index++)
        {
            if (tokens[index].Text == "(") return index;
            if (tokens[index].Text is "{" or "}" or ";" || tokens[index].Line > tokens[start].Line + 5)
                return -1;
        }

        return -1;
    }

    private static int FindFunctionName(IReadOnlyList<KotlinToken> tokens, int start, int open)
    {
        for (int index = open - 1; index >= start; index--)
            if (tokens[index].Kind == KotlinTokenKind.Identifier) return index;
        return -1;
    }

    private static (int Start, int End) FindFunctionBody(IReadOnlyList<KotlinToken> tokens, int close)
    {
        for (int index = close + 1; index < tokens.Count && index < close + 100; index++)
        {
            if (tokens[index].Text == "{")
                return (index, KotlinTokenUtilities.FindMatching(tokens, index, "{", "}"));
            if (tokens[index].Text == "=")
            {
                int line = tokens[index].Line;
                int end = index + 1;
                while (end < tokens.Count && tokens[end].Line == line) end++;
                return (index, end);
            }
            if (tokens[index].Text == ";") return (-1, -1);
        }

        return (-1, -1);
    }

    private static void ApplyFunctions(
        IReadOnlyList<KotlinFunctionDeclaration> functions,
        KotlinSourceMetrics metrics)
    {
        foreach (KotlinFunctionDeclaration function in functions)
        {
            bool local = functions.Any(parent => parent.Index != function.Index &&
                parent.BodyStart >= 0 && function.Index > parent.BodyStart && function.Index < parent.BodyEnd);
            if (function.IsMethod) metrics.Methods++;
            else metrics.Functions++;
            if (function.IsExtension) metrics.ExtensionFunctions++;
            if (function.HasGenerics) metrics.GenericDeclarations++;
            if (function.IsSuspend)
            {
                metrics.SuspendFunctions++;
                metrics.AsyncUnits++;
            }
            if (!local && !function.IsRestricted) metrics.PublicSymbols++;
            if (!local && !function.IsMethod && function.Name == "main") metrics.EntryPoints++;
        }
    }

    private static void ReadPublicProperties(
        IReadOnlyList<KotlinToken> tokens,
        IReadOnlyList<KotlinFunctionDeclaration> functions,
        IReadOnlyList<KotlinDeclarationRange> types,
        KotlinSourceMetrics metrics)
    {
        foreach ((KotlinToken token, int index) in tokens.Select((token, index) => (token, index)))
        {
            if (token.Text is not ("val" or "var") || index + 1 >= tokens.Count ||
                tokens[index + 1].Kind != KotlinTokenKind.Identifier) continue;
            bool local = functions.Any(function => function.BodyStart >= 0 &&
                index > function.BodyStart && index < function.BodyEnd);
            bool memberOrTopLevel = !local || types.Any(range => index > range.Start && index < range.End);
            if (memberOrTopLevel && !KotlinTokenUtilities.IsRestrictedVisibility(tokens, index))
                metrics.PublicSymbols++;
        }
    }

    private static void ReadControlAndNullability(
        IReadOnlyList<KotlinToken> tokens,
        KotlinSourceMetrics metrics)
    {
        foreach (KotlinToken token in tokens)
        {
            if (token.Text is "if" or "when" or "for" or "while" or "catch" or "&&" or "||" or "?:")
                metrics.BranchPoints++;
            if (token.Text is "throw" or "try" or "catch") metrics.ExceptionPaths++;
            if (token.Text is "?" or "?." or "?:" or "!!" or "as?") metrics.NullabilityUsages++;
        }
    }
}
