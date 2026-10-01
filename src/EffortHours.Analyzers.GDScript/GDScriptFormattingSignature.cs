using System.Globalization;
using System.Text;

namespace EffortHours.Analyzers.GDScript;

/// <summary>Conservative static GDScript comparison; unsafe lexical structure fails closed.</summary>
public static class GDScriptFormattingSignature
{
    public static bool TryCreate(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        signature = string.Empty;
        if (source.Length > GDScriptTextReader.MaximumBytes) return false;
        GDScriptTokenization parsed = GDScriptTokenizer.Tokenize(source);
        if (parsed.Confidence == "low") return false;
        StringBuilder result = new();
        bool lineHasCode = false;
        foreach (GDScriptToken token in parsed.Tokens)
        {
            switch (token.Kind)
            {
                case GDScriptTokenKind.NewLine:
                    EndLine();
                    break;
                case GDScriptTokenKind.Indent:
                    EndLine();
                    result.Append("I;");
                    break;
                case GDScriptTokenKind.Dedent:
                    EndLine();
                    result.Append("D;");
                    break;
                case GDScriptTokenKind.End:
                    EndLine();
                    break;
                default:
                    result.Append(token.Text.Length.ToString(CultureInfo.InvariantCulture));
                    result.Append(':').Append(token.Text).Append(';');
                    lineHasCode = true;
                    break;
            }
        }
        signature = result.ToString();
        return true;

        void EndLine()
        {
            if (lineHasCode) result.Append("N;");
            lineHasCode = false;
        }
    }
}
