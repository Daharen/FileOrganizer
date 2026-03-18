namespace FileOrganizer.Core.Extraction;

public interface IFileExtractor
{
    bool CanHandle(string extension, string mime);
    ExtractionArtifact Extract(string path);
}
