using System;
using System.IO;

namespace FileOrganizer.Core;

public sealed class UndoCollisionResolver : IUndoCollisionResolver
{
    private const int MaxFileNameLength = 120;

    public string ResolvePreservedUndoPath(string restoreTargetPath, string currentPath)
    {
        var directory = Path.GetDirectoryName(restoreTargetPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Restore target directory is missing.");
        }

        var extension = Path.GetExtension(restoreTargetPath);
        var baseName = Path.GetFileNameWithoutExtension(restoreTargetPath);
        var firstCandidate = Path.Combine(directory, BuildFileName(baseName, extension, null));
        if (!File.Exists(firstCandidate) && !PathEquals(firstCandidate, currentPath))
        {
            return firstCandidate;
        }

        var counter = 1;
        while (true)
        {
            var candidate = Path.Combine(directory, BuildFileName(baseName, extension, counter));
            if (!File.Exists(candidate) && !PathEquals(candidate, currentPath))
            {
                return candidate;
            }

            counter++;
        }
    }

    private static string BuildFileName(string baseName, string extension, int? counter)
    {
        var suffix = counter is null ? ".undo-preserved" : $".undo-preserved ({counter.Value})";
        var safeExtension = extension ?? string.Empty;
        var maxBaseLength = Math.Max(1, MaxFileNameLength - safeExtension.Length - suffix.Length);
        var truncatedBase = baseName.Length > maxBaseLength ? baseName[..maxBaseLength] : baseName;
        if (OperatingSystem.IsWindows())
        {
            truncatedBase = truncatedBase.TrimEnd(' ', '.');
        }

        if (string.IsNullOrWhiteSpace(truncatedBase))
        {
            truncatedBase = "_";
        }

        return $"{truncatedBase}{suffix}{safeExtension}";
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparisonPolicy.PathComparison);
    }
}
