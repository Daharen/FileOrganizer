using System.Collections.Generic;

namespace FileOrganizer.Core;

public sealed class UndoResult
{
    public string RunId { get; init; } = string.Empty;

    public int Attempted { get; set; }

    public int Restored { get; set; }

    public int Failed { get; set; }

    public int Skipped { get; set; }

    public int CollisionPreserved { get; set; }

    public List<string> Messages { get; } = new();
}
