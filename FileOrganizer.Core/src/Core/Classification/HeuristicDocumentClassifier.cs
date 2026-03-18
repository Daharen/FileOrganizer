using System.Text.RegularExpressions;
using FileOrganizer.Core.Extraction;

namespace FileOrganizer.Core.Classification;

public sealed partial class HeuristicDocumentClassifier : IHeuristicDocumentClassifier
{
    private static readonly string[] InvoiceTerms = ["invoice", "bill to", "amount due", "subtotal", "tax", "total", "invoice number"];
    private static readonly string[] ResumeTerms = ["experience", "education", "skills", "summary", "employment", "references"];
    private static readonly string[] NotesTerms = ["todo", "notes", "agenda", "action items", "meeting", "follow-up"];
    private static readonly string[] ReportTerms = ["executive summary", "introduction", "methodology", "results", "conclusion"];
    private static readonly string[] ConfigTerms = ["enabled", "host", "port", "timeout", "connectionstring", "connection_string"];
    private static readonly string[] DataExportFields = ["id", "created_at", "updated_at", "email", "status"];

    public HeuristicClassificationSignal Classify(ExtractionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var text = artifact.Content.TextPreview;
        if (string.IsNullOrWhiteSpace(text))
        {
            return NoMatch();
        }

        var normalized = text.Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var metadata = artifact.Metadata.Additional;

        var signals = new[]
        {
            EvaluateInvoice(normalized, lines),
            EvaluateResume(normalized),
            EvaluateNotes(normalized, lines, artifact),
            EvaluateReport(normalized, artifact),
            EvaluateConfiguration(normalized, lines, artifact, metadata),
            EvaluateDataExport(normalized, lines, artifact, metadata)
        };

        var best = signals.OrderByDescending(signal => signal.ConfidenceScore).FirstOrDefault();
        return best is not null && best.Matched ? best : NoMatch();
    }

    private static HeuristicClassificationSignal EvaluateInvoice(string text, string[] lines)
    {
        var keywordMatches = CountContains(text, InvoiceTerms);
        var lineItemSignal = lines.Count(line => line.Contains('$') || MoneyRegex().IsMatch(line)) >= 2;
        var matched = keywordMatches >= 4 || (keywordMatches >= 3 && lineItemSignal);
        return matched
            ? Match("Invoice", Math.Min(0.95, 0.80 + (keywordMatches * 0.03)), "Financial document signals detected.")
            : NoMatch();
    }

    private static HeuristicClassificationSignal EvaluateResume(string text)
    {
        var keywordMatches = CountContains(text, ResumeTerms);
        var matched = keywordMatches >= 4;
        return matched
            ? Match("Resume", Math.Min(0.92, 0.79 + (keywordMatches * 0.025)), "Career profile section signals detected.")
            : NoMatch();
    }

    private static HeuristicClassificationSignal EvaluateNotes(string text, string[] lines, ExtractionArtifact artifact)
    {
        var keywordMatches = CountContains(text, NotesTerms);
        var bulletHeavy = lines.Length > 0 && lines.Count(IsBulletLine) >= Math.Max(3, lines.Length / 3);
        var shortPlainText = artifact.Structure.TokenCount > 0 && artifact.Structure.TokenCount <= 350 && !artifact.Structure.HasCodeBlocks;
        var dateHeadings = lines.Count(line => DateHeadingRegex().IsMatch(line)) >= 1;
        var matched = keywordMatches >= 2 && (bulletHeavy || shortPlainText || dateHeadings);
        return matched
            ? Match("Notes", bulletHeavy ? 0.83 : 0.79, "Informal note-taking structure detected.")
            : NoMatch();
    }

    private static HeuristicClassificationSignal EvaluateReport(string text, ExtractionArtifact artifact)
    {
        var keywordMatches = CountContains(text, ReportTerms);
        var sectionSignal = artifact.Structure.SectionCount >= 3 || artifact.Structure.HasHeaders;
        var matched = keywordMatches >= 3 && sectionSignal;
        return matched
            ? Match("Report", Math.Min(0.9, 0.80 + (keywordMatches * 0.02)), "Formal report section signals detected.")
            : NoMatch();
    }

