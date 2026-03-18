namespace FileOrganizer.Core.Classification;

public sealed class ClassificationResult
{
    public string FilePath { get; init; } = string.Empty;
    public string DetectedType { get; init; } = "Unknown";
    public string SemanticCategory { get; init; } = "Unclassified";
    public string SuggestedFolder { get; init; } = string.Empty;
    public string? SuggestedFilename { get; init; }
    public double ConfidenceScore { get; init; }
    public string ReasoningSource { get; init; } = "Deterministic";
    public string AnalysisStage { get; init; } = "extension";
    public string ReasoningSummary { get; init; } = string.Empty;
}
