using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class DirectoryScanner
{
    private static readonly HashSet<string> ProtectedExecutableExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe",
            ".dll",
            ".sys",
            ".bat",
            ".cmd",
            ".msi"
        };

    public List<ScannedFile> ScanDirectory(string path)
    {
        var results = new List<ScannedFile>();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return results;
        }

        try
        {
            var root = new DirectoryInfo(path);
            ScanRecursive(root, root.FullName, results);

            return results
                .OrderBy(file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning directory: {ex.Message}");
            return new List<ScannedFile>();
        }
    }

    private static void ScanRecursive(DirectoryInfo directory, string rootPath, List<ScannedFile> results)
    {
        IEnumerable<DirectoryInfo> subdirectories = Array.Empty<DirectoryInfo>();
        IEnumerable<FileInfo> files = Array.Empty<FileInfo>();

        try
        {
            subdirectories = directory
                .EnumerateDirectories()
                .OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enumerate directories in {directory.FullName}: {ex.Message}");
        }

        try
        {
            files = directory
                .EnumerateFiles()
                .OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enumerate files in {directory.FullName}: {ex.Message}");
        }

        foreach (var file in files)
        {
            if (IsHidden(file.Attributes))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootPath, file.FullName);
            var topLevelFolder = GetTopLevelFolder(relativePath);

            results.Add(new ScannedFile
            {
                SourcePath = file.FullName,
                RelativePath = relativePath,
                SizeBytes = file.Length,
                LastModifiedUtc = file.LastWriteTimeUtc,
                IsHidden = false,
                IsProtectedExecutable = ProtectedExecutableExtensions.Contains(file.Extension),
                IsAlreadyInCategoryFolder = IsKnownCategoryFolder(topLevelFolder)
            });
        }

        foreach (var subdirectory in subdirectories)
        {
            if (IsHidden(subdirectory.Attributes))
            {
                continue;
            }

            ScanRecursive(subdirectory, rootPath, results);
        }
    }

    private static bool IsHidden(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
    }

    private static string GetTopLevelFolder(string relativePath)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var firstSegment = relativePath
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return firstSegment ?? string.Empty;
    }

    private static bool IsKnownCategoryFolder(string folderName)
    {
        return folderName.Equals("Images", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Documents", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Videos", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Audio", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Archives", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Code", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Data Files", StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Miscellaneous", StringComparison.OrdinalIgnoreCase);
    }
}
