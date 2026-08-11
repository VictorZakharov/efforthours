namespace EffortHours.Analyzers.Java;

internal static class JavaSyntaxAnalyzer
{
    private static readonly HashSet<string> PrimitiveOrReturnTypes = new(StringComparer.Ordinal)
    {
        "boolean", "byte", "char", "double", "float", "int", "long", "short", "void", "var",
    };

    public static JavaSyntaxAnalysis Analyze(string source, string path)
    {
        JavaTokenization tokenization = JavaTokenizer.Tokenize(source);
        IReadOnlyList<JavaToken> tokens = tokenization.Tokens;
        JavaImportContext imports = JavaImportAnalysis.Read(tokens);
        JavaSourceMetrics metrics = new()
        {
            Imports = imports.ImportsSeen.Count,
        };
        metrics.Technologies.UnionWith(imports.Technologies);
        bool moduleDescriptor = Path.GetFileName(path)
            .Equals("module-info.java", StringComparison.OrdinalIgnoreCase);
        ReadPackageAndModule(tokens, metrics, moduleDescriptor);
        HashSet<string> typeNames = ReadTypesAnnotationsAndBases(tokens, metrics);
        ReadMethods(tokens, typeNames, metrics);
        ReadControlAndModules(tokens, metrics, moduleDescriptor);
        JavaSemanticAnalyzer.Analyze(tokens, imports, path, metrics);
        return new JavaSyntaxAnalysis(tokenization.Confidence, imports, metrics);
    }

