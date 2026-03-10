using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class OrganizationExecutorJournalIntegrationTests
{
    [Fact]
    public void ExecutePlan_Success_AppendsSucceededEntry()
    {
        var root = CreateTempDirectory();
        var source = CreateFile(root, "a.txt");
        var destination = Path.Combine(root, "organized", "a.txt");

        var journal = new InMemoryExecutionJournal();
        var executor = new OrganizationExecutor(journal, Path.Combine(root, "execution-journal.ndjson"));

        var result = executor.ExecutePlan(CreatePlan(root, new ValidatedOperation
        {
            OperationId = "op1",
            SourcePath = source,
            DestinationPath = destination,
            OriginalProposedDestinationPath = destination,
            ResolvedDestinationPath = destination,
            ConfidenceScore = 0.8,
            PlanningStage = "Validated",
            StableOrderIndex = 0
        }));

        Assert.Single(journal.Entries);
        Assert.Equal("Succeeded", journal.Entries[0].ExecutionStatus);
        Assert.Equal("op1", journal.Entries[0].OperationId);
        Assert.Equal(1, result.JournalEntriesAppended);
    }

    [Fact]
    public void ExecutePlan_FailedMove_AppendsFailedEntryWithFailureReason()
    {
        var root = CreateTempDirectory();
        var source = CreateFile(root, "b.txt");
        var destinationDirectory = Path.Combine(root, "occupied");
        Directory.CreateDirectory(destinationDirectory);
        var destination = destinationDirectory;

        var journal = new InMemoryExecutionJournal();
        var executor = new OrganizationExecutor(journal, Path.Combine(root, "execution-journal.ndjson"));

        executor.ExecutePlan(CreatePlan(root, new ValidatedOperation
        {
            OperationId = "op2",
            SourcePath = source,
            DestinationPath = destination,
            OriginalProposedDestinationPath = destination,
            ResolvedDestinationPath = destination,
            StableOrderIndex = 0
        }));

        Assert.Single(journal.Entries);
        Assert.Equal("Failed", journal.Entries[0].ExecutionStatus);
        Assert.False(string.IsNullOrWhiteSpace(journal.Entries[0].FailureReason));
    }

    [Fact]
    public void ExecutePlan_ContinuesAfterFailure()
    {
        var root = CreateTempDirectory();
        var missingSource = Path.Combine(root, "missing.txt");
        var source2 = CreateFile(root, "c.txt");
        var dest1 = Path.Combine(root, "organized", "missing.txt");
        var dest2 = Path.Combine(root, "organized", "c.txt");

        var journal = new InMemoryExecutionJournal();
        var executor = new OrganizationExecutor(journal, Path.Combine(root, "execution-journal.ndjson"));

        var result = executor.ExecutePlan(CreatePlan(root,
            new ValidatedOperation
            {
                OperationId = "op3",
                SourcePath = missingSource,
                DestinationPath = dest1,
                OriginalProposedDestinationPath = dest1,
                ResolvedDestinationPath = dest1,
                StableOrderIndex = 0
            },
            new ValidatedOperation
            {
                OperationId = "op4",
                SourcePath = source2,
                DestinationPath = dest2,
                OriginalProposedDestinationPath = dest2,
                ResolvedDestinationPath = dest2,
                StableOrderIndex = 1
            }));

        Assert.Equal(2, journal.Entries.Count);
        Assert.Contains(journal.Entries, entry => entry.OperationId == "op3" && entry.ExecutionStatus == "Skipped");
        Assert.Contains(journal.Entries, entry => entry.OperationId == "op4" && entry.ExecutionStatus == "Succeeded");
        Assert.Equal(1, result.Executed);
    }

    [Fact]
    public void ExecutePlan_UsesSameRunIdForBatch_AndNewRunIdPerExecution()
    {
        var root = CreateTempDirectory();
        var source1 = CreateFile(root, "d1.txt");
        var source2 = CreateFile(root, "d2.txt");
        var destination1 = Path.Combine(root, "organized", "d1.txt");
        var destination2 = Path.Combine(root, "organized", "d2.txt");

        var journal = new InMemoryExecutionJournal();
        var executor = new OrganizationExecutor(journal, Path.Combine(root, "execution-journal.ndjson"));

        var firstResult = executor.ExecutePlan(CreatePlan(root,
            CreateValidatedOperation("op5", source1, destination1, 0),
            CreateValidatedOperation("op6", source2, destination2, 1)));

        var firstRunIds = journal.Entries.Select(entry => entry.RunId).Distinct().ToList();
        Assert.Single(firstRunIds);
        Assert.Equal(firstResult.RunId, firstRunIds[0]);

        var source3 = CreateFile(root, "d3.txt");
        var destination3 = Path.Combine(root, "organized", "d3.txt");

        var secondResult = executor.ExecutePlan(CreatePlan(root,
            CreateValidatedOperation("op7", source3, destination3, 0)));

        Assert.NotEqual(firstResult.RunId, secondResult.RunId);
    }

    private static ValidatedOperation CreateValidatedOperation(string operationId, string source, string destination, int stableOrder)
    {
        return new ValidatedOperation
        {
            OperationId = operationId,
            SourcePath = source,
            DestinationPath = destination,
            OriginalProposedDestinationPath = destination,
            ResolvedDestinationPath = destination,
            ConfidenceScore = 0.95,
            PlanningStage = "Validated",
            StableOrderIndex = stableOrder
        };
    }

    private static ValidatedOrganizationPlan CreatePlan(string rootPath, params ValidatedOperation[] operations)
    {
        return new ValidatedOrganizationPlan
        {
            AuthorizedRootPath = rootPath,
            ApprovedOperations = operations.ToList(),
            RejectedOperations = new List<ValidationFailure>()
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "content");
        return path;
    }

    private sealed class InMemoryExecutionJournal : IExecutionJournal
    {
        public List<ExecutionJournalEntry> Entries { get; } = new();

        public Task AppendAsync(ExecutionJournalEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
