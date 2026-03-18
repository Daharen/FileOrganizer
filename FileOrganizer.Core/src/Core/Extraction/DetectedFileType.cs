namespace FileOrganizer.Core.Extraction;

public sealed class DetectedFileType
{
    public string Extension { get; init; } = string.Empty;
    public string DetectedMime { get; init; } = "application/octet-stream";
    public string Category { get; init; } = "Unknown";
    public double Confidence { get; init; }
    public bool SignatureMatched { get; init; }
}
