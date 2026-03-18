namespace FileOrganizer.Core.Extraction;

public sealed class FileIdentity
{
    public required string Path { get; init; }
    public required long Size { get; init; }
    public string? Hash { get; init; }
}
