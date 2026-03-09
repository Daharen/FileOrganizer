using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class ValidatedOrganizationPlan
{
    public string AuthorizedRootPath { get; init; } = string.Empty;

    public List<ValidatedOperation> ApprovedOperations { get; init; } = new();

    public List<ValidationFailure> RejectedOperations { get; init; } = new();
}

public sealed class ValidatedOperation
{
    public string OperationId { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public string ProposedFileName { get; init; } = string.Empty;

    public double ConfidenceScore { get; init; }

    public string ReasoningSummary { get; init; } = string.Empty;

    public int StableOrderIndex { get; init; }
}

public sealed class ValidationFailure
{
    public string OperationId { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public string? DestinationPath { get; init; }

    public ValidationFailureCode Code { get; init; }

    public string Message { get; init; } = string.Empty;
}

public enum ValidationFailureCode
{
    SourceMissing,
    SourceProtected,
    InvalidDestination,
    DestinationOutsideAuthorizedRoot,
    InvalidFileName,
    CollisionDetected,
    RepositoryProtected,
    HiddenPathBlocked,
    ExecutableProtected,
    Unknown
}

public sealed class OperationPlanValidator
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

    private static readonly string[] RepositoryMarkers =
    {
        ".git",
        ".hg",
        ".svn",
        "package.json",
        "pyproject.toml",
        "*.sln",
        "*.csproj",
        "Cargo.toml",
        "go.mod"
    };

    public ValidatedOrganizationPlan Validate(string authorizedRootPath, OrganizationPlan plan)
    {
        var normalizedRoot = Path.GetFullPath(authorizedRootPath);
        var approved = new List<ValidatedOperation>();
        var rejected = new List<ValidationFailure>();
        var destinationsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var indexedOperation in plan.Operations.Select((operation, index) => (operation, index)))
        {
            var operation = indexedOperation.operation;
            var stableOrderIndex = indexedOperation.index;

            var sourcePath = SafeGetFullPath(operation.SourcePath);
            if (sourcePath is null)
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.SourceMissing, "Source path is invalid."));
                continue;
            }

            if (!File.Exists(sourcePath))
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.SourceMissing, "Source file does not exist."));
                continue;
            }

            if (!IsUnderRoot(normalizedRoot, sourcePath))
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.SourceProtected, "Source is outside the authorized root."));
                continue;
            }

            if (IsHiddenPath(sourcePath, normalizedRoot))
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.HiddenPathBlocked, "Hidden paths are blocked by default."));
                continue;
            }

            if (IsRepositoryProtected(sourcePath, normalizedRoot))
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.RepositoryProtected, "File is inside a detected repository boundary."));
                continue;
            }

            var normalizedFileName = NormalizeFileName(operation.ProposedFileName);
            if (string.IsNullOrWhiteSpace(normalizedFileName))
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.InvalidFileName, "Proposed file name is invalid after normalization."));
                continue;
            }

            var destinationDirectory = SafeGetFullPath(operation.DestinationDirectory);
            if (destinationDirectory is null)
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.InvalidDestination, "Destination directory is invalid."));
                continue;
            }

            var destinationPath = SafeGetFullPath(Path.Combine(destinationDirectory, normalizedFileName));
            if (destinationPath is null)
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.InvalidDestination, "Destination path could not be resolved."));
                continue;
            }

            if (!IsUnderRoot(normalizedRoot, destinationPath))
            {
                rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.DestinationOutsideAuthorizedRoot, "Destination is outside the authorized root."));
                continue;
            }

            if (IsHiddenPath(destinationPath, normalizedRoot))
            {
                rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.HiddenPathBlocked, "Hidden destination paths are blocked by default."));
                continue;
            }

            if (IsRepositoryProtected(destinationPath, normalizedRoot))
            {
                rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.RepositoryProtected, "Destination is inside a detected repository boundary."));
                continue;
            }

            var destinationExtension = Path.GetExtension(destinationPath);
            if (ProtectedExecutableExtensions.Contains(destinationExtension))
            {
                rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.ExecutableProtected, "Protected executable targets are blocked."));
                continue;
            }

            if (!destinationsSeen.Add(destinationPath) || File.Exists(destinationPath))
            {
                rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.CollisionDetected, "Destination collision detected."));
                continue;
            }

            approved.Add(new ValidatedOperation
            {
                OperationId = operation.OperationId,
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                ProposedFileName = normalizedFileName,
                ConfidenceScore = operation.ConfidenceScore,
                ReasoningSummary = operation.ReasoningSummary,
                StableOrderIndex = stableOrderIndex
            });
        }

        return new ValidatedOrganizationPlan
        {
            AuthorizedRootPath = normalizedRoot,
            ApprovedOperations = approved,
            RejectedOperations = rejected
        };
    }

    private static ValidationFailure CreateFailure(FileMoveOperation operation, string? destinationPath, ValidationFailureCode code, string message)
    {
        return new ValidationFailure
        {
            OperationId = operation.OperationId,
            SourcePath = operation.SourcePath,
            DestinationPath = destinationPath,
            Code = code,
            Message = message
        };
    }

    private static string NormalizeFileName(string proposedFileName)
    {
        var trimmed = proposedFileName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(trimmed.Select(ch => invalidChars.Contains(ch) ? '_' : ch)).Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        var maxLength = 120;
        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        var extension = Path.GetExtension(sanitized);
        var baseName = Path.GetFileNameWithoutExtension(sanitized);
        var maxBaseLength = Math.Max(1, maxLength - extension.Length);

        if (baseName.Length > maxBaseLength)
        {
            baseName = baseName[..maxBaseLength];
        }

        return $"{baseName}{extension}";
    }

    private static bool IsUnderRoot(string rootPath, string candidatePath)
    {
        var relative = Path.GetRelativePath(rootPath, candidatePath);

        if (relative == ".")
        {
            return true;
        }

        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static bool IsHiddenPath(string path, string authorizedRoot)
    {
        var relative = Path.GetRelativePath(authorizedRoot, path);
        var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => segment.StartsWith(".", StringComparison.Ordinal));
    }

    private static bool IsRepositoryProtected(string path, string authorizedRoot)
    {
        var currentDirectory = ResolveStartingDirectory(path);

        while (!string.IsNullOrEmpty(currentDirectory) &&
               Directory.Exists(currentDirectory) &&
               IsUnderRoot(authorizedRoot, currentDirectory))
        {
            if (ContainsRepositoryMarker(currentDirectory))
            {
                return true;
            }

            if (PathsEqual(currentDirectory, authorizedRoot))
            {
                break;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        return false;
    }

    private static string? ResolveStartingDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        var extension = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(extension))
        {
            var parent = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(parent) ? null : Path.GetFullPath(parent);
        }

        if (File.Exists(path))
        {
            var parent = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(parent) ? null : Path.GetFullPath(parent);
        }

        var candidateParent = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(candidateParent) ? null : Path.GetFullPath(candidateParent);
    }

    private static bool ContainsRepositoryMarker(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }

            foreach (var marker in RepositoryMarkers)
            {
                if (marker.StartsWith("*.", StringComparison.Ordinal))
                {
                    var extension = marker[1..];
                    if (Directory.EnumerateFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly).Any())
                    {
                        return true;
                    }

                    continue;
                }

                if (Directory.Exists(Path.Combine(directory, marker)) || File.Exists(Path.Combine(directory, marker)))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? SafeGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
