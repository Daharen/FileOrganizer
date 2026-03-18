namespace FileOrganizer.Core.Extraction;

public sealed class ExtractionArtifact
{
    public required FileIdentity Identity { get; init; }
    public required FileTypeInfo FileType { get; init; }
    public required MetadataInfo Metadata { get; init; }
    public required ContentSummary Content { get; init; }
    public required StructuralFeatures Structure { get; init; }
    public required ExtractionStatus Status { get; init; }
}
