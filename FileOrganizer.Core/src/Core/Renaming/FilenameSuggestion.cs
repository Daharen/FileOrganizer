namespace FileOrganizer.Core.Renaming;

public sealed class FilenameSuggestion
{
    public string OriginalFilename { get; init; } = string.Empty;
    public string SuggestedFilename { get; init; } = string.Empty;
    public bool ShouldRename { get; init; }
    public double ConfidenceScore { get; init; }
    public string ReasoningSummary { get; init; } = string.Empty;
    public string Strategy { get; init; } = "PreserveOriginal";
}
