using System;
using System.IO;

namespace FileOrganizer.Core;

public sealed class ScannedFile
{
    public string SourcePath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string FileName => Path.GetFileName(SourcePath);

    public string Extension => Path.GetExtension(SourcePath).ToLowerInvariant();

    public long SizeBytes { get; init; }

    public DateTime LastModifiedUtc { get; init; }

    public bool IsHidden { get; init; }

    public bool IsProtectedExecutable { get; init; }

    public bool IsAlreadyInCategoryFolder { get; init; }
}
