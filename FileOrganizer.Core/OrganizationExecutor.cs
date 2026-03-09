using System;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class OrganizationExecutor
{
    public ExecutionResult ExecutePlan(OrganizationPlan plan)
    {
        var messages = new System.Collections.Generic.List<string>();
        var executed = 0;
        var failed = 0;

        foreach (var operation in plan.Operations.OrderBy(op => op.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(operation.SourcePath))
                {
                    messages.Add($"SKIP | Missing source | {operation.SourcePath}");
                    continue;
                }

                Directory.CreateDirectory(operation.DestinationDirectory);

                var safeFileName = SanitizeFileName(operation.ProposedFileName);
                var destinationPath = GetUniqueDestinationPath(operation.DestinationDirectory, safeFileName);

                if (PathsEqual(operation.SourcePath, destinationPath))
                {
                    messages.Add($"SKIP | Already in destination | {operation.SourcePath}");
                    continue;
                }

                File.Move(operation.SourcePath, destinationPath);

                executed++;
                messages.Add($"MOVE | {operation.SourcePath} -> {destinationPath}");
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add($"FAIL | {operation.SourcePath} | {ex.Message}");
            }
        }

        var result = new ExecutionResult
        {
            Attempted = plan.Operations.Count,
            Executed = executed,
            Failed = failed
        };

        foreach (var message in messages)
        {
            result.Messages.Add(message);
        }

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