    private static HeuristicClassificationSignal EvaluateConfiguration(string text, string[] lines, ExtractionArtifact artifact, IReadOnlyDictionary<string, string> metadata)
    {
        var configKeywordMatches = CountContains(text, ConfigTerms);
        var iniSections = lines.Count(line => IniSectionRegex().IsMatch(line)) >= 1;
        var jsonRoot = text.TrimStart().StartsWith("{", StringComparison.Ordinal) || text.TrimStart().StartsWith("[", StringComparison.Ordinal);
        var yamlDensity = lines.Length >= 3 && lines.Count(line => YamlKeyValueRegex().IsMatch(line)) >= Math.Max(3, lines.Length / 2);
        var xmlRoot = XmlRootRegex().IsMatch(text);
        var structuredSignal = artifact.FileType.Category.Equals("CodeFile", StringComparison.OrdinalIgnoreCase)
            || artifact.FileType.Category.Equals("TextDocument", StringComparison.OrdinalIgnoreCase)
            || artifact.Structure.HasCodeBlocks
            || metadata.ContainsKey("ContainerSubtype");
        var matched = structuredSignal && ((configKeywordMatches >= 2 && (iniSections || yamlDensity || jsonRoot || xmlRoot)) || iniSections || (jsonRoot && configKeywordMatches >= 1) || yamlDensity || (xmlRoot && configKeywordMatches >= 1));
        return matched
            ? Match("Configuration", jsonRoot || xmlRoot ? 0.84 : 0.80, "Configuration-oriented structure detected.")
            : NoMatch();
    }

    private static HeuristicClassificationSignal EvaluateDataExport(string text, string[] lines, ExtractionArtifact artifact, IReadOnlyDictionary<string, string> metadata)
    {
        var header = lines.FirstOrDefault() ?? string.Empty;
        var commaDenseLines = lines.Count(line => line.Count(c => c == ',') >= 2 || line.Count(c => c == '\t') >= 2);
        var rowsSignal = lines.Length >= 4 && commaDenseLines >= Math.Max(3, lines.Length - 1);
        var repeatedFieldMatches = CountContains(header, DataExportFields);
        var tableSignal = artifact.Structure.HasTables || metadata.ContainsKey("HasSheets");
        var matched = rowsSignal && (repeatedFieldMatches >= 2 || tableSignal || header.Contains(',', StringComparison.Ordinal) || header.Contains('\t', StringComparison.Ordinal));
        return matched
            ? Match("DataExport", tableSignal ? 0.88 : 0.84, "Tabular export structure detected.")
            : NoMatch();
    }

    private static int CountContains(string text, IEnumerable<string> terms)
        => terms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsBulletLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("-", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal)
            || BulletNumberRegex().IsMatch(trimmed);
    }

    private static HeuristicClassificationSignal Match(string category, double confidence, string summary)
        => new()
        {
            SemanticCategory = category,
            ConfidenceScore = confidence,
            ReasoningSummary = summary,
            Matched = true
        };

    private static HeuristicClassificationSignal NoMatch()
        => new();

    [GeneratedRegex(@"\b\d+[,.]\d{2}\b")]
    private static partial Regex MoneyRegex();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}|[A-Z][a-z]{2,8}\s+\d{1,2},\s+\d{4}|\d{1,2}/\d{1,2}/\d{2,4})[:\-]?")]
    private static partial Regex DateHeadingRegex();

    [GeneratedRegex(@"^\d+[.)]\s+")]
    private static partial Regex BulletNumberRegex();

    [GeneratedRegex(@"^\[[^\]]+\]$")]
    private static partial Regex IniSectionRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+\s*:\s*.+$")]
    private static partial Regex YamlKeyValueRegex();

    [GeneratedRegex(@"^\s*<\?xml|^\s*<[A-Za-z][^>]+>", RegexOptions.Multiline)]
    private static partial Regex XmlRootRegex();
}
