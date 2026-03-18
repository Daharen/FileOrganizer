using System.Text;
using System.Text.RegularExpressions;

namespace FileOrganizer.Core.Extraction;

public sealed partial class PdfFileExtractor : IFileExtractor
{
    internal const int MaxReadBytes = 256 * 1024;
    private const int MaxPreviewCharacters = 4000;

    public bool CanHandle(string extension, string mime)
        => mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public ExtractionArtifact Extract(string path, DetectedFileType detectedType)
    {
        var fileInfo = new FileInfo(path);
        var bytes = ReadBounded(path, MaxReadBytes, out var wasTruncated);
        var ascii = Encoding.ASCII.GetString(bytes);
        var metadata = new MetadataInfo
        {
            CreatedAt = fileInfo.CreationTimeUtc,
            ModifiedAt = fileInfo.LastWriteTimeUtc
        };

        var pageCount = CountPages(ascii);
        if (pageCount > 0)
        {
            metadata.Additional["PageCount"] = pageCount.ToString();
        }

        var pdfVersion = TryGetPdfVersion(ascii);
        if (!string.IsNullOrWhiteSpace(pdfVersion))
        {
            metadata.Additional["PdfVersion"] = pdfVersion;
        }

        foreach (var pair in ExtractInfoMetadata(ascii))
        {
            metadata.Additional[pair.Key] = pair.Value;
        }

        var preview = ExtractReadablePreview(bytes);
        var hasStructure = pageCount > 0 || !string.IsNullOrWhiteSpace(pdfVersion) || metadata.Additional.Count > 0;
        var hasText = !string.IsNullOrWhiteSpace(preview);
        var sectionCount = hasText ? InferSectionCount(preview!) : Math.Max(pageCount, hasStructure ? 1 : 0);

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
            Content = new ContentSummary
            {
                TextPreview = hasText ? preview : null,
                LineCount = hasText ? CountLines(preview!) : 0,
                Encoding = hasText ? "pdf-text" : null
            },
            Structure = new StructuralFeatures
            {
                TokenCount = hasText ? CountTokens(preview!) : 0,
                SectionCount = sectionCount,
                PageCount = pageCount,
                HasHeaders = hasText && sectionCount > 0,
                HasTables = ascii.Contains("/Table", StringComparison.Ordinal),
                HasImages = ascii.Contains("/Image", StringComparison.Ordinal)
            },
            Status = new ExtractionStatus
            {
                Success = hasStructure || hasText,
                Partial = hasStructure && !hasText || wasTruncated,
                ErrorMessage = hasStructure || hasText ? null : "No readable PDF metadata or text could be extracted."
            }
        };
    }

    private static byte[] ReadBounded(string path, int maxBytes, out bool wasTruncated)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = stream.Length;
        var bytesToRead = (int)Math.Min(length, maxBytes);
        var buffer = new byte[bytesToRead];
        var offset = 0;
        while (offset < bytesToRead)
        {
            var read = stream.Read(buffer, offset, bytesToRead - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        wasTruncated = length > maxBytes;
        return offset == buffer.Length ? buffer : buffer[..offset];
    }

    private static int CountPages(string ascii)
        => Math.Max(0, Regex.Matches(ascii, @"/Type\s*/Page\b", RegexOptions.CultureInvariant).Count);

    private static string? TryGetPdfVersion(string ascii)
    {
        var match = Regex.Match(ascii, @"%PDF-(?<version>\d+\.\d+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["version"].Value : null;
    }

    private static Dictionary<string, string> ExtractInfoMetadata(string ascii)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in InfoFieldRegex().Matches(ascii))
        {
            var key = match.Groups["key"].Value;
            var value = CleanupPdfString(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }

    private static string? ExtractReadablePreview(byte[] bytes)
    {
        var latin1 = Encoding.Latin1.GetString(bytes);
        var matches = ReadableTextRegex().Matches(latin1)
            .Select(m => CleanupPdfString(m.Value))
            .Where(v => v.Length >= 4)
            .Distinct(StringComparer.Ordinal)
            .Take(40)
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        var preview = string.Join(Environment.NewLine, matches);
        return preview.Length <= MaxPreviewCharacters ? preview : preview[..MaxPreviewCharacters];
    }

    private static string CleanupPdfString(string value)
    {
        var cleaned = value
            .Replace("\\r", " ", StringComparison.Ordinal)
            .Replace("\\n", " ", StringComparison.Ordinal)
            .Replace("\\t", " ", StringComparison.Ordinal)
            .Replace("\\(", "(", StringComparison.Ordinal)
            .Replace("\\)", ")", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return cleaned;
    }

    private static int InferSectionCount(string text)
        => text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Length > 20 || line.EndsWith(':'));

    private static int CountLines(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.Split(["\r\n", "\n"], StringSplitOptions.None).Length;

    private static int CountTokens(string text)
        => Regex.Matches(text, @"\S+").Count;

    [GeneratedRegex(@"/(?<key>Title|Author|Subject|Creator|Producer|Keywords)\s*\((?<value>(?:\\.|[^)])*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex InfoFieldRegex();

    [GeneratedRegex(@"[A-Za-z0-9][A-Za-z0-9 	
\-_,.;:/'""!?()]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex ReadableTextRegex();
}
