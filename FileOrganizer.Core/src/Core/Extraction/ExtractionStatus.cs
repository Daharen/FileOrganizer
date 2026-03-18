namespace FileOrganizer.Core.Extraction;

public sealed class ExtractionStatus
{
    public bool Success { get; init; }
    public bool Partial { get; init; }
    public string? ErrorMessage { get; init; }
}
