namespace FileOrganizer.Core.Extraction;

public sealed class ContentSummary
{
    public string? TextPreview { get; init; }
    public int LineCount { get; init; }
    public string? Encoding { get; init; }
}
