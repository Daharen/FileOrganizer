namespace FileOrganizer.Core.Extraction;

public sealed class ExtractionService : IExtractionService
{
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly ExtractionDispatcher _dispatcher;

    public ExtractionService(IFileTypeDetector fileTypeDetector, ExtractionDispatcher dispatcher)
    {
        _fileTypeDetector = fileTypeDetector;
        _dispatcher = dispatcher;
    }

    public ExtractionArtifact Extract(string path)
    {
        var detectedType = _fileTypeDetector.Detect(path);
        return _dispatcher.Dispatch(path, detectedType);
    }
}
