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

    public ExtractionArtifact Dispatch(string path, string extension, string mime)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanHandle(extension, mime));

        if (extractor == null)
        {
            return CreateFallback(path, extension, mime);
        }

        try
        {
            return extractor.Extract(path);
        }
        catch (Exception ex)
        {
            return CreateFailure(path, extension, mime, ex.Message);
        }
    }

    private static ExtractionArtifact CreateFallback(string path, string ext, string mime)
    {
        return new ExtractionArtifact
        {
            Identity = new FileIdentity { Path = path, Size = new FileInfo(path).Length },
            FileType = new FileTypeInfo { Extension = ext, DetectedMime = mime, Confidence = 0.0 },
            Metadata = new MetadataInfo(),
            Content = new ContentSummary(),
            Structure = new StructuralFeatures(),
            Status = new ExtractionStatus { Success = false, Partial = true }
        };
    }

    private static ExtractionArtifact CreateFailure(string path, string ext, string mime, string error)
    {
        return new ExtractionArtifact
        {
            Identity = new FileIdentity { Path = path, Size = new FileInfo(path).Length },
            FileType = new FileTypeInfo { Extension = ext, DetectedMime = mime, Confidence = 0.0 },
            Metadata = new MetadataInfo(),
            Content = new ContentSummary(),
            Structure = new StructuralFeatures(),
            Status = new ExtractionStatus { Success = false, Partial = false, ErrorMessage = error }
        };
    }
}
