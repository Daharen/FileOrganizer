using FileOrganizer.Core.Classification;
using FileOrganizer.Core.Extraction;

namespace FileOrganizer.Core.Renaming;

public sealed class DeterministicFilenameSuggestionService : IFilenameSuggestionService
{
    private const double ClassificationConfidenceThreshold = 0.79;

    public FilenameSuggestion Suggest(string filePath, ClassificationResult classification, ExtractionArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(artifact);

        var originalFilename = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        var preview = artifact.Content.TextPreview;

        if (classification.ConfidenceScore < ClassificationConfidenceThreshold)
        {
            return Preserve(originalFilename, classification.ConfidenceScore, "Classification confidence is below rename threshold.");
        }

        return classification.SemanticCategory switch
        {
            "Invoice" => SuggestInvoice(originalFilename, extension, preview),
            "Resume" => SuggestResume(originalFilename, extension, preview),
            "Notes" => SuggestNotes(originalFilename, extension, preview),
            "Report" => SuggestReport(originalFilename, extension, preview),
            "DataExport" => SuggestDataExport(originalFilename, extension, preview),
            "Configuration" => SuggestConfiguration(originalFilename, extension, preview),
            _ => Preserve(originalFilename, classification.ConfidenceScore, "Semantic category is not enabled for deterministic rename suggestions.")
        };
    }

    private static FilenameSuggestion SuggestInvoice(string originalFilename, string extension, string? preview)
    {
        var source = DeterministicTextParsers.ExtractSourceLikeName(originalFilename, preview);
        var date = DeterministicTextParsers.ExtractDateLikeToken(originalFilename, preview);

        if (string.IsNullOrWhiteSpace(source))
        {
            return Preserve(originalFilename, 0.42, "Invoice source signal is too weak for deterministic renaming.");
        }

        var candidate = string.IsNullOrWhiteSpace(date)
            ? $"Invoice_{source}{extension}"
            : $"Invoice_{source}_{date}{extension}";

        return FinalizeSuggestion(originalFilename, candidate, string.IsNullOrWhiteSpace(date) ? 0.84 : 0.92, "InvoicePattern", string.IsNullOrWhiteSpace(date)
            ? "Derived invoice source from deterministic signals."
            : "Derived invoice source and date from deterministic signals.");
    }

    private static FilenameSuggestion SuggestResume(string originalFilename, string extension, string? preview)
    {
        var candidateName = DeterministicTextParsers.ExtractResumeCandidateName(originalFilename, preview);
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return Preserve(originalFilename, 0.40, "Resume candidate name is not strong enough to rename safely.");
        }

        return FinalizeSuggestion(originalFilename, $"Resume_{candidateName}{extension}", 0.91, "ResumePattern", "Derived resume candidate name from deterministic signals.");
    }

    private static FilenameSuggestion SuggestNotes(string originalFilename, string extension, string? preview)
    {
        var date = DeterministicTextParsers.ExtractDateLikeToken(originalFilename, preview);
        if (string.IsNullOrWhiteSpace(date))
        {
            return Preserve(originalFilename, 0.43, "Notes date signal is insufficient for renaming.");
        }

        var prefix = DeterministicTextParsers.HasMeetingSignal(originalFilename, preview) ? "MeetingNotes" : "Notes";
        return FinalizeSuggestion(originalFilename, $"{prefix}_{date}{extension}", 0.86, prefix == "MeetingNotes" ? "MeetingNotesPattern" : "NotesPattern", "Derived notes date from deterministic signals.");
    }

    private static FilenameSuggestion SuggestReport(string originalFilename, string extension, string? preview)
    {
        var topic = DeterministicTextParsers.ExtractTopicLikeLabel(originalFilename, preview);
        var date = DeterministicTextParsers.ExtractDateLikeToken(originalFilename, preview);
        var suffix = !string.IsNullOrWhiteSpace(topic) ? topic : date;
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return Preserve(originalFilename, 0.41, "Report topic/date signal is too ambiguous for renaming.");
        }

        return FinalizeSuggestion(originalFilename, $"Report_{suffix}{extension}", !string.IsNullOrWhiteSpace(topic) ? 0.85 : 0.82, "ReportPattern", "Derived report topic/date from deterministic signals.");
    }

    private static FilenameSuggestion SuggestDataExport(string originalFilename, string extension, string? preview)
    {
        var entity = DeterministicTextParsers.ExtractTopicLikeLabel(originalFilename, preview);
        var date = DeterministicTextParsers.ExtractDateLikeToken(originalFilename, preview);
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(date))
        {
            return Preserve(originalFilename, 0.39, "Data export entity/date signals are too weak for deterministic rename.");
        }

        return FinalizeSuggestion(originalFilename, $"DataExport_{entity}_{date}{extension}", 0.88, "DataExportPattern", "Derived data export entity and date from deterministic signals.");
    }

    private static FilenameSuggestion SuggestConfiguration(string originalFilename, string extension, string? preview)
    {
        var systemName = DeterministicTextParsers.ExtractTopicLikeLabel(originalFilename, preview);
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return Preserve(originalFilename, 0.38, "Configuration system name is too weak for deterministic rename.");
        }

        return FinalizeSuggestion(originalFilename, $"Config_{systemName}{extension}", 0.81, "ConfigPattern", "Derived configuration system name from deterministic signals.");
    }

    private static FilenameSuggestion FinalizeSuggestion(string originalFilename, string candidateFilename, double confidence, string strategy, string summary)
    {
        var normalized = FilenameNormalization.NormalizeSuggestedFilename(candidateFilename);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Preserve(originalFilename, confidence, "Generated filename became empty after normalization.");
        }

        if (IsSameSubstance(originalFilename, normalized))
        {
            return Preserve(originalFilename, confidence, "Generated filename does not materially improve the original filename.");
        }

        return new FilenameSuggestion
        {
            OriginalFilename = originalFilename,
            SuggestedFilename = normalized,
            ShouldRename = true,
            ConfidenceScore = confidence,
            ReasoningSummary = summary,
            Strategy = strategy
        };
    }

    private static FilenameSuggestion Preserve(string originalFilename, double confidence, string summary)
        => new()
        {
            OriginalFilename = originalFilename,
            SuggestedFilename = originalFilename,
            ShouldRename = false,
            ConfidenceScore = confidence,
            ReasoningSummary = summary,
            Strategy = "PreserveOriginal"
        };

    private static bool IsSameSubstance(string originalFilename, string normalizedCandidate)
    {
        var originalNormalized = FilenameNormalization.NormalizeSegment(Path.GetFileNameWithoutExtension(originalFilename));
        var candidateNormalized = FilenameNormalization.NormalizeSegment(Path.GetFileNameWithoutExtension(normalizedCandidate));
        return string.Equals(originalFilename, normalizedCandidate, StringComparison.OrdinalIgnoreCase)
            || string.Equals(originalNormalized, candidateNormalized, StringComparison.OrdinalIgnoreCase);
    }
}
