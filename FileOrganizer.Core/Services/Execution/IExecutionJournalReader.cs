using System.Collections.Generic;

namespace FileOrganizer.Core;

public interface IExecutionJournalReader
{
    IReadOnlyList<ExecutionJournalEntry> ReadAll();

    IReadOnlyList<ExecutionJournalEntry> ReadByRunId(string runId);

    string? ReadLatestRunId();
}
