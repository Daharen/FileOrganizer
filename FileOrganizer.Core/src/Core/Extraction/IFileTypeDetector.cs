namespace FileOrganizer.Core.Extraction;

public interface IFileTypeDetector
{
    DetectedFileType Detect(string path);
}
