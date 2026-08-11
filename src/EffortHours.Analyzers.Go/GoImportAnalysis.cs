namespace EffortHours.Analyzers.Go;

internal static class GoImportAnalysis
{
    public static Dictionary<string, string> Read(
        IReadOnlyList<GoToken> tokens,
        GoSourceMetrics metrics)
    {
        Dictionary<string, string> aliases = new(StringComparer.Ordinal);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text != "import") continue;
            index++;
            if (index < tokens.Count && tokens[index].Text == "(")
            {
                int end = GoTokenUtilities.FindMatching(tokens, index, "(", ")");
                if (end < 0) end = tokens.Count;
                for (int item = index + 1; item < end; item++)
                {
                    if (tokens[item].Kind != GoTokenKind.String) continue;
                    string alias = AliasBefore(tokens, item, index + 1);
                    AddImport(GoTokenUtilities.StringValue(tokens[item].Text), alias, aliases, metrics);
                }

                index = end;
            }
            else if (index < tokens.Count)
            {
                string alias = string.Empty;
                if (tokens[index].Kind == GoTokenKind.Identifier ||
                    tokens[index].Text is "_" or ".")
                {
                    alias = tokens[index++].Text;
                }

                if (index < tokens.Count && tokens[index].Kind == GoTokenKind.String)
                    AddImport(GoTokenUtilities.StringValue(tokens[index].Text), alias, aliases, metrics);
            }
        }

        return aliases;
    }

    private static string AliasBefore(
        IReadOnlyList<GoToken> tokens,
        int stringIndex,
        int lowerBound)
    {
        if (stringIndex <= lowerBound) return string.Empty;
        GoToken previous = tokens[stringIndex - 1];
        return previous.Line == tokens[stringIndex].Line &&
            (previous.Kind == GoTokenKind.Identifier || previous.Text is "_" or ".")
                ? previous.Text
                : string.Empty;
    }

    private static void AddImport(
        string importPath,
        string explicitAlias,
        Dictionary<string, string> aliases,
        GoSourceMetrics metrics)
    {
        if (importPath.Length == 0) return;
        if (metrics.ImportsSeen.Add(importPath)) metrics.Imports++;
        string alias = explicitAlias.Length > 0 ? explicitAlias : DefaultAlias(importPath);
        if (alias == "_") metrics.BlankImports++;
        else if (alias != ".") aliases[alias] = importPath;
        string? technology = Technology(importPath);
        if (technology is not null) metrics.Technologies.Add(technology);
        if (importPath == "C") metrics.CgoFiles = 1;
    }

    private static string DefaultAlias(string importPath)
    {
        string[] segments = importPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string candidate = segments.LastOrDefault() ?? string.Empty;
        if (candidate.Length > 1 && candidate[0] == 'v' &&
            candidate.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0 &&
            segments.Length > 1) candidate = segments[^2];
        return candidate.Replace('-', '_');
    }

    public static string? Technology(string importPath)
    {
        string lower = importPath.ToLowerInvariant();
        if (lower == "net/http") return "net/http";
        if (lower == "database/sql") return "database/sql";
        if (lower == "flag") return "flag";
        if (lower == "sync" || lower.StartsWith("sync/", StringComparison.Ordinal)) return "sync";
        if (lower == "embed") return "embed";
        if (lower.StartsWith("crypto/", StringComparison.Ordinal)) return "crypto";
        if (IsModule(lower, "github.com/gin-gonic/gin")) return "gin";
        if (IsModule(lower, "github.com/labstack/echo")) return "echo";
        if (IsModule(lower, "github.com/go-chi/chi")) return "chi";
        if (IsModule(lower, "github.com/gorilla/mux")) return "gorilla/mux";
        if (IsModule(lower, "gorm.io/gorm")) return "gorm";
        if (IsModule(lower, "github.com/jmoiron/sqlx")) return "sqlx";
        if (IsModule(lower, "entgo.io/ent")) return "ent";
        if (IsModule(lower, "google.golang.org/grpc") ||
            IsModule(lower, "github.com/grpc/grpc-go")) return "grpc";
        if (IsModule(lower, "github.com/aws/aws-sdk-go") ||
            IsModule(lower, "github.com/aws/aws-sdk-go-v2")) return "aws";
        if (IsModule(lower, "cloud.google.com/go")) return "google-cloud";
        if (IsModule(lower, "github.com/azure/azure-sdk-for-go")) return "azure";
        if (IsModule(lower, "github.com/segmentio/kafka-go") ||
            IsModule(lower, "github.com/confluentinc/confluent-kafka-go")) return "kafka";
        if (IsModule(lower, "github.com/nats-io/nats.go")) return "nats";
        if (IsModule(lower, "github.com/golang-jwt/jwt")) return "jwt";
        if (IsModule(lower, "golang.org/x/oauth2")) return "oauth2";
        if (IsModule(lower, "golang.org/x/crypto")) return "x/crypto";
        if (IsModule(lower, "github.com/spf13/cobra")) return "cobra";
        if (IsModule(lower, "github.com/urfave/cli")) return "urfave/cli";
        if (IsModule(lower, "github.com/robfig/cron")) return "cron";
        if (IsModule(lower, "github.com/hibiken/asynq")) return "asynq";
        if (IsModule(lower, "go.temporal.io/sdk")) return "temporal";
        if (IsModule(lower, "github.com/go-playground/validator")) return "validator";
        if (IsModule(lower, "github.com/stretchr/testify")) return "testify";
        if (IsModule(lower, "github.com/google/go-cmp")) return "go-cmp";
        if (IsModule(lower, "github.com/golang/mock") ||
            IsModule(lower, "go.uber.org/mock")) return "gomock";
        if (IsModule(lower, "github.com/pressly/goose") ||
            IsModule(lower, "github.com/golang-migrate/migrate")) return "migration";
        return null;
    }

    private static bool IsModule(string importPath, string modulePath) =>
        importPath == modulePath || importPath.StartsWith(modulePath + "/", StringComparison.Ordinal);
}
