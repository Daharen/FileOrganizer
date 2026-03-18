using System.Text;

namespace FileOrganizer.Core.Extraction;

public sealed class FileTypeDetector : IFileTypeDetector
{
    private const double HighConfidence = 0.95;
    private const double MediumConfidence = 0.6;
    private const double LowConfidence = 0.1;

    private static readonly Dictionary<string, (string Mime, string Category)> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = ("text/plain", "TextDocument"),
        [".md"] = ("text/markdown", "TextDocument"),
        [".log"] = ("text/plain", "TextDocument"),
        [".json"] = ("application/json", "StructuredDocument"),
        [".csv"] = ("text/csv", "StructuredDocument"),
        [".xml"] = ("application/xml", "StructuredDocument"),
        [".yaml"] = ("application/yaml", "StructuredDocument"),
        [".yml"] = ("application/yaml", "StructuredDocument"),
        [".cs"] = ("text/plain", "CodeFile"),
        [".js"] = ("text/plain", "CodeFile"),
        [".ts"] = ("text/plain", "CodeFile"),
        [".py"] = ("text/plain", "CodeFile"),
        [".java"] = ("text/plain", "CodeFile"),
        [".cpp"] = ("text/plain", "CodeFile"),
        [".h"] = ("text/plain", "CodeFile"),
        [".hpp"] = ("text/plain", "CodeFile"),
        [".css"] = ("text/css", "CodeFile"),
        [".html"] = ("text/html", "CodeFile"),
        [".pdf"] = ("application/pdf", "StructuredDocument"),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "StructuredDocument"),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StructuredDocument"),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", "StructuredDocument"),
        [".jpg"] = ("image/jpeg", "Image"),
        [".jpeg"] = ("image/jpeg", "Image"),
        [".png"] = ("image/png", "Image"),
        [".mp3"] = ("audio/mpeg", "Audio"),
        [".mp4"] = ("video/mp4", "Video"),
        [".zip"] = ("application/zip", "Archive"),
        [".7z"] = ("application/x-7z-compressed", "Archive"),
        [".rar"] = ("application/vnd.rar", "Archive")
    };

    public DetectedFileType Detect(string path)
    {
        var extension = Path.GetExtension(path) ?? string.Empty;
        var header = ReadHeader(path, 16);

        if (TryDetectBySignature(header, extension, out var detected))
        {
            return detected;
        }

        if (ExtensionMap.TryGetValue(extension, out var mapped))
        {
            return new DetectedFileType
            {
                Extension = extension,
                DetectedMime = mapped.Mime,
                Category = mapped.Category,
                Confidence = MediumConfidence,
                SignatureMatched = false
            };
        }

        if (header.Length > 0 && TextFileHeuristics.LooksLikeText(header))
        {
            return new DetectedFileType
            {
                Extension = extension,
                DetectedMime = "text/plain",
                Category = TextFileHeuristics.DetermineTextCategoryFromExtension(extension),
                Confidence = MediumConfidence,
                SignatureMatched = false
            };
        }

        return new DetectedFileType
        {
            Extension = extension,
            DetectedMime = "application/octet-stream",
            Category = "Unknown",
            Confidence = LowConfidence,
            SignatureMatched = false
        };
    }

    private static byte[] ReadHeader(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[maxBytes];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        return bytesRead == buffer.Length ? buffer : buffer[..bytesRead];
    }

    private static bool TryDetectBySignature(ReadOnlySpan<byte> header, string extension, out DetectedFileType detected)
    {
        if (header.StartsWith("%PDF-"u8))
        {
            detected = CreateDetected(extension, "application/pdf", "StructuredDocument");
            return true;
        }

        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
        {
            var zipMime = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? ExtensionMap[".docx"].Mime
                : extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? ExtensionMap[".xlsx"].Mime
                    : extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
                        ? ExtensionMap[".pptx"].Mime
                        : "application/zip";
            var category = extension is ".docx" or ".xlsx" or ".pptx" ? "StructuredDocument" : "Archive";
            detected = CreateDetected(extension, zipMime, category);
            return true;
        }

        if (header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            detected = CreateDetected(extension, "image/png", "Image");
            return true;
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            detected = CreateDetected(extension, "image/jpeg", "Image");
            return true;
        }

        if (header.StartsWith("ID3"u8))
        {
            detected = CreateDetected(extension, "audio/mpeg", "Audio");
            return true;
        }

        if (header.Length >= 8 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
        {
            detected = CreateDetected(extension, "video/mp4", "Video");
            return true;
        }

        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            detected = new DetectedFileType
            {
                Extension = extension,
                DetectedMime = "audio/mpeg",
                Category = "Audio",
                Confidence = MediumConfidence,
                SignatureMatched = false
            };
            return true;
        }

        if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            detected = new DetectedFileType
            {
                Extension = extension,
                DetectedMime = "video/mp4",
                Category = "Video",
                Confidence = MediumConfidence,
                SignatureMatched = false
            };
            return true;
        }

        detected = null!;
        return false;
    }

    private static DetectedFileType CreateDetected(string extension, string mime, string category)
        => new()
        {
            Extension = extension,
            DetectedMime = mime,
            Category = category,
            Confidence = HighConfidence,
            SignatureMatched = true
        };
}
