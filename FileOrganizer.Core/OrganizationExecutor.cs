using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class OrganizationExecutor
{
    private readonly IExecutionJournal _executionJournal;
    private readonly string _journalPath;

    public OrganizationExecutor(IExecutionJournal executionJournal, string journalPath)
    {
        _executionJournal = executionJournal;
        _journalPath = journalPath;
    }

    public ExecutionResult ExecutePlan(ValidatedOrganizationPlan plan)
    {
        var messages = new List<string>();
        var executed = 0;
        var failed = 0;
        var skipped = 0;
        var journalAppendFailures = 0;
        var journalEntriesAppended = 0;
        var runId = Guid.NewGuid().ToString("N");

        foreach (var operation in plan.ApprovedOperations.OrderBy(op => op.StableOrderIndex))
        {
            string executionStatus;
            string? failureReason = null;

            try
            {
                if (!File.Exists(operation.SourcePath))
                {
                    executionStatus = "Skipped";
                    skipped++;
                    failureReason = "Missing source at execution.";
                    messages.Add($"SKIP | Missing source at execution | {operation.SourcePath}");
                }
                else if (!IsUnderRoot(plan.AuthorizedRootPath, operation.SourcePath) ||
                         !IsUnderRoot(plan.AuthorizedRootPath, operation.DestinationPath))
                {
                    executionStatus = "Skipped";
                    skipped++;
                    failureReason = "Runtime boundary check failed.";
                    messages.Add($"SKIP | Runtime boundary check failed | {operation.SourcePath}");
                }
                else
                {
                    var destinationDirectory = Path.GetDirectoryName(operation.DestinationPath);
                    if (string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        executionStatus = "Failed";
                        failed++;
                        failureReason = "Invalid destination directory.";
                        messages.Add($"FAIL | Invalid destination directory | {operation.DestinationPath}");
                    }
                    else
                    {
                        Directory.CreateDirectory(destinationDirectory);

                        if (PathsEqual(operation.SourcePath, operation.DestinationPath))
                        {
                            executionStatus = "Skipped";
                            skipped++;
                            failureReason = "Already in destination.";
                            messages.Add($"SKIP | Already in destination | {operation.SourcePath}");
                        }
                        else if (File.Exists(operation.DestinationPath))
                        {
                            executionStatus = "Skipped";
                            skipped++;
                            failureReason = "Destination collision at execution.";
                            messages.Add($"SKIP | Destination collision at execution | {operation.DestinationPath}");
                        }
                        else
                        {
                            File.Move(operation.SourcePath, operation.DestinationPath);

                            executionStatus = "Succeeded";
                            executed++;
                            messages.Add($"MOVE | {operation.SourcePath} -> {operation.DestinationPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                executionStatus = "Failed";
                failed++;
                failureReason = ex.Message;
                messages.Add($"FAIL | {operation.SourcePath} | {ex.Message}");
            }

            try
            {
                var entry = new ExecutionJournalEntry(
                    RunId: runId,
                    OperationId: operation.OperationId,
                    OriginalPath: operation.SourcePath,
                    ProposedDestinationPath: operation.OriginalProposedDestinationPath,
                    ResolvedDestinationPath: operation.ResolvedDestinationPath,
                    DestinationPath: operation.DestinationPath,
                    OperationType: OperationTypeClassifier.Classify(operation.SourcePath, operation.DestinationPath),
                    ExecutionStatus: executionStatus,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    ClassificationConfidence: operation.ConfidenceScore,
                    PlanningStage: operation.PlanningStage,
                    FailureReason: failureReason);

                _executionJournal.AppendAsync(entry).GetAwaiter().GetResult();
                journalEntriesAppended++;
            }
            catch (Exception ex)
            {
                journalAppendFailures++;
                messages.Add($"JOURNAL_FAIL | {operation.OperationId} | {ex.Message}");
            }
        }

        var result = new ExecutionResult
        {
            Approved = plan.ApprovedOperations.Count,
            Rejected = plan.RejectedOperations.Count,
            Attempted = plan.ApprovedOperations.Count,
            Executed = executed,
            Failed = failed,
            Skipped = skipped,
            JournalAppendFailures = journalAppendFailures,
            JournalEntriesAppended = journalEntriesAppended,
            RunId = runId,
            JournalPath = _journalPath
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
            PathComparisonPolicy.PathComparison);
    }
}
