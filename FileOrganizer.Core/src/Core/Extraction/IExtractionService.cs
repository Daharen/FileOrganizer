namespace FileOrganizer.Core.Extraction;

public interface IExtractionService
{
    ExtractionArtifact Extract(string path);
}
