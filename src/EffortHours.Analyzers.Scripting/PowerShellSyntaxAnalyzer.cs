namespace EffortHours.Analyzers.Scripting;

internal static class PowerShellSyntaxAnalyzer
{
    private static readonly HashSet<string> ControlWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "begin", "break", "catch", "class", "continue", "data", "do", "dynamicparam",
        "else", "elseif", "end", "exit", "filter", "finally", "for", "foreach",
        "function", "if", "in", "param", "process", "return", "switch", "throw",
        "trap", "try", "until", "while", "workflow",
    };

    private static readonly HashSet<string> FileCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "Add-Content", "Clear-Content", "Copy-Item", "Get-ChildItem", "Get-Content",
        "Get-Item", "Join-Path", "Move-Item", "New-Item", "Remove-Item", "Rename-Item",
        "Resolve-Path", "Set-Content", "Set-Item", "Split-Path", "Test-Path",
    };

    private static readonly HashSet<string> NetworkCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connect-AzAccount", "Connect-MgGraph", "Invoke-RestMethod", "Invoke-WebRequest",
        "New-PSSession", "Receive-PSSession", "scp", "ssh", "curl", "wget", "az", "aws",
        "gcloud", "helm", "kubectl",
    };

    private static readonly HashSet<string> ProcessCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "Debug-Process", "Get-Process", "Invoke-Command", "Start-Job", "Start-Process",
        "Stop-Job", "Stop-Process", "Wait-Job", "Wait-Process",
    };

    public static ScriptSyntaxAnalysis Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ScriptTokenizationResult tokenization = PowerShellTokenizer.Tokenize(source);
        IReadOnlyList<ScriptToken> tokens = tokenization.Tokens;
        ScriptSourceMetrics metrics = new()
        {
            HasShebang = FirstLine(source).TrimStart('\uFEFF').StartsWith("#!", StringComparison.Ordinal),
            RequiresDirectives = CountRequiresDirectives(source),
        };
        HashSet<string> functions = FindFunctions(tokens);
        metrics.Functions = functions.Count;
        metrics.Types = tokens.Count(token =>
            token.Kind == ScriptTokenKind.Word &&
            token.Text.Equals("class", StringComparison.OrdinalIgnoreCase));
        metrics.PublicSymbols = functions.Count;
        HashSet<int> declarationWords = DeclarationWordIndices(tokens);
        int braceDepth = 0;
        bool commandStart = true;
        bool inParamBlock = false;
        int paramDepth = -1;

        for (int index = 0; index < tokens.Count; index++)
        {
            ScriptToken token = tokens[index];
            if (token.Kind == ScriptTokenKind.HereDocument)
            {
                metrics.HereDocuments++;
                continue;
            }

            if (token.Kind == ScriptTokenKind.Variable)
            {
                if (inParamBlock) metrics.Parameters++;
                if (IsCredentialName(token.Text)) metrics.CredentialCandidates++;
                bool assignmentTarget = commandStart && IsFollowedByAssignment(tokens, index + 1);
                if (commandStart && !assignmentTarget)
                {
                    metrics.HasDynamicInvocation = true;
                    metrics.DynamicExpansions++;
                }
                if (assignmentTarget) commandStart = false;
                continue;
            }

            if (token.Kind == ScriptTokenKind.NewLine ||
                token.Kind == ScriptTokenKind.Operator && token.Text == ";")
            {
                commandStart = true;
                continue;
            }

            if (token.Kind == ScriptTokenKind.Operator)
            {
                if (token.Text == "|")
                {
                    metrics.Pipelines++;
                    commandStart = true;
                }
                else if (token.Text is "&&" or "||")
                {
                    metrics.BranchPoints++;
                    if (token.Text == "||") metrics.ErrorHandlers++;
                    commandStart = true;
                }
                else if (token.Text == "&")
                {
                    metrics.ProcessOperations++;
                    metrics.HasDynamicInvocation = true;
                    commandStart = true;
                }
                else if (token.Text == "=")
                {
                    commandStart = true;
                }
                else if (token.Text == "{")
                {
                    braceDepth++;
                    commandStart = true;
                }
                else if (token.Text == "}")
                {
                    braceDepth = Math.Max(0, braceDepth - 1);
                    commandStart = true;
                }
                else if (token.Text == "(" && inParamBlock && paramDepth < 0)
                {
                    paramDepth = braceDepth;
                }
                else if (token.Text == ")" && inParamBlock)
                {
                    inParamBlock = false;
                    paramDepth = -1;
                }
                continue;
            }

            if (token.Kind != ScriptTokenKind.Word) continue;
            string word = token.Text;
            if (word.Equals("param", StringComparison.OrdinalIgnoreCase))
            {
                inParamBlock = true;
                commandStart = false;
                continue;
            }
            if (word.Equals("if", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("elseif", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("switch", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Conditionals++;
                metrics.BranchPoints++;
                commandStart = true;
                continue;
            }
            if (word.Equals("for", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("foreach", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("while", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("do", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Loops++;
                metrics.BranchPoints++;
                commandStart = true;
                continue;
            }
            if (word.Equals("try", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("catch", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("finally", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("trap", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("throw", StringComparison.OrdinalIgnoreCase))
                metrics.ErrorHandlers++;
            if (IsCredentialName(word) && word.Contains('=')) metrics.CredentialCandidates++;

            if (!commandStart || declarationWords.Contains(index) || ControlWords.Contains(word))
                continue;
            commandStart = false;
            if (braceDepth == 0) metrics.TopLevelCommands++;
            AnalyzeCommand(tokens, index, word, functions, metrics);
        }

        metrics.Methods = CountClassMethods(tokens);
        string confidence = tokenization.Complete && !tokenization.Truncated ? "medium" : "low";
        return new ScriptSyntaxAnalysis(confidence, "powershell", metrics);
    }

    private static void AnalyzeCommand(
        IReadOnlyList<ScriptToken> tokens,
        int index,
        string command,
        HashSet<string> functions,
        ScriptSourceMetrics metrics)
    {
        if (functions.Contains(command)) return;

        if (command.Equals(".", StringComparison.Ordinal) ||
            command.Equals("Import-Module", StringComparison.OrdinalIgnoreCase))
        {
            metrics.ModuleOperations++;
            metrics.SourcedFiles++;
            metrics.HasUnresolvedSourcing = true;
            ScriptToken? target = NextValue(tokens, index + 1);
            if (target is null || !TryStaticPath(target.Text, out _))
            {
                metrics.HasDynamicInvocation = true;
                metrics.DynamicExpansions++;
            }
        }
        if (command.Equals("Export-ModuleMember", StringComparison.OrdinalIgnoreCase))
        {
            metrics.ModuleOperations++;
            metrics.PublicSymbols = Math.Max(metrics.PublicSymbols, 1);
        }
        if (FileCommands.Contains(command)) metrics.FileOperations++;
        if (NetworkCommands.Contains(command))
        {
            metrics.NetworkOperations++;
            metrics.Technologies.Add(NetworkTechnology(command));
        }
        if (ProcessCommands.Contains(command))
        {
            metrics.ProcessOperations++;
            if (command.EndsWith("Job", StringComparison.OrdinalIgnoreCase)) metrics.AsyncUnits++;
        }
        if (command.Equals("Invoke-Expression", StringComparison.OrdinalIgnoreCase))
        {
            metrics.DynamicExpansions++;
            metrics.HasDynamicInvocation = true;
        }
        if (command.Equals("Set-StrictMode", StringComparison.OrdinalIgnoreCase) ||
            HasNearbyOption(tokens, index, "-ErrorAction")) metrics.ErrorHandlers++;
        if (command.Equals("ConvertTo-SecureString", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Get-Credential", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Get-Secret", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Set-Secret", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Read-Host", StringComparison.OrdinalIgnoreCase) &&
                HasNearbyOption(tokens, index, "-AsSecureString")) metrics.CredentialCandidates++;
        if (command.Equals("Describe", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Context", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("It", StringComparison.OrdinalIgnoreCase)) metrics.TestCases++;
        if (command.Equals("Should", StringComparison.OrdinalIgnoreCase)) metrics.Assertions++;
        if (command.Equals("Mock", StringComparison.OrdinalIgnoreCase)) metrics.MockUsages++;

        if (LooksLikeCmdlet(command)) metrics.CmdletCalls++;
        else if (!ControlWords.Contains(command) && IsLiteralCommand(command)) metrics.ExternalCommands++;
    }

    private static HashSet<string> FindFunctions(IReadOnlyList<ScriptToken> tokens)
    {
        HashSet<string> functions = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != ScriptTokenKind.Word ||
                !tokens[index].Text.Equals("function", StringComparison.OrdinalIgnoreCase) &&
                !tokens[index].Text.Equals("filter", StringComparison.OrdinalIgnoreCase) &&
                !tokens[index].Text.Equals("workflow", StringComparison.OrdinalIgnoreCase)) continue;
            (string Name, int First, int Last)? name = FunctionName(tokens, index + 1);
            if (name is not null) functions.Add(name.Value.Name);
        }
        return functions;
    }

    private static HashSet<int> DeclarationWordIndices(IReadOnlyList<ScriptToken> tokens)
    {
        HashSet<int> indices = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != ScriptTokenKind.Word ||
                !tokens[index].Text.Equals("function", StringComparison.OrdinalIgnoreCase) &&
                !tokens[index].Text.Equals("filter", StringComparison.OrdinalIgnoreCase) &&
                !tokens[index].Text.Equals("workflow", StringComparison.OrdinalIgnoreCase)) continue;
            (string Name, int First, int Last)? name = FunctionName(tokens, index + 1);
            if (name is null) continue;
            indices.Add(name.Value.First);
            indices.Add(name.Value.Last);
        }
        return indices;
    }

    private static (string Name, int First, int Last)? FunctionName(
        IReadOnlyList<ScriptToken> tokens,
        int start)
    {
        int first = NextValueIndex(tokens, start);
        if (first < 0) return null;
        string name = tokens[first].Text;
        int colon = NextSignificantIndex(tokens, first + 1);
        if (IsFunctionScope(name) && colon >= 0 && tokens[colon].Text == ":")
        {
            int scoped = NextValueIndex(tokens, colon + 1);
            if (scoped >= 0)
                return (NormalizeFunctionName(tokens[scoped].Text), first, scoped);
        }
        return (NormalizeFunctionName(name), first, first);
    }

    private static int CountClassMethods(IReadOnlyList<ScriptToken> tokens)
    {
        int methods = 0;
        bool inClass = false;
        int classDepth = -1;
        int depth = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == ScriptTokenKind.Word &&
                tokens[index].Text.Equals("class", StringComparison.OrdinalIgnoreCase)) inClass = true;
            if (tokens[index].Text == "{")
            {
                depth++;
                if (inClass && classDepth < 0) classDepth = depth;
            }
            else if (tokens[index].Text == "}")
            {
                if (classDepth == depth) { inClass = false; classDepth = -1; }
                depth = Math.Max(0, depth - 1);
            }
            else if (inClass && depth == classDepth && tokens[index].Kind == ScriptTokenKind.Word &&
                index + 1 < tokens.Count && tokens[index + 1].Text == "(" &&
                !ControlWords.Contains(tokens[index].Text)) methods++;
        }
        return methods;
    }

    private static ScriptToken? NextValue(IReadOnlyList<ScriptToken> tokens, int start) =>
        tokens.Skip(start).FirstOrDefault(token =>
            token.Kind is ScriptTokenKind.Word or ScriptTokenKind.String or ScriptTokenKind.Variable);

    private static int NextValueIndex(IReadOnlyList<ScriptToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == ScriptTokenKind.NewLine) return -1;
            if (tokens[index].Kind is ScriptTokenKind.Word or ScriptTokenKind.String)
                return index;
        }
        return -1;
    }

    private static int NextSignificantIndex(IReadOnlyList<ScriptToken> tokens, int start)
    {
        return start < tokens.Count && tokens[start].Kind != ScriptTokenKind.NewLine
            ? start
            : -1;
    }

    private static bool IsFollowedByAssignment(IReadOnlyList<ScriptToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == ScriptTokenKind.NewLine) return false;
            if (tokens[index].Kind == ScriptTokenKind.Operator) return tokens[index].Text == "=";
            if (tokens[index].Kind is ScriptTokenKind.Word or ScriptTokenKind.String or ScriptTokenKind.Variable)
                return false;
        }
        return false;
    }

    private static bool HasNearbyOption(
        IReadOnlyList<ScriptToken> tokens,
        int start,
        string value)
    {
        for (int index = start + 1; index < Math.Min(tokens.Count, start + 10); index++)
        {
            if (tokens[index].Kind == ScriptTokenKind.NewLine) break;
            if (tokens[index].Text.Equals(value, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool TryStaticPath(string token, out string? path)
    {
        path = token.Trim('\'', '"');
        if (path.Length == 0 || path.Contains('$') || path.Contains('*') || path.Contains('?'))
        {
            path = null;
            return false;
        }
        path = path.Replace('\\', '/').TrimStart('.', '/');
        return path.Length > 0 && !Path.IsPathRooted(path);
    }

    private static bool IsCredentialName(string value)
    {
        string token = value.Trim('$', '{', '}').ToLowerInvariant();
        return token.Contains("password", StringComparison.Ordinal) ||
            token.Contains("passwd", StringComparison.Ordinal) ||
            token.Contains("secret", StringComparison.Ordinal) ||
            token.Contains("credential", StringComparison.Ordinal) ||
            token.Contains("privatekey", StringComparison.Ordinal) ||
            token.Contains("apikey", StringComparison.Ordinal) ||
            token is "token" or "pat" ||
            token.EndsWith("_token", StringComparison.Ordinal) ||
            token.EndsWith("-token", StringComparison.Ordinal) ||
            token.Contains("accesstoken", StringComparison.Ordinal) ||
            token.Contains("apitoken", StringComparison.Ordinal) ||
            token.Contains("authtoken", StringComparison.Ordinal) ||
            token.Contains("bearertoken", StringComparison.Ordinal) ||
            token.Contains("refreshtoken", StringComparison.Ordinal);
    }

    private static string NormalizeFunctionName(string value)
    {
        string name = value.Trim('\'', '"');
        int separator = name.IndexOf(':');
        if (separator > 0)
        {
            string scope = name[..separator];
            if (scope.Equals("global", StringComparison.OrdinalIgnoreCase) ||
                scope.Equals("local", StringComparison.OrdinalIgnoreCase) ||
                scope.Equals("private", StringComparison.OrdinalIgnoreCase) ||
                scope.Equals("script", StringComparison.OrdinalIgnoreCase))
                return name[(separator + 1)..];
        }
        return name;
    }

    private static bool IsFunctionScope(string value) =>
        value.Equals("global", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("local", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("private", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("script", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeCmdlet(string command)
    {
        int dash = command.IndexOf('-');
        return dash > 0 && dash < command.Length - 1 &&
            command[..dash].All(char.IsLetter) &&
            command[(dash + 1)..].All(character => char.IsLetterOrDigit(character));
    }

    private static bool IsLiteralCommand(string command) =>
        command.Length > 0 && !command.Contains('$') && !command.Contains('=');

    private static string NetworkTechnology(string command)
    {
        if (command.Equals("Invoke-RestMethod", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Invoke-WebRequest", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("curl", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("wget", StringComparison.OrdinalIgnoreCase)) return "http-client";
        if (command.Equals("ssh", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("scp", StringComparison.OrdinalIgnoreCase) ||
            command.EndsWith("PSSession", StringComparison.OrdinalIgnoreCase)) return "remote-shell";
        if (command.Equals("kubectl", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("helm", StringComparison.OrdinalIgnoreCase)) return "container-orchestrator";
        if (command.Equals("az", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("aws", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("gcloud", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("Connect-", StringComparison.OrdinalIgnoreCase)) return "cloud-cli";
        return "network-tool";
    }

    private static int CountRequiresDirectives(string source) => source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n')
        .Count(line => line.TrimStart().StartsWith("#requires", StringComparison.OrdinalIgnoreCase));

    private static string FirstLine(string source)
    {
        int newline = source.IndexOfAny(['\r', '\n']);
        return newline < 0 ? source : source[..newline];
    }
}
