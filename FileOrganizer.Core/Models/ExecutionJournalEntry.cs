using System;

namespace FileOrganizer.Core;

public sealed record ExecutionJournalEntry(
    string RunId,
    string OperationId,
    string OriginalPath,
    string ProposedDestinationPath,
    string ResolvedDestinationPath,
    string DestinationPath,
    string OperationType,
    string ExecutionStatus,
    DateTimeOffset TimestampUtc,
    double? ClassificationConfidence,
    string? PlanningStage,
    string? FailureReason);
