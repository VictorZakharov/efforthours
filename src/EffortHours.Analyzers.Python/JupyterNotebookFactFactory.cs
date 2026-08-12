using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal static class JupyterNotebookFactFactory
{
    public static EvidenceFact Notebook(JupyterNotebookAnalysis notebook) => PythonEvidence.Fact(
        $"python:notebook:{PythonEvidence.IdToken(notebook.File.Scope)}",
        EvidenceKinds.JupyterNotebook,
        notebook.Package.Directory,
        $"Bounded static Jupyter notebook inventory for '{notebook.File.Scope}'.",
        EvidenceSourceKind.Measured,
        "bounded JSON shape inspection with outputs and transient execution state excluded",
        [PythonEvidence.Location(notebook.File.Scope)],
        [
            PythonEvidence.Measurement("cells", notebook.TotalCells, "cells"),
            PythonEvidence.Measurement("code-cells", notebook.CodeCells, "cells"),
            PythonEvidence.Measurement("python-code-cells", notebook.PythonCodeCells, "cells"),
            PythonEvidence.Measurement("unique-code-cells", notebook.UniqueCodeCells, "cells"),
            PythonEvidence.Measurement("duplicate-code-cells", notebook.DuplicateCodeCells, "cells"),
            PythonEvidence.Measurement("markdown-cells", notebook.MarkdownCells, "cells"),
            PythonEvidence.Measurement("unique-markdown-cells", notebook.UniqueMarkdownCells, "cells"),
            PythonEvidence.Measurement("duplicate-markdown-cells", notebook.DuplicateMarkdownCells, "cells"),
            PythonEvidence.Measurement("raw-cells", notebook.RawCells, "cells"),
            PythonEvidence.Measurement("unsupported-code-cells", notebook.UnsupportedCodeCells, "cells"),
            PythonEvidence.Measurement("output-bearing-cells", notebook.OutputCells, "cells"),
            PythonEvidence.Measurement("execution-count-cells", notebook.ExecutionCountCells, "cells"),
            PythonEvidence.Measurement("attachment-cells", notebook.AttachmentCells, "cells"),
            PythonEvidence.Measurement("widget-state-containers", notebook.HasWidgetState ? 1 : 0, "containers"),
            PythonEvidence.Measurement("magic-lines", notebook.MagicLines, "lines"),
            PythonEvidence.Measurement("shell-escape-lines", notebook.ShellEscapeLines, "lines"),
        ],
        CommonTags(notebook));

    public static EvidenceFact SourceStructure(
        PythonPackageModel package,
        IReadOnlyList<JupyterNotebookAnalysis> notebooks)
    {
        PythonSourceMetrics[] metrics = [.. notebooks.Select(notebook => notebook.Syntax.Metrics)];
        return PythonEvidence.Fact(
            $"python:notebook-source:{PythonEvidence.IdToken(package.Directory)}",
            EvidenceKinds.SourceStructure,
            package.Directory,
            $"Projection-normalized Python code-cell structure for Jupyter notebooks in '{package.Name}'.",
            EvidenceSourceKind.Measured,
            "bounded notebook JSON projection followed by the managed Python tokenizer",
            notebooks.Select(notebook => PythonEvidence.Location(notebook.File.Scope)),
            [
                PythonEvidence.Measurement("files", notebooks.Count, "notebooks"),
                PythonEvidence.Measurement("functions", Sum(metrics, item => item.Functions), "symbols"),
                PythonEvidence.Measurement("methods", Sum(metrics, item => item.Methods), "symbols"),
                PythonEvidence.Measurement("types", Sum(metrics, item => item.Classes), "symbols"),
                PythonEvidence.Measurement("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                PythonEvidence.Measurement("async-units", Sum(metrics, item => item.AsyncUnits), "symbols"),
                PythonEvidence.Measurement("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                PythonEvidence.Measurement("notebook-code-cells", notebooks.Sum(item => item.UniqueCodeCells), "cells"),
            ],
            [
                "ecosystem:python",
                "format:jupyter-notebook",
                "syntax:token-backed",
                "structure:projection-normalized",
                "outputs:excluded",
                "source-excerpts:not-emitted",
                $"parser-confidence:{Confidence(notebooks)}",
            ]);
    }

    public static IEnumerable<EvidenceFact> Specialized(JupyterNotebookAnalysis notebook)
    {
        string token = PythonEvidence.IdToken(notebook.File.Scope);
        PythonSourceMetrics metrics = notebook.Syntax.Metrics;
        string[] tags = [.. CommonTags(notebook), .. metrics.Technologies.Order(StringComparer.Ordinal)
            .Select(technology => $"technology:{technology}")];
        if (notebook.MarkdownCells > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:notebook-documentation:{token}",
                EvidenceKinds.Documentation,
                notebook.Package.Directory,
                $"Maintained Markdown narrative in Jupyter notebook '{notebook.File.Scope}'.",
                EvidenceSourceKind.Measured,
                "bounded nonblank Markdown structure with attachments excluded",
                [PythonEvidence.Location(notebook.File.Scope)],
                [
                    PythonEvidence.Measurement("files", notebook.UniqueMarkdownCells, "cells"),
                    PythonEvidence.Measurement("physical-lines", notebook.MarkdownLines, "normalized-lines"),
                    PythonEvidence.Measurement("headings", notebook.MarkdownHeadings, "headings"),
                    PythonEvidence.Measurement("links", notebook.MarkdownLinks, "links"),
                ],
                tags);
        }

        if (metrics.VisualizationCalls > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:notebook-visualization:{token}",
                EvidenceKinds.UserInterface,
                notebook.Package.Directory,
                $"Import-qualified visualization surface in Jupyter notebook '{notebook.File.Scope}'.",
                EvidenceSourceKind.Inferred,
                "qualified visualization-library calls in admitted Python code cells",
                [PythonEvidence.Location(notebook.File.Scope)],
                [
                    PythonEvidence.Measurement("components", Math.Min(8, metrics.VisualizationCalls), "visualizations"),
                    PythonEvidence.Measurement("ui-types", 1, "notebook-surfaces"),
                ],
                tags);
        }

        if (metrics.DataAnalysisCalls > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:notebook-data:{token}",
                EvidenceKinds.DataAccess,
                notebook.Package.Directory,
                $"Import-qualified data-analysis surface in Jupyter notebook '{notebook.File.Scope}'.",
                EvidenceSourceKind.Inferred,
                "qualified data-analysis library calls in admitted Python code cells",
                [PythonEvidence.Location(notebook.File.Scope)],
                [PythonEvidence.Measurement("data-calls", Math.Min(16, metrics.DataAnalysisCalls), "calls")],
                tags);
        }
    }

    private static IEnumerable<string> CommonTags(JupyterNotebookAnalysis notebook)
    {
        yield return "ecosystem:python";
        yield return "format:jupyter-notebook";
        yield return "notebook-execution:not-performed";
        yield return "outputs:excluded";
        yield return "execution-counts:excluded";
        yield return "attachments:excluded";
        yield return "widget-state:excluded";
        yield return "source-excerpts:not-emitted";
        yield return $"declared-language:{notebook.DeclaredLanguage}";
        yield return $"parser-confidence:{notebook.Confidence}";
        if (notebook.UnsupportedCodeCells > 0) yield return "mixed-language:uncertain";
        if (notebook.MagicLines + notebook.ShellEscapeLines > 0) yield return "notebook-dynamic-syntax:excluded";
        if (notebook.SafeguardReached) yield return "analysis:bounded-incomplete";
        if (!notebook.IsCanonical) yield return "normalization:duplicate-maintained-projection";
    }

    private static int Sum(IEnumerable<PythonSourceMetrics> metrics, Func<PythonSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<JupyterNotebookAnalysis> notebooks) =>
        notebooks.Any(notebook => notebook.Confidence == "low") ? "low" : "medium";
}
