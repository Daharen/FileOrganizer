using System.IO.Compression;

namespace FileOrganizer.Core.Extraction;

public sealed class OpenXmlContainerExtractor : IFileExtractor
{
    private static readonly Dictionary<string, string> MimeToSubtype = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "DOCX",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "XLSX",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "PPTX"
    };

    private static readonly Dictionary<string, string> ExtensionToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public bool CanHandle(string extension, string mime)
        => MimeToSubtype.ContainsKey(mime) || ExtensionToMime.ContainsKey(extension);

    public ExtractionArtifact Extract(string path, DetectedFileType detectedType)
    {
        var fileInfo = new FileInfo(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var entryNames = archive.Entries.Select(entry => entry.FullName).ToArray();
        var entryCount = entryNames.Length;
        var documentPartCount = entryNames.Count(name => name.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
                                                || name.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                                                || name.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase));
        var hasSheets = entryNames.Any(name => name.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase));
        var hasSlides = entryNames.Any(name => name.StartsWith("ppt/slides/", StringComparison.OrdinalIgnoreCase));
        var hasImages = entryNames.Any(name => name.Contains("/media/", StringComparison.OrdinalIgnoreCase));
        var subtype = ResolveSubtype(detectedType, entryNames);

        var metadata = new MetadataInfo
        {
            CreatedAt = fileInfo.CreationTimeUtc,
            ModifiedAt = fileInfo.LastWriteTimeUtc,
            Additional =
            {
                ["ContainerSubtype"] = subtype,
                ["DocumentPartCount"] = documentPartCount.ToString(),
                ["EntryCount"] = entryCount.ToString(),
                ["HasSheets"] = hasSheets.ToString(),
                ["HasSlides"] = hasSlides.ToString()
            }
        };

        return new ExtractionArtifact
        {
            Identity = new FileIdentity { Path = path, Size = fileInfo.Length },
            FileType = new FileTypeInfo
            {
                Extension = detectedType.Extension,
                DetectedMime = detectedType.DetectedMime,
                Category = detectedType.Category,
                Confidence = detectedType.Confidence
            },
            Metadata = metadata,
            Content = new ContentSummary(),
            Structure = new StructuralFeatures
            {
                EntryCount = entryCount,
                SectionCount = Math.Max(documentPartCount, hasSheets || hasSlides ? 1 : 0),
                HasTables = hasSheets,
                HasImages = hasImages
            },
            Status = new ExtractionStatus
            {
                Success = entryCount > 0,
                Partial = true
            }
        };
    }

    private static string ResolveSubtype(DetectedFileType detectedType, IEnumerable<string> entryNames)
    {
        if (MimeToSubtype.TryGetValue(detectedType.DetectedMime, out var subtype))
        {
            return subtype;
        }

        if (entryNames.Any(name => name.StartsWith("word/", StringComparison.OrdinalIgnoreCase))) return "DOCX";
        if (entryNames.Any(name => name.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))) return "XLSX";
        if (entryNames.Any(name => name.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase))) return "PPTX";
        return "OpenXml";
    }
}
