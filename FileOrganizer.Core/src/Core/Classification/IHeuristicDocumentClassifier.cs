using FileOrganizer.Core.Extraction;

namespace FileOrganizer.Core.Classification;

public interface IHeuristicDocumentClassifier
{
    HeuristicClassificationSignal Classify(ExtractionArtifact artifact);
}
