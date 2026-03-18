using System.Text;
using System.Text.RegularExpressions;

namespace FileOrganizer.Core.Renaming;

public static partial class FilenameNormalization
{
    private const int DefaultMaxLength = 120;

    public static string NormalizeSuggestedFilename(string filename, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(filename);
        var stem = Path.GetFileNameWithoutExtension(filename);
        var normalizedStem = NormalizeSegment(stem);
        if (string.IsNullOrWhiteSpace(normalizedStem))
        {
            return string.Empty;
        }

        var normalizedExtension = NormalizeExtension(extension);
        var boundedStem = BoundStemLength(normalizedStem, normalizedExtension, maxLength);
        return string.Concat(boundedStem, normalizedExtension).TrimEnd(' ', '.');
    }

    public static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(IsInvalidFileNameChar(ch) ? '_' : ch);
        }

        var normalized = builder.ToString();
        normalized = WhitespaceRegex().Replace(normalized, " ");
        normalized = SeparatorRegex().Replace(normalized, "_");
        normalized = DuplicateUnderscoreRegex().Replace(normalized, "_");
        normalized = normalized.Replace(" _", "_", StringComparison.Ordinal)
            .Replace("_ ", "_", StringComparison.Ordinal);
        normalized = normalized.Trim('_', '-', '.', ' ');
        return normalized;
    }

    public static string CombineSegments(params string?[] segments)
    {
        var normalized = segments
            .Select(NormalizeSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment));

        return NormalizeSegment(string.Join("_", normalized));
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var sanitized = new string(extension.Trim().Where(ch => !IsInvalidFileNameChar(ch) && !char.IsWhiteSpace(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        return sanitized.StartsWith('.', StringComparison.Ordinal) ? sanitized : $".{sanitized}";
    }

    private static string BoundStemLength(string stem, string extension, int maxLength)
    {
        var allowedStemLength = Math.Max(1, maxLength - extension.Length);
        if (stem.Length <= allowedStemLength)
        {
            return stem;
        }

        return stem[..allowedStemLength].TrimEnd(' ', '_', '-', '.');
    }

    private static bool IsInvalidFileNameChar(char ch)
        => Path.GetInvalidFileNameChars().Contains(ch) || ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\s_-]+")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"_+")]
    private static partial Regex DuplicateUnderscoreRegex();
}
