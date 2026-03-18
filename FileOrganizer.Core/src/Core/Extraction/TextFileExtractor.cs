using System.Text;
using System.Text.RegularExpressions;

namespace FileOrganizer.Core.Extraction;

public sealed partial class TextFileExtractor : IFileExtractor
{
    internal const int MaxReadBytes = 256 * 1024;
    private const int MaxPreviewCharacters = 4000;

    private static readonly HashSet<string> KnownCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h", ".hpp", ".css", ".html"
    };

    public bool CanHandle(string extension, string mime)
        => mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
           || mime.Equals("application/json", StringComparison.OrdinalIgnoreCase)
           || mime.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
           || KnownCodeExtensions.Contains(extension);

    public ExtractionArtifact Extract(string path, DetectedFileType detectedType)
    {
        var fileInfo = new FileInfo(path);
        var buffer = ReadBounded(path, MaxReadBytes, out var wasTruncated);
        var encodingName = TextFileHeuristics.DetectEncoding(buffer);
        var text = Decode(buffer, encodingName);
        var preview = text.Length <= MaxPreviewCharacters ? text : text[..MaxPreviewCharacters];
        var sectionCount = CountSections(text);

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
            Metadata = new MetadataInfo
            {
                CreatedAt = fileInfo.CreationTimeUtc,
                ModifiedAt = fileInfo.LastWriteTimeUtc
            },
            Content = new ContentSummary
            {
                TextPreview = preview,
                LineCount = CountLines(text),
                Encoding = encodingName
            },
            Structure = new StructuralFeatures
            {
                TokenCount = CountTokens(text),
                SectionCount = sectionCount,
                HasHeaders = sectionCount > 0,
                HasCodeBlocks = HasCodePatterns(text)
            },
            Status = new ExtractionStatus
            {
                Success = true,
                Partial = wasTruncated
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

    private static string Decode(byte[] bytes, string encodingName)
    {
        var encoding = TextFileHeuristics.GetEncoding(encodingName);
        var clone = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        return clone.GetString(bytes);
    }

    private static int CountLines(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.Split(["\r\n", "\n"], StringSplitOptions.None).Length;

    private static int CountTokens(string text)
        => Regex.Matches(text, @"\S+").Count;

    private static int CountSections(string text)
    {
        var count = 0;
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal)
                || (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                || trimmed.StartsWith("<?xml", StringComparison.Ordinal)
                || trimmed.StartsWith("<", StringComparison.Ordinal)
                || trimmed is "{" or "[")
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasCodePatterns(string text)
    {
        if (text.Contains("```", StringComparison.Ordinal))
        {
            return true;
        }

        var braceLines = text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Count(line => line.Contains('{') || line.Contains('}') || line.Contains(';'));
        return braceLines >= 2 || CodeBlockRegex().IsMatch(text);
    }

    [GeneratedRegex(@"\{[\s\S]*\}", RegexOptions.Multiline)]
    private static partial Regex CodeBlockRegex();
}
