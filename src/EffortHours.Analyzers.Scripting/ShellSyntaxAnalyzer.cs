namespace EffortHours.Analyzers.Scripting;

internal static class ShellSyntaxAnalyzer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "case", "do", "done", "elif", "else", "esac", "fi", "for", "function",
        "if", "in", "select", "then", "time", "until", "while",
    };

    private static readonly HashSet<string> Builtins = new(StringComparer.Ordinal)
    {
        ".", ":", "alias", "bg", "bind", "break", "builtin", "caller", "cd",
        "command", "compgen", "complete", "continue", "declare", "dirs", "disown",
        "echo", "enable", "eval", "exec", "exit", "export", "false", "fc", "fg",
        "getopts", "hash", "help", "history", "jobs", "kill", "let", "local",
        "logout", "mapfile", "popd", "printf", "pushd", "pwd", "read", "readarray",
        "readonly", "return", "set", "shift", "shopt", "source", "suspend", "test",
        "times", "trap", "true", "type", "typeset", "ulimit", "umask", "unalias",
        "unset", "wait",
    };

    private static readonly HashSet<string> FileCommands = new(StringComparer.Ordinal)
    {
        "cat", "chmod", "chown", "cmp", "cp", "cut", "diff", "find", "install",
        "ln", "mkdir", "mktemp", "mv", "readlink", "realpath", "rm", "rmdir", "sed",
        "stat", "tar", "tee", "touch", "unzip", "zip",
    };

    private static readonly HashSet<string> NetworkCommands = new(StringComparer.Ordinal)
    {
        "az", "aws", "curl", "dig", "gcloud", "helm", "host", "kubectl", "nc",
        "netcat", "nslookup", "rsync", "scp", "sftp", "ssh", "wget",
    };

    private static readonly HashSet<string> ProcessCommands = new(StringComparer.Ordinal)
    {
        "exec", "jobs", "kill", "killall", "nohup", "pgrep", "pkill", "ps", "timeout",
        "wait", "xargs",
    };

    public static ScriptSyntaxAnalysis Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ScriptTokenizationResult tokenization = ShellTokenizer.Tokenize(source);
        IReadOnlyList<ScriptToken> tokens = tokenization.Tokens;
        ScriptSourceMetrics metrics = new()
        {
            HasShebang = FirstLine(source).TrimStart('\uFEFF').StartsWith("#!", StringComparison.Ordinal),
        };
        HashSet<string> functions = FindFunctions(tokens);
        metrics.Functions = functions.Count;
        metrics.PublicSymbols = functions.Count(name => !name.StartsWith('_'));
        HashSet<int> functionDeclarationWords = FunctionDeclarationWordIndices(tokens);
        int braceDepth = 0;
        bool commandStart = true;

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
                if (IsParameter(token.Text)) metrics.Parameters++;
                if (IsCredentialName(token.Text)) metrics.CredentialCandidates++;
                if (token.Text.StartsWith("$(", StringComparison.Ordinal) ||
                    token.Text.StartsWith("${!", StringComparison.Ordinal))
                {
                    metrics.DynamicExpansions++;
                    metrics.HasDynamicInvocation = true;
                }
                if (commandStart) metrics.HasDynamicInvocation = true;
                continue;
            }

            if (token.Kind == ScriptTokenKind.NewLine ||
                token.Kind == ScriptTokenKind.Operator && token.Text is ";" or ";;" or ";&" or ";;&")
            {
                commandStart = true;
                continue;
            }

            if (token.Kind == ScriptTokenKind.Operator)
            {
                switch (token.Text)
                {
                    case "|":
                    case "|&":
                        metrics.Pipelines++;
                        commandStart = true;
                        break;
                    case "&&":
                    case "||":
                        metrics.BranchPoints++;
                        if (token.Text == "||") metrics.ErrorHandlers++;
                        commandStart = true;
                        break;
                    case "&":
                        metrics.AsyncUnits++;
                        commandStart = true;
                        break;
                    case "[[":
                        metrics.Assertions++;
                        commandStart = false;
                        break;
                    case "{":
                        braceDepth++;
                        commandStart = true;
                        break;
                    case "}":
                        braceDepth = Math.Max(0, braceDepth - 1);
                        commandStart = true;
                        break;
                }
                continue;
            }

            if (token.Kind != ScriptTokenKind.Word)
            {
                continue;
            }

            string word = NormalizeWord(token.Text);
            if (word.Length == 0) continue;
            if (IsCredentialName(word) && LooksLikeAssignment(token.Text))
                metrics.CredentialCandidates++;
            if (word is "if" or "elif" or "case")
            {
                metrics.Conditionals++;
                metrics.BranchPoints++;
                commandStart = true;
                continue;
            }
            if (word is "for" or "while" or "until" or "select")
            {
                metrics.Loops++;
                metrics.BranchPoints++;
                commandStart = true;
                continue;
            }
            if (word is "then" or "do" or "else")
            {
                commandStart = true;
                continue;
            }
            if (word is "eval")
            {
                metrics.DynamicExpansions++;
                metrics.HasDynamicInvocation = true;
            }

            if (!commandStart || functionDeclarationWords.Contains(index) || LooksLikeAssignment(token.Text))
            {
                continue;
            }

            commandStart = false;
            if (braceDepth == 0 && !Keywords.Contains(word)) metrics.TopLevelCommands++;
            AnalyzeCommand(tokens, index, word, functions, metrics);
        }

        metrics.BranchPoints += Math.Max(0, metrics.Conditionals + metrics.Loops - metrics.BranchPoints);
        string confidence = tokenization.Complete && !tokenization.Truncated ? "medium" : "low";
        return new ScriptSyntaxAnalysis(confidence, Dialect(source), metrics);
    }

    private static void AnalyzeCommand(
        IReadOnlyList<ScriptToken> tokens,
        int index,
        string command,
        HashSet<string> functions,
        ScriptSourceMetrics metrics)
    {
        if (functions.Contains(command)) return;

        if (command is "source" or ".")
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

        if (FileCommands.Contains(command)) metrics.FileOperations++;
        if (NetworkCommands.Contains(command))
        {
            metrics.NetworkOperations++;
            metrics.Technologies.Add(NetworkTechnology(command));
        }
        if (ProcessCommands.Contains(command)) metrics.ProcessOperations++;
        if (command == "set" && HasNearbyOption(tokens, index, "-e", "-u", "pipefail") ||
            command is "trap" or "return" or "exit") metrics.ErrorHandlers++;
        if (command == "read" && HasNearbyOption(tokens, index, "-s")) metrics.CredentialCandidates++;
        if (command == "@test" || command.StartsWith("assert", StringComparison.Ordinal))
        {
            if (command == "@test") metrics.TestCases++;
            else metrics.Assertions++;
        }
        if (command is "[" or "[[" or "test") metrics.Assertions++;

        if (!Keywords.Contains(command) && !Builtins.Contains(command))
        {
            metrics.ExternalCommands++;
        }
    }

    private static HashSet<string> FindFunctions(IReadOnlyList<ScriptToken> tokens)
    {
        HashSet<string> functions = new(StringComparer.Ordinal);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != ScriptTokenKind.Word) continue;
            string word = NormalizeWord(tokens[index].Text);
            if (word == "function" && NextWord(tokens, index + 1) is { } named)
            {
                functions.Add(NormalizeWord(named.Text));
                continue;
            }
            if (IsFunctionDeclaration(tokens, index)) functions.Add(word);
        }
        return functions;
    }

    private static HashSet<int> FunctionDeclarationWordIndices(IReadOnlyList<ScriptToken> tokens)
    {
        HashSet<int> indices = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != ScriptTokenKind.Word) continue;
            if (NormalizeWord(tokens[index].Text) == "function")
            {
                int named = NextWordIndex(tokens, index + 1);
                if (named >= 0) indices.Add(named);
            }
            else if (IsFunctionDeclaration(tokens, index)) indices.Add(index);
        }
        return indices;
    }

    private static ScriptToken? NextValue(IReadOnlyList<ScriptToken> tokens, int start) =>
        tokens.Skip(start).FirstOrDefault(token => token.Kind is ScriptTokenKind.Word or ScriptTokenKind.String);

    private static ScriptToken? NextWord(IReadOnlyList<ScriptToken> tokens, int start)
    {
        int index = NextWordIndex(tokens, start);
        return index < 0 ? null : tokens[index];
    }

    private static int NextWordIndex(IReadOnlyList<ScriptToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == ScriptTokenKind.Word) return index;
            if (tokens[index].Kind == ScriptTokenKind.NewLine) break;
        }
        return -1;
    }

    private static bool IsFunctionDeclaration(IReadOnlyList<ScriptToken> tokens, int index)
    {
        int open = NextStructuralIndex(tokens, index + 1);
        int close = NextStructuralIndex(tokens, open + 1);
        int body = NextStructuralIndex(tokens, close + 1);
        return open >= 0 && close >= 0 && body >= 0 &&
            tokens[open].Text == "(" && tokens[close].Text == ")" && tokens[body].Text == "{";
    }

    private static int NextStructuralIndex(IReadOnlyList<ScriptToken> tokens, int start)
    {
        for (int index = Math.Max(0, start); index < tokens.Count; index++)
            if (tokens[index].Kind != ScriptTokenKind.NewLine) return index;
        return -1;
    }

    private static bool HasNearbyOption(
        IReadOnlyList<ScriptToken> tokens,
        int start,
        params string[] values)
    {
        for (int index = start + 1; index < Math.Min(tokens.Count, start + 8); index++)
        {
            if (tokens[index].Kind == ScriptTokenKind.NewLine) break;
            string value = NormalizeWord(tokens[index].Text);
            if (values.Contains(value, StringComparer.Ordinal)) return true;
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

    private static bool IsParameter(string value)
    {
        string token = value.TrimStart('$', '{').TrimEnd('}');
        return token is "@" or "*" or "#" || token.Length == 1 && char.IsDigit(token[0]);
    }

    private static bool IsCredentialName(string value)
    {
        string token = value.Trim('$', '{', '}').ToLowerInvariant();
        return token.Contains("password", StringComparison.Ordinal) ||
            token.Contains("passwd", StringComparison.Ordinal) ||
            token.Contains("secret", StringComparison.Ordinal) ||
            token.Contains("credential", StringComparison.Ordinal) ||
            token.Contains("private_key", StringComparison.Ordinal) ||
            token.Contains("api_key", StringComparison.Ordinal) ||
            token.EndsWith("_token", StringComparison.Ordinal);
    }

    private static bool LooksLikeAssignment(string value)
    {
        int equals = value.IndexOf('=');
        return equals > 0 && value[..equals].All(character =>
            character == '_' || char.IsLetterOrDigit(character));
    }

    private static string NormalizeWord(string value) => value.Trim('\'', '"');

    private static string NetworkTechnology(string command) => command switch
    {
        "curl" or "wget" => "http-client",
        "ssh" or "scp" or "sftp" or "rsync" => "remote-shell",
        "kubectl" or "helm" => "container-orchestrator",
        "aws" or "az" or "gcloud" => "cloud-cli",
        _ => "network-tool",
    };

    private static string Dialect(string source)
    {
        string shebang = FirstLine(source).ToLowerInvariant();
        if (shebang.Contains("bash", StringComparison.Ordinal)) return "bash";
        if (shebang.Contains("ash", StringComparison.Ordinal) ||
            shebang.Contains("dash", StringComparison.Ordinal) ||
            shebang.Contains("/sh", StringComparison.Ordinal)) return "posix-shell";
        if (shebang.Contains("ksh", StringComparison.Ordinal)) return "ksh-compatible";
        return "shell-unspecified";
    }

    private static string FirstLine(string source)
    {
        int newline = source.IndexOfAny(['\r', '\n']);
        return newline < 0 ? source : source[..newline];
    }
}
