using System;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class OrganizationExecutor
{
    public ExecutionResult ExecutePlan(ValidatedOrganizationPlan plan)
    {
        var messages = new System.Collections.Generic.List<string>();
        var executed = 0;
        var failed = 0;

        foreach (var operation in plan.ApprovedOperations.OrderBy(op => op.StableOrderIndex))
        {
            try
            {
                if (!File.Exists(operation.SourcePath))
                {
                    failed++;
                    messages.Add($"FAIL | Missing source at execution | {operation.SourcePath}");
                    continue;
                }

                if (!IsUnderRoot(plan.AuthorizedRootPath, operation.SourcePath) ||
                    !IsUnderRoot(plan.AuthorizedRootPath, operation.DestinationPath))
                {
                    failed++;
                    messages.Add($"FAIL | Runtime boundary check failed | {operation.SourcePath}");
                    continue;
                }

                var destinationDirectory = Path.GetDirectoryName(operation.DestinationPath);
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    failed++;
                    messages.Add($"FAIL | Invalid destination directory | {operation.DestinationPath}");
                    continue;
                }

                Directory.CreateDirectory(destinationDirectory);

                if (PathsEqual(operation.SourcePath, operation.DestinationPath))
                {
                    messages.Add($"SKIP | Already in destination | {operation.SourcePath}");
                    continue;
                }

                if (File.Exists(operation.DestinationPath))
                {
                    failed++;
                    messages.Add($"FAIL | Destination collision at execution | {operation.DestinationPath}");
                    continue;
                }

                File.Move(operation.SourcePath, operation.DestinationPath);

                executed++;
                messages.Add($"MOVE | {operation.SourcePath} -> {operation.DestinationPath}");
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add($"FAIL | {operation.SourcePath} | {ex.Message}");
            }
        }

        var result = new ExecutionResult
        {
            Approved = plan.ApprovedOperations.Count,
            Rejected = plan.RejectedOperations.Count,
            Attempted = plan.ApprovedOperations.Count,
            Executed = executed,
            Failed = failed
        };

        foreach (var message in messages)
        {
            result.Messages.Add(message);
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

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
