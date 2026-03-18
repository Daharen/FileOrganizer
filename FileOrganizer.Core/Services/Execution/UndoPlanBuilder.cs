using System;
using System.Collections.Generic;
using System.Linq;

namespace FileOrganizer.Core;

public sealed class UndoPlanBuilder
{
    public IReadOnlyList<UndoOperation> Build(IReadOnlyList<ExecutionJournalEntry> entries)
    {
        return entries
            .Where(entry => string.Equals(entry.ExecutionStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.OriginalPath))
            .Select((entry, index) => new
            {
                Entry = entry,
                SequenceIndex = entry.JournalSequenceIndex >= 0 ? entry.JournalSequenceIndex : index,
                CurrentPath = string.IsNullOrWhiteSpace(entry.DestinationPath)
                    ? entry.ResolvedDestinationPath
                    : entry.DestinationPath
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.CurrentPath))
            .OrderByDescending(x => x.Entry.TimestampUtc)
            .ThenByDescending(x => x.SequenceIndex)
            .Select(x => new UndoOperation(
                x.Entry.RunId,
                x.Entry.OperationId,
                x.CurrentPath,
                x.Entry.OriginalPath,
                x.Entry.TimestampUtc,
                x.SequenceIndex))
            .ToList();
    }
}
