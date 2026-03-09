using System;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class OrganizationExecutor
{
    public ExecutionResult ExecutePlan(OrganizationPlan plan)
    {
        var result = new ExecutionResult
        {
            Attempted = plan.Operations.Count
        };

        foreach (var operation in plan.Operations.OrderBy(op => op.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(operation.SourcePath))
                {
                    result.Messages.Add($"SKIP | Missing source | {operation.SourcePath}");
                    continue;
                }

                Directory.CreateDirectory(operation.DestinationDirectory);

                var safeFileName = SanitizeFileName(operation.ProposedFileName);
                var destinationPath = GetUniqueDestinationPath(operation.DestinationDirectory, safeFileName);

                if (PathsEqual(operation.SourcePath, destinationPath))
                {
                    result.Messages.Add($"SKIP | Already in destination | {operation.SourcePath}");
                    continue;
                }

                File.Move(operation.SourcePath, destinationPath);

                result.Executed++;
                result.Messages.Add($"MOVE | {operation.SourcePath} -> {destinationPath}");
            }
            catch (Exception ex)
            {
                result.Messages.Add($"FAIL | {operation.SourcePath} | {ex.Message}");
            }
        }

        result = new ExecutionResult
        {
            Attempted = result.Attempted,
            Executed = result.Executed,
            Failed = result.Messages.Count(message => message.StartsWith("FAIL |", StringComparison.Ordinal))
        }.WithMessagesFrom(result);

        return result;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed_file" : sanitized;
    }

    private static string GetUniqueDestinationPath(string destinationDirectory, string proposedFileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(proposedFileName);
        var extension = Path.GetExtension(proposedFileName);

        var candidatePath = Path.Combine(destinationDirectory, proposedFileName);
        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        var counter = 1;
        while (true)
        {
            var candidateFileName = $"{fileNameWithoutExtension} ({counter}){extension}";
            candidatePath = Path.Combine(destinationDirectory, candidateFileName);

            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            counter++;
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

internal static class ExecutionResultExtensions
{
    public static ExecutionResult WithMessagesFrom(this ExecutionResult target, ExecutionResult source)
    {
        foreach (var message in source.Messages)
        {
            target.Messages.Add(message);
        }

        return target;
    }
}
