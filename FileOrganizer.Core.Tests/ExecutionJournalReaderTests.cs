using System;
using System.IO;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class ExecutionJournalReaderTests
{
    [Fact]
    public void ReadAll_ReadsMultipleNdjsonEntries()
    {
        var journalPath = CreateJournal(
            CreateEntry("run-1", "op-1", DateTimeOffset.Parse("2026-01-02T00:00:00+00:00")),
            CreateEntry("run-1", "op-2", DateTimeOffset.Parse("2026-01-02T00:01:00+00:00")));

        var reader = new FileExecutionJournalReader(journalPath);
        var entries = reader.ReadAll();

        Assert.Equal(2, entries.Count);
        Assert.Equal(0, entries[0].JournalSequenceIndex);
        Assert.Equal(1, entries[1].JournalSequenceIndex);
    }

    [Fact]
    public void ReadAll_SkipsBlankLines()
    {
        var root = CreateTempDirectory();
        var journalPath = Path.Combine(root, "journal.ndjson");
        File.WriteAllText(journalPath, Environment.NewLine + Serialize(CreateEntry("run-1", "op-1", DateTimeOffset.UtcNow)) + Environment.NewLine + "   " + Environment.NewLine);

        var reader = new FileExecutionJournalReader(journalPath);

        var entries = reader.ReadAll();

        Assert.Single(entries);
    }

    [Fact]
    public void ReadAll_ToleratesCorruptLineAndContinues()
    {
        var root = CreateTempDirectory();
        var journalPath = Path.Combine(root, "journal.ndjson");
        File.WriteAllText(
            journalPath,
            Serialize(CreateEntry("run-1", "op-1", DateTimeOffset.Parse("2026-01-02T00:00:00+00:00"))) + Environment.NewLine +
            "not-json" + Environment.NewLine +
            Serialize(CreateEntry("run-2", "op-2", DateTimeOffset.Parse("2026-01-02T00:01:00+00:00"))) + Environment.NewLine);

        var reader = new FileExecutionJournalReader(journalPath);
        var entries = reader.ReadAll();

        Assert.Equal(2, entries.Count);
        Assert.Single(reader.ParseFailures);
        Assert.Contains("JOURNAL_PARSE_FAIL", reader.ParseFailures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ReadLatestRunId_ReturnsLatestRunId()
    {
        var journalPath = CreateJournal(
            CreateEntry("run-older", "op-1", DateTimeOffset.Parse("2026-01-02T00:00:00+00:00")),
            CreateEntry("run-newer", "op-2", DateTimeOffset.Parse("2026-01-02T00:05:00+00:00")));

        var reader = new FileExecutionJournalReader(journalPath);

        var latestRunId = reader.ReadLatestRunId();

        Assert.Equal("run-newer", latestRunId);
    }

    private static string CreateJournal(params ExecutionJournalEntry[] entries)
    {
        var root = CreateTempDirectory();
        var journalPath = Path.Combine(root, "journal.ndjson");
        File.WriteAllLines(journalPath, Array.ConvertAll(entries, Serialize));
        return journalPath;
    }

    private static string Serialize(ExecutionJournalEntry entry) => System.Text.Json.JsonSerializer.Serialize(entry);

    private static ExecutionJournalEntry CreateEntry(string runId, string operationId, DateTimeOffset timestampUtc)
    {
        return new ExecutionJournalEntry(
            RunId: runId,
            OperationId: operationId,
            OriginalPath: "/source.txt",
            ProposedDestinationPath: "/proposed.txt",
            ResolvedDestinationPath: "/resolved.txt",
            DestinationPath: "/destination.txt",
            OperationType: "Move",
            ExecutionStatus: "Succeeded",
            TimestampUtc: timestampUtc,
            ClassificationConfidence: 0.91,
            PlanningStage: "Validated",
            FailureReason: null);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
