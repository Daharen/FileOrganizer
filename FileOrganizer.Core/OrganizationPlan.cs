using System;
using System.Collections.Generic;

namespace FileOrganizer.Core;

public sealed class FileMoveOperation
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationDirectory { get; init; } = string.Empty;

    public string ProposedFileName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public double ConfidenceScore { get; init; }

    public string ReasoningSummary { get; init; } = string.Empty;
}

public sealed class PlanSkipRecord
{
    public string SourcePath { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class OrganizationPlan
{
    public List<FileMoveOperation> Operations { get; } = new();

    public List<PlanSkipRecord> SkippedFiles { get; } = new();
}

public sealed class ExecutionResult
{
    public int Approved { get; init; }

    public int Rejected { get; init; }

    public int Attempted { get; init; }

    public int Executed { get; init; }

    public int Failed { get; init; }

    public List<string> Messages { get; } = new();
}
