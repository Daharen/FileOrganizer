using FileOrganizer.Core.Classification;
using FileOrganizer.Core.Extraction;

namespace FileOrganizer.Core.Renaming;

public interface IFilenameSuggestionService
{
    FilenameSuggestion Suggest(
        string filePath,
        ClassificationResult classification,
        ExtractionArtifact artifact);
}
