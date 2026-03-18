using System;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class UndoPlanBuilderTests
{
    [Fact]
    public void Build_IncludesOnlySucceededEntries()
    {
        var builder = new UndoPlanBuilder();
        var entries = new[]
        {
            CreateEntry("op-1", "Succeeded", 0, "/dest-1.txt", "/source-1.txt", DateTimeOffset.Parse("2026-01-02T00:00:00+00:00")),
            CreateEntry("op-2", "Failed", 1, "/dest-2.txt", "/source-2.txt", DateTimeOffset.Parse("2026-01-02T00:01:00+00:00")),
            CreateEntry("op-3", "Skipped", 2, "/dest-3.txt", "/source-3.txt", DateTimeOffset.Parse("2026-01-02T00:02:00+00:00"))
        };

        var plan = builder.Build(entries);

        Assert.Single(plan);
        Assert.Equal("op-1", plan[0].OperationId);
    }

    [Fact]
    public void Build_ReversesOrderByTimestampThenSequence()
    {
        var builder = new UndoPlanBuilder();
        var timestamp = DateTimeOffset.Parse("2026-01-02T00:00:00+00:00");
        var entries = new[]
        {
            CreateEntry("op-1", "Succeeded", 0, "/dest-1.txt", "/source-1.txt", timestamp),
            CreateEntry("op-2", "Succeeded", 1, "/dest-2.txt", "/source-2.txt", timestamp),
            CreateEntry("op-3", "Succeeded", 2, "/dest-3.txt", "/source-3.txt", timestamp.AddMinutes(1))
        };

        var plan = builder.Build(entries);

        Assert.Collection(plan,
            op => Assert.Equal("op-3", op.OperationId),
            op => Assert.Equal("op-2", op.OperationId),
            op => Assert.Equal("op-1", op.OperationId));
    }

    [Fact]
    public void Build_UsesDestinationToOriginalMapping_WithResolvedFallback()
    {
        var builder = new UndoPlanBuilder();
        var entries = new[]
        {
            CreateEntry("op-1", "Succeeded", 0, string.Empty, "/source-1.txt", DateTimeOffset.UtcNow) with { ResolvedDestinationPath = "/resolved-1.txt" }
        };

        var plan = builder.Build(entries);

        Assert.Single(plan);
        Assert.Equal("/resolved-1.txt", plan[0].CurrentPath);
        Assert.Equal("/source-1.txt", plan[0].TargetRestorePath);
    }

    private static ExecutionJournalEntry CreateEntry(string operationId, string status, int index, string destinationPath, string originalPath, DateTimeOffset timestamp)
    {
        return new ExecutionJournalEntry(
            RunId: "run-1",
            OperationId: operationId,
            OriginalPath: originalPath,
            ProposedDestinationPath: destinationPath,
            ResolvedDestinationPath: destinationPath,
            DestinationPath: destinationPath,
            OperationType: "Move",
            ExecutionStatus: status,
            TimestampUtc: timestamp,
            ClassificationConfidence: 0.9,
            PlanningStage: "Validated",
            FailureReason: null,
            JournalSequenceIndex: index);
    }
}
