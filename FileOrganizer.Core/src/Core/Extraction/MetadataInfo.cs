namespace FileOrganizer.Core.Extraction;

public sealed class MetadataInfo
{
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public Dictionary<string, string> Additional { get; init; } = new();
}
