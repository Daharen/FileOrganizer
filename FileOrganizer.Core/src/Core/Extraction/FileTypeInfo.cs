namespace FileOrganizer.Core.Extraction;

public sealed class FileTypeInfo
{
    public required string Extension { get; init; }
    public required string DetectedMime { get; init; }
    public required double Confidence { get; init; }
}
