using System;
using System.Collections.Generic;
using System.IO;

namespace FileOrganizer.Core;

public sealed class UndoExecutor
{
    private readonly IUndoCollisionResolver _undoCollisionResolver;

    public UndoExecutor()
        : this(new UndoCollisionResolver())
    {
    }

    public UndoExecutor(IUndoCollisionResolver undoCollisionResolver)
    {
        _undoCollisionResolver = undoCollisionResolver;
    }

    public UndoResult Execute(string runId, string authorizedRootPath, IReadOnlyList<UndoOperation> undoOperations)
    {
        var normalizedRoot = Path.GetFullPath(authorizedRootPath);
        var result = new UndoResult
        {
            RunId = runId,
            Attempted = undoOperations.Count
        };

        foreach (var operation in undoOperations)
        {
            try
            {
                if (!File.Exists(operation.CurrentPath))
                {
                    result.Skipped++;
                    result.Messages.Add($"UNDO_SKIP | {operation.OperationId} | Missing current file | {operation.CurrentPath}");
                    continue;
                }

                if (!IsUnderRoot(normalizedRoot, operation.CurrentPath) || !IsUnderRoot(normalizedRoot, operation.TargetRestorePath))
                {
                    result.Failed++;
                    result.Messages.Add($"UNDO_FAIL | {operation.OperationId} | Runtime boundary check failed");
                    continue;
                }

                var targetDirectory = Path.GetDirectoryName(operation.TargetRestorePath);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    result.Failed++;
                    result.Messages.Add($"UNDO_FAIL | {operation.OperationId} | Invalid restore directory");
                    continue;
                }

                Directory.CreateDirectory(targetDirectory);

                if (!File.Exists(operation.TargetRestorePath))
                {
                    File.Move(operation.CurrentPath, operation.TargetRestorePath);
                    result.Restored++;
                    result.Messages.Add($"UNDO_RESTORE | {operation.OperationId} | {operation.CurrentPath} -> {operation.TargetRestorePath}");
                }
                else
                {
                    var preservedPath = _undoCollisionResolver.ResolvePreservedUndoPath(operation.TargetRestorePath, operation.CurrentPath);
                    File.Move(operation.CurrentPath, preservedPath);
                    result.CollisionPreserved++;
                    result.Messages.Add($"UNDO_COLLISION | {operation.OperationId} | Restore target occupied, preserved at {preservedPath}");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"UNDO_FAIL | {operation.OperationId} | {ex.Message}");
            }
        }

        return result;
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
}
