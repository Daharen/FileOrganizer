using System.Globalization;
using System.Text.RegularExpressions;

namespace FileOrganizer.Core.Renaming;

public static partial class DeterministicTextParsers
{
    private static readonly string[] MeaninglessTokens = ["unknown", "document", "file", "scan", "scanned", "copy", "final", "draft", "untitled", "notes"];
    private static readonly string[] StopWords = ["the", "and", "for", "with", "from", "this", "that", "meeting", "notes", "report", "invoice", "resume", "config", "data", "export"];
    private static readonly string[] VendorAnchors = ["invoice from", "vendor", "bill to", "from"];
    private static readonly string[] MeetingTerms = ["meeting", "agenda", "attendees", "action items", "minutes"];
    private static readonly string[] NameExclusions = ["resume", "cv", "curriculum vitae", "experience", "education", "skills", "summary", "references"];

    public static string? ExtractDateLikeToken(string originalFilename, string? textPreview)
    {
        foreach (var candidate in EnumerateDateCandidates(originalFilename, textPreview))
        {
            if (TryNormalizeDate(candidate, out var normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    public static string? ExtractSourceLikeName(string originalFilename, string? textPreview)
    {
        var filenameCandidate = ExtractSourceFromFilename(originalFilename);
        if (!string.IsNullOrWhiteSpace(filenameCandidate))
        {
            return filenameCandidate;
        }

        if (string.IsNullOrWhiteSpace(textPreview))
        {
            return null;
        }

        foreach (var line in GetLines(textPreview).Take(8))
        {
            var match = VendorLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var candidate = CleanLabel(match.Groups[1].Value);
            if (IsMeaningfulToken(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? ExtractResumeCandidateName(string originalFilename, string? textPreview)
    {
        var fromFileName = ExtractCandidateNameFromWords(TokenizeWords(Path.GetFileNameWithoutExtension(originalFilename)));
        if (!string.IsNullOrWhiteSpace(fromFileName))
        {
            return fromFileName;
        }

        if (string.IsNullOrWhiteSpace(textPreview))
        {
            return null;
        }

        foreach (var line in GetLines(textPreview).Take(4))
        {
            var candidate = CleanLabel(line);
            if (LooksLikeCandidateName(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? ExtractTopicLikeLabel(string originalFilename, string? textPreview)
    {
        if (!string.IsNullOrWhiteSpace(textPreview))
        {
            foreach (var line in GetLines(textPreview).Take(6))
            {
                var trimmed = line.Trim().TrimStart('#', '-', '*').Trim();
                if (trimmed.Length < 4 || trimmed.Length > 40)
                {
                    continue;
                }

                if (LooksLikeTopic(trimmed))
                {
                    return CleanLabel(trimmed);
                }
            }
        }

        var stem = Path.GetFileNameWithoutExtension(originalFilename);
        var words = TokenizeWords(stem).Where(IsMeaningfulToken).Take(4).ToArray();
        if (words.Length is >= 1 and <= 4)
        {
            var candidate = string.Join("_", words.Select(ToTitleCaseToken));
            return candidate.Length >= 4 ? candidate : null;
        }

        return null;
    }

    public static bool HasMeetingSignal(string originalFilename, string? textPreview)
    {
        var combined = string.Join('\n', new[] { originalFilename, textPreview ?? string.Empty });
        return MeetingTerms.Any(term => combined.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateDateCandidates(string originalFilename, string? textPreview)
    {
        foreach (Match match in NumericDateRegex().Matches(originalFilename))
        {
            yield return match.Value;
        }

        if (string.IsNullOrWhiteSpace(textPreview))
        {
            yield break;
        }

        foreach (Match match in NumericDateRegex().Matches(textPreview))
        {
            yield return match.Value;
        }

        foreach (Match match in MonthDateRegex().Matches(textPreview))
        {
            yield return match.Value;
        }
    }

    private static bool TryNormalizeDate(string candidate, out string normalized)
    {
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
            "MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy",
            "MMMM d, yyyy", "MMM d, yyyy", "MMMM d yyyy", "MMM d yyyy"
        };

        if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            normalized = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static string? ExtractSourceFromFilename(string originalFilename)
    {
        var stem = Path.GetFileNameWithoutExtension(originalFilename);
        foreach (var anchor in VendorAnchors)
        {
            var index = stem.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var tail = stem[(index + anchor.Length)..];
            var candidate = CleanLabel(tail);
            if (IsMeaningfulToken(candidate))
            {
                return candidate;
            }
        }

        var tokens = TokenizeWords(stem).Where(IsMeaningfulToken).Take(3).ToArray();
        if (tokens.Length == 1)
        {
            return ToTitleCaseToken(tokens[0]);
        }

        return null;
    }

    private static string? ExtractCandidateNameFromWords(IEnumerable<string> words)
    {
        var filtered = words.Where(word => !NameExclusions.Contains(word, StringComparer.OrdinalIgnoreCase)).Take(3).ToArray();
        if (filtered.Length is < 2 or > 3)
        {
            return null;
        }

        var candidate = string.Join("_", filtered.Select(ToTitleCaseToken));
        return LooksLikeCandidateName(candidate.Replace('_', ' ')) ? candidate : null;
    }

    private static bool LooksLikeCandidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var words = value.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 2 or > 3)
        {
            return false;
        }

        return words.All(word => word.Length >= 2
            && char.IsLetter(word[0])
            && word.All(ch => char.IsLetter(ch) || ch == '-' || ch == '\''))
            && words.All(word => !NameExclusions.Contains(word, StringComparer.OrdinalIgnoreCase));
    }

    private static bool LooksLikeTopic(string value)
    {
        var words = TokenizeWords(value).Where(IsMeaningfulToken).Take(5).ToArray();
        return words.Length is >= 1 and <= 5 && value.Any(char.IsLetter) && !LooksLikeCandidateName(value);
    }

    private static string CleanLabel(string value)
    {
        var normalized = FilenameNormalization.NormalizeSegment(value.Replace(':', ' ').Replace(',', ' '));
        var words = TokenizeWords(normalized).Where(IsMeaningfulToken).Take(4).Select(ToTitleCaseToken).ToArray();
        return words.Length == 0 ? string.Empty : string.Join("_", words);
    }

    private static IEnumerable<string> GetLines(string textPreview)
        => textPreview.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IEnumerable<string> TokenizeWords(string value)
        => WordRegex().Matches(value).Select(match => match.Value.ToLowerInvariant());

    private static string ToTitleCaseToken(string token)
        => string.IsNullOrWhiteSpace(token)
            ? string.Empty
            : char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();

    private static bool IsMeaningfulToken(string? token)
        => !string.IsNullOrWhiteSpace(token)
           && token.Length >= 2
           && !MeaninglessTokens.Contains(token, StringComparer.OrdinalIgnoreCase)
           && !StopWords.Contains(token, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(\d{4}[-/.]\d{2}[-/.]\d{2}|\d{1,2}/\d{1,2}/\d{2,4})\b")]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(@"\b(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec|January|February|March|April|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDateRegex();

    [GeneratedRegex(@"\b(?:invoice\s+from|vendor|bill\s+to|from)\s*[:\-]?\s*([A-Za-z][A-Za-z0-9&'\-. ]{1,40})", RegexOptions.IgnoreCase)]
    private static partial Regex VendorLineRegex();

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9']*")]
    private static partial Regex WordRegex();
}
