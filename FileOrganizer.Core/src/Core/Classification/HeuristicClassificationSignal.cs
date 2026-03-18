namespace FileOrganizer.Core.Classification;

public sealed class HeuristicClassificationSignal
{
    public string SemanticCategory { get; init; } = "Unclassified";
    public double ConfidenceScore { get; init; }
    public string ReasoningSummary { get; init; } = string.Empty;
    public bool Matched { get; init; }
}
