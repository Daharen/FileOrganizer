using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class DeterministicCollisionResolver : ICollisionResolver
{
    private const int MaxFileNameLength = 120;

    public string ResolveDestinationPath(
        string proposedDestinationPath,
        IEnumerable<string> reservedDestinationPaths,
        StringComparison pathComparison)
    {
        var directory = Path.GetDirectoryName(proposedDestinationPath)
            ?? throw new InvalidOperationException("Destination directory is missing.");

        if (!ContainsPath(reservedDestinationPaths, proposedDestinationPath, pathComparison))
        {
            return proposedDestinationPath;
        }

        var extension = Path.GetExtension(proposedDestinationPath);
        var baseName = Path.GetFileNameWithoutExtension(proposedDestinationPath);
        var counter = 1;

        while (true)
        {
            var candidateFileName = BuildCollisionSafeFileName(baseName, extension, counter, MaxFileNameLength);
            var candidatePath = Path.Combine(directory, candidateFileName);

            if (!ContainsPath(reservedDestinationPaths, candidatePath, pathComparison))
            {
                return candidatePath;
            }

            counter++;
        }
    }

    public static string BuildCollisionSafeFileName(
        string baseName,
        string extension,
        int counter,
        int maxFileNameLength)
    {
        var suffix = $" ({counter})";
        var safeExtension = extension ?? string.Empty;
        var maxBaseLength = Math.Max(1, maxFileNameLength - safeExtension.Length - suffix.Length);
        var truncatedBaseName = baseName.Length > maxBaseLength
            ? baseName[..maxBaseLength]
            : baseName;

        var normalizedBaseName = TrimInvalidTrailingCharacters(truncatedBaseName);
        if (string.IsNullOrWhiteSpace(normalizedBaseName))
        {
            normalizedBaseName = "_";
        }

        return $"{normalizedBaseName}{suffix}{safeExtension}";
    }

    private static string TrimInvalidTrailingCharacters(string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            return value;
        }

        return value.TrimEnd(' ', '.');
    }

    private static bool ContainsPath(
        IEnumerable<string> paths,
        string candidate,
        StringComparison comparison)
    {
        return paths.Any(path => string.Equals(path, candidate, comparison));
    }
}
