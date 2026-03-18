namespace FileOrganizer.Core.Extraction;

public sealed class StructuralFeatures
{
    public int TokenCount { get; init; }
    public int SectionCount { get; init; }
    public int PageCount { get; init; }
    public int EntryCount { get; init; }
    public bool HasHeaders { get; init; }
    public bool HasCodeBlocks { get; init; }
    public bool HasTables { get; init; }
    public bool HasImages { get; init; }
}
