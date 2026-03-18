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

    public string OriginalProposedDestinationPath { get; init; } = string.Empty;

    public string ResolvedDestinationPath { get; init; } = string.Empty;

    public bool CollisionResolutionApplied { get; init; }

    public string? CollisionResolutionReason { get; init; }

    public string ProposedFileName { get; init; } = string.Empty;

    public double ConfidenceScore { get; init; }

    public string ReasoningSummary { get; init; } = string.Empty;

    public string PlanningStage { get; init; } = "Validated";

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

    private readonly ICollisionResolver _collisionResolver;

    public OperationPlanValidator()
        : this(new DeterministicCollisionResolver())
    {
    }

    public OperationPlanValidator(ICollisionResolver collisionResolver)
    {
        _collisionResolver = collisionResolver;
    }

    public ValidatedOrganizationPlan Validate(string authorizedRootPath, OrganizationPlan plan)
    {
        var normalizedRoot = Path.GetFullPath(authorizedRootPath);
        var approved = new List<ValidatedOperation>();
        var rejected = new List<ValidationFailure>();
        var reservedDestinations = new HashSet<string>(PathComparisonPolicy.PathComparer);

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

            var proposedDestinationPath = SafeGetFullPath(Path.Combine(destinationDirectory, normalizedFileName));
            if (proposedDestinationPath is null)
            {
                rejected.Add(CreateFailure(operation, null, ValidationFailureCode.InvalidDestination, "Destination path could not be resolved."));
                continue;
            }

            if (!IsAllowedDestination(normalizedRoot, proposedDestinationPath, operation, rejected))
            {
                continue;
            }

            ReserveExistingCollisionPaths(destinationDirectory, reservedDestinations);

            var resolvedDestinationPath = _collisionResolver.ResolveDestinationPath(
                proposedDestinationPath,
                reservedDestinations,
                PathComparisonPolicy.PathComparison);

            var normalizedResolvedDestinationPath = SafeGetFullPath(resolvedDestinationPath);
            if (normalizedResolvedDestinationPath is null)
            {
                rejected.Add(CreateFailure(operation, proposedDestinationPath, ValidationFailureCode.InvalidDestination, "Resolved destination path could not be resolved."));
                continue;
            }

            if (!IsAllowedDestination(normalizedRoot, normalizedResolvedDestinationPath, operation, rejected))
            {
                continue;
            }

            var resolvedFileName = Path.GetFileName(normalizedResolvedDestinationPath);
            if (!IsLegalResolvedFileName(resolvedFileName))
            {
                rejected.Add(CreateFailure(operation, normalizedResolvedDestinationPath, ValidationFailureCode.InvalidFileName, "Resolved file name is not legal."));
                continue;
            }

            approved.Add(new ValidatedOperation
            {
                OperationId = operation.OperationId,
                SourcePath = sourcePath,
                DestinationPath = normalizedResolvedDestinationPath,
                OriginalProposedDestinationPath = proposedDestinationPath,
                ResolvedDestinationPath = normalizedResolvedDestinationPath,
                CollisionResolutionApplied = !PathsEqual(proposedDestinationPath, normalizedResolvedDestinationPath),
                CollisionResolutionReason = !PathsEqual(proposedDestinationPath, normalizedResolvedDestinationPath)
                    ? "Destination collision resolved by numeric suffix."
                    : null,
                ProposedFileName = normalizedFileName,
                ConfidenceScore = operation.ConfidenceScore,
                ReasoningSummary = operation.ReasoningSummary,
                PlanningStage = ExtractPlanningStage(operation.ReasoningSummary),
                StableOrderIndex = stableOrderIndex
            });

            reservedDestinations.Add(normalizedResolvedDestinationPath);
        }

        return new ValidatedOrganizationPlan
        {
            AuthorizedRootPath = normalizedRoot,
            ApprovedOperations = approved,
            RejectedOperations = rejected
        };
    }

    private static void ReserveExistingCollisionPaths(string destinationDirectory, HashSet<string> reservedDestinations)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            return;
        }

        foreach (var existingPath in Directory.EnumerateFiles(destinationDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var normalized = SafeGetFullPath(existingPath);
            if (normalized is not null)
            {
                reservedDestinations.Add(normalized);
            }
        }
    }

    private static bool IsAllowedDestination(
        string normalizedRoot,
        string destinationPath,
        FileMoveOperation operation,
        List<ValidationFailure> rejected)
    {
        if (!IsUnderRoot(normalizedRoot, destinationPath))
        {
            rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.DestinationOutsideAuthorizedRoot, "Destination is outside the authorized root."));
            return false;
        }

        if (IsHiddenPath(destinationPath, normalizedRoot))
        {
            rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.HiddenPathBlocked, "Hidden destination paths are blocked by default."));
            return false;
        }

        if (IsRepositoryProtected(destinationPath, normalizedRoot))
        {
            rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.RepositoryProtected, "Destination is inside a detected repository boundary."));
            return false;
        }

        var destinationExtension = Path.GetExtension(destinationPath);
        if (ProtectedExecutableExtensions.Contains(destinationExtension))
        {
            rejected.Add(CreateFailure(operation, destinationPath, ValidationFailureCode.ExecutableProtected, "Protected executable targets are blocked."));
            return false;
        }

        return true;
    }

    private static bool IsLegalResolvedFileName(string resolvedFileName)
    {
        if (string.IsNullOrWhiteSpace(resolvedFileName))
        {
            return false;
        }

        return string.Equals(NormalizeFileName(resolvedFileName), resolvedFileName, StringComparison.Ordinal);
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

        if (OperatingSystem.IsWindows())
        {
            sanitized = sanitized.TrimEnd('.', ' ');
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
            PathComparisonPolicy.PathComparison);
    }

    private static string ExtractPlanningStage(string reasoningSummary)
    {
        if (string.IsNullOrWhiteSpace(reasoningSummary))
        {
            return "validated";
        }

        var separatorIndex = reasoningSummary.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return "validated";
        }

        var stage = reasoningSummary[..separatorIndex].Trim();
        return string.IsNullOrWhiteSpace(stage) ? "validated" : stage;
    }

}
