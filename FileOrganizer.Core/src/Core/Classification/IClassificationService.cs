namespace FileOrganizer.Core.Classification;

public interface IClassificationService
{
    ClassificationResult Classify(string path);
}
