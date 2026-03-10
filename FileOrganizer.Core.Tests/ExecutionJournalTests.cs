using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class ExecutionJournalTests
{
    [Fact]
    public void ExecutionJournalEntry_SerializesAsSingleJsonObjectLine()
    {
        var entry = CreateEntry();
        var json = JsonSerializer.Serialize(entry);

        Assert.StartsWith("{", json);
        Assert.EndsWith("}", json);
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void ExecutionJournalEntry_SerializesNullOptionalFields()
    {
        var entry = CreateEntry() with
        {
            PlanningStage = null,
            FailureReason = null,
            ClassificationConfidence = null
        };

        var json = JsonSerializer.Serialize(entry);

        Assert.Contains("\"planningStage\":null", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"failureReason\":null", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"classificationConfidence\":null", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecutionJournalEntry_TimestampUsesIso8601Format()
    {
        var entry = CreateEntry() with
        {
            TimestampUtc = DateTimeOffset.Parse("2026-01-02T03:04:05+00:00")
        };

        var json = JsonSerializer.Serialize(entry);

        Assert.Contains("2026-01-02T03:04:05+00:00", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FileExecutionJournal_AppendAsync_CreatesDirectoryAndFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var journalPath = Path.Combine(tempRoot, "logs", "execution-journal.ndjson");
        var journal = new FileExecutionJournal(journalPath);

        journal.AppendAsync(CreateEntry()).GetAwaiter().GetResult();

        Assert.True(File.Exists(journalPath));
        var lines = File.ReadAllLines(journalPath);
        Assert.Single(lines);
    }

    [Fact]
    public void FileExecutionJournal_AppendAsync_AppendsMultipleLines()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var journalPath = Path.Combine(tempRoot, "execution-journal.ndjson");
        var journal = new FileExecutionJournal(journalPath);

        journal.AppendAsync(CreateEntry() with { OperationId = "op1" }).GetAwaiter().GetResult();
        journal.AppendAsync(CreateEntry() with { OperationId = "op2" }).GetAwaiter().GetResult();

        var lines = File.ReadAllLines(journalPath);
        Assert.Equal(2, lines.Length);
        Assert.Contains("op1", lines[0], StringComparison.Ordinal);
        Assert.Contains("op2", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void FileExecutionJournal_AppendAsync_DoesNotOverwriteExistingFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var journalPath = Path.Combine(tempRoot, "execution-journal.ndjson");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(journalPath, "existing-line" + Environment.NewLine);

        var journal = new FileExecutionJournal(journalPath);
        journal.AppendAsync(CreateEntry() with { OperationId = "op3" }).GetAwaiter().GetResult();

        var lines = File.ReadAllLines(journalPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("existing-line", lines[0]);
    }

    private static ExecutionJournalEntry CreateEntry()
    {
        return new ExecutionJournalEntry(
            RunId: "run-1",
            OperationId: "op-1",
            OriginalPath: "/source.txt",
            ProposedDestinationPath: "/proposed.txt",
            ResolvedDestinationPath: "/resolved.txt",
            DestinationPath: "/resolved.txt",
            OperationType: "Move",
            ExecutionStatus: "Succeeded",
            TimestampUtc: DateTimeOffset.UtcNow,
            ClassificationConfidence: 0.91,
            PlanningStage: "Validated",
            FailureReason: null);
    }
}