    private static void ReadPackageAndModule(
        IReadOnlyList<JavaToken> tokens,
        JavaSourceMetrics metrics,
        bool moduleDescriptor)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "package" && index + 1 < tokens.Count)
            {
                metrics.PackageName = JavaTokenUtilities.QualifiedName(tokens, index + 1);
                if (metrics.PackageName.Length > 0) metrics.DeclaredPackages.Add(metrics.PackageName);
            }
            else if (moduleDescriptor && tokens[index].Text == "module" && index + 1 < tokens.Count)
            {
                metrics.ModuleName = JavaTokenUtilities.QualifiedName(tokens, index + 1);
            }
        }
    }

    private static HashSet<string> ReadTypesAnnotationsAndBases(
        IReadOnlyList<JavaToken> tokens,
        JavaSourceMetrics metrics)
    {
        HashSet<string> typeNames = new(StringComparer.Ordinal);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "@" && index + 1 < tokens.Count)
            {
                string annotation = JavaTokenUtilities.QualifiedName(tokens, index + 1);
                if (annotation.Length > 0)
                {
                    metrics.Annotations++;
                    metrics.AnnotationsSeen.Add(annotation);
                }
            }

            if ((tokens[index].Text is "class" or "interface" or "record" or "enum") &&
                index + 1 < tokens.Count && tokens[index + 1].Kind == JavaTokenKind.Identifier)
            {
                string name = tokens[index + 1].Text;
                typeNames.Add(name);
                metrics.Types++;
                if (tokens[index].Text == "class") metrics.Classes++;
                else if (tokens[index].Text == "interface") metrics.Interfaces++;
                else if (tokens[index].Text == "record") metrics.Records++;
                else metrics.Enums++;
                if (JavaTokenUtilities.HasModifierBefore(tokens, index, "public")) metrics.PublicSymbols++;
                if (index + 2 < tokens.Count && tokens[index + 2].Text == "<") metrics.GenericDeclarations++;
            }

            if (tokens[index].Text is "extends" or "implements" or "permits")
            {
                for (int cursor = index + 1; cursor < tokens.Count &&
                    tokens[cursor].Text is not ("{" or ";"); cursor++)
                {
                    if (tokens[cursor].Kind != JavaTokenKind.Identifier) continue;
                    string name = JavaTokenUtilities.QualifiedName(tokens, cursor);
                    if (name.Length > 0) metrics.BaseTypesSeen.Add(name);
                    cursor += Math.Max(0, JavaTokenUtilities.QualifiedNameLength(tokens, cursor) - 1);
                }
            }
        }

        return typeNames;
    }

    private static void ReadMethods(
        IReadOnlyList<JavaToken> tokens,
        HashSet<string> typeNames,
        JavaSourceMetrics metrics)
    {
        for (int open = 1; open < tokens.Count; open++)
        {
            if (tokens[open].Text != "(" || tokens[open - 1].Kind != JavaTokenKind.Identifier) continue;
            int nameIndex = open - 1;
            string name = tokens[nameIndex].Text;
            if (!HasDeclarationPrefix(tokens, nameIndex, name, typeNames)) continue;
            int close = JavaTokenUtilities.FindMatching(tokens, open, "(", ")");
            if (close < 0 || !HasDeclarationSuffix(tokens, close)) continue;
            bool constructor = typeNames.Contains(name);
            metrics.Methods++;
            if (constructor) metrics.Constructors++;
            if (JavaTokenUtilities.HasModifierBefore(tokens, nameIndex, "public")) metrics.PublicSymbols++;
            if (HasGenericPrefix(tokens, nameIndex)) metrics.GenericDeclarations++;
            if (name == "main" && JavaTokenUtilities.HasModifierBefore(tokens, nameIndex, "static"))
                metrics.EntryPoints++;
            open = close;
        }
    }

    private static bool HasDeclarationPrefix(
        IReadOnlyList<JavaToken> tokens,
        int nameIndex,
        string name,
        HashSet<string> typeNames)
    {
        if (nameIndex == 0) return false;
        string previous = tokens[nameIndex - 1].Text;
        if (previous is "." or "::" or "@" or "new" or "return" or "throw" or
            "yield" or "case" or "this" or "super" ||
            previous is "class" or "interface" or "record" or "enum") return false;
        if (typeNames.Contains(name))
            return previous is "public" or "protected" or "private" ||
                tokens[nameIndex - 1].Kind == JavaTokenKind.Identifier;
        return tokens[nameIndex - 1].Kind == JavaTokenKind.Identifier ||
            PrimitiveOrReturnTypes.Contains(previous) || previous is "]" or ">" or "?";
    }

    private static bool HasDeclarationSuffix(IReadOnlyList<JavaToken> tokens, int close)
    {
        int cursor = close + 1;
        if (cursor < tokens.Count && tokens[cursor].Text == "throws")
        {
            cursor++;
            while (cursor < tokens.Count && tokens[cursor].Text is not ("{" or ";")) cursor++;
        }

        if (cursor < tokens.Count && tokens[cursor].Text == "default")
        {
            while (cursor < tokens.Count && tokens[cursor].Text != ";") cursor++;
        }

        return cursor < tokens.Count && tokens[cursor].Text is "{" or ";";
    }

    private static bool HasGenericPrefix(IReadOnlyList<JavaToken> tokens, int nameIndex)
    {
        for (int index = Math.Max(0, nameIndex - 12); index < nameIndex; index++)
        {
            if (tokens[index].Text is ";" or "{" or "}") continue;
            if (tokens[index].Text == "<") return true;
        }

        return false;
    }

    private static void ReadControlAndModules(
        IReadOnlyList<JavaToken> tokens,
        JavaSourceMetrics metrics,
        bool moduleDescriptor)
    {
        foreach (JavaToken token in tokens)
        {
            if (token.Text is "if" or "for" or "while" or "switch" or "case" or "catch" or
                "&&" or "||" or "?") metrics.BranchPoints++;
            if (token.Text is "throw" or "throws" or "catch") metrics.ExceptionPaths++;
            if (token.Text == "synchronized")
            {
                metrics.ConcurrencyUsages++;
                metrics.AsyncUnits++;
            }
            if (moduleDescriptor && token.Text == "requires") metrics.ModuleRequires++;
            if (moduleDescriptor && token.Text == "exports") metrics.ModuleExports++;
            if (moduleDescriptor && (token.Text is "uses" or "provides")) metrics.ModuleServices++;
        }
    }
}
