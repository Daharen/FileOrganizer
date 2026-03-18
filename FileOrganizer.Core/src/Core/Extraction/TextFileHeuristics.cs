using System.Text;

namespace FileOrganizer.Core.Extraction;

public static class TextFileHeuristics
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h", ".hpp", ".css", ".html"
    };

    public static bool LooksLikeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return true;
        }

        var controlCount = 0;
        foreach (var value in bytes)
        {
            if (value == 0)
            {
                return false;
            }

            if (value < 0x20 && value is not (byte)'\r' and not (byte)'\n' and not (byte)'\t' and not 0x0C)
            {
                controlCount++;
            }
        }

        return controlCount <= Math.Max(1, bytes.Length / 20);
    }

    public static string DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return "utf-8-bom";
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return "utf-16-le";
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return "utf-16-be";
            }
        }

        if (CanDecode(new UTF8Encoding(false, true), bytes))
        {
            return "utf-8";
        }

        if (CanDecode(Encoding.Unicode, bytes))
        {
            return "utf-16-le";
        }

        if (CanDecode(Encoding.BigEndianUnicode, bytes))
        {
            return "utf-16-be";
        }

        return "ascii";
    }

    public static string DetermineTextCategoryFromExtension(string extension)
        => CodeExtensions.Contains(extension) ? "CodeFile" : extension.Equals(".json", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? "StructuredDocument"
            : "TextDocument";

    public static Encoding GetEncoding(string encodingName)
        => encodingName switch
        {
            "utf-8-bom" => new UTF8Encoding(true, false),
            "utf-8" => new UTF8Encoding(false, false),
            "utf-16-le" => Encoding.Unicode,
            "utf-16-be" => Encoding.BigEndianUnicode,
            _ => Encoding.ASCII
        };

    private static bool CanDecode(Encoding encoding, byte[] bytes)
    {
        try
        {
            var clone = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            _ = clone.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
