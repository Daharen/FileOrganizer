using System.IO;
using System.Linq;

namespace FileOrganizer.Core.Extraction;

public sealed class ExtractionDispatcher
{
    private readonly List<IFileExtractor> _extractors;

    public ExtractionDispatcher(IEnumerable<IFileExtractor> extractors)
    {
        _extractors = extractors.ToList();
    }

    public ExtractionArtifact Dispatch(string path, DetectedFileType detectedType)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanHandle(detectedType.Extension, detectedType.DetectedMime));

        if (extractor == null)
        {
            return CreateFallback(path, detectedType);
        }

        try
        {
            return extractor.Extract(path, detectedType);
        }
        catch (Exception ex)
        {
            return CreateFailure(path, detectedType, ex.Message);
        }
    }

    private static ExtractionArtifact CreateFallback(string path, DetectedFileType detectedType)
    {
        return new ExtractionArtifact
        {
            Identity = new FileIdentity { Path = path, Size = new FileInfo(path).Length },
            FileType = new FileTypeInfo
            {
                Extension = detectedType.Extension,
                DetectedMime = detectedType.DetectedMime,
                Category = detectedType.Category,
                Confidence = detectedType.Confidence
            },
            Metadata = new MetadataInfo(),
            Content = new ContentSummary(),
            Structure = new StructuralFeatures(),
            Status = new ExtractionStatus { Success = false, Partial = true }
        };
    }

    private static ExtractionArtifact CreateFailure(string path, DetectedFileType detectedType, string error)
    {
        return new ExtractionArtifact
        {
            Identity = new FileIdentity { Path = path, Size = new FileInfo(path).Length },
            FileType = new FileTypeInfo
            {
                Extension = detectedType.Extension,
                DetectedMime = detectedType.DetectedMime,
                Category = detectedType.Category,
                Confidence = detectedType.Confidence
            },
            Metadata = new MetadataInfo(),
            Content = new ContentSummary(),
            Structure = new StructuralFeatures(),
            Status = new ExtractionStatus { Success = false, Partial = false, ErrorMessage = error }
        };
    }
}
