using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FileOrganizer.Core;

public sealed class FileExecutionJournalReader : IExecutionJournalReader
{
    private readonly string _journalPath;
    private readonly JsonSerializerOptions _jsonOptions = new();

    public FileExecutionJournalReader(string journalPath)
    {
        _journalPath = journalPath;
    }

    public IReadOnlyList<string> ParseFailures { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<ExecutionJournalEntry> ReadAll()
    {
        if (!File.Exists(_journalPath))
        {
            ParseFailures = Array.Empty<string>();
            return Array.Empty<ExecutionJournalEntry>();
        }

        var entries = new List<IndexedJournalEntry>();
        var parseFailures = new List<string>();
        var sequenceIndex = 0;

        foreach (var rawLine in File.ReadLines(_journalPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ExecutionJournalEntry>(rawLine, _jsonOptions);
                if (entry is null)
                {
                    parseFailures.Add($"JOURNAL_PARSE_FAIL | seq={sequenceIndex} | Entry deserialized to null.");
                }
                else
                {
                    entries.Add(new IndexedJournalEntry(entry, sequenceIndex));
                }
            }
            catch (Exception ex)
            {
                parseFailures.Add($"JOURNAL_PARSE_FAIL | seq={sequenceIndex} | {ex.Message}");
            }
            finally
            {
                sequenceIndex++;
            }
        }

        ParseFailures = parseFailures;
        return entries
            .OrderBy(item => item.SequenceIndex)
            .Select(item => item.Entry with { JournalSequenceIndex = item.SequenceIndex })
            .ToList();
    }

    public IReadOnlyList<ExecutionJournalEntry> ReadByRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return Array.Empty<ExecutionJournalEntry>();
        }

        return ReadAll()
            .Where(entry => string.Equals(entry.RunId, runId, StringComparison.Ordinal))
            .ToList();
    }

    public string? ReadLatestRunId()
    {
        return ReadAll()
            .OrderByDescending(entry => entry.TimestampUtc)
            .ThenByDescending(entry => entry.JournalSequenceIndex)
            .Select(entry => entry.RunId)
            .FirstOrDefault();
    }

    private sealed record IndexedJournalEntry(ExecutionJournalEntry Entry, int SequenceIndex);
}
