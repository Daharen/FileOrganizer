using System;

namespace FileOrganizer.Core;

public sealed record UndoOperation(
    string RunId,
    string OperationId,
    string CurrentPath,
    string TargetRestorePath,
    DateTimeOffset OriginalExecutionTimestampUtc,
    int JournalSequenceIndex);
