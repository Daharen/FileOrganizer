using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileOrganizer.Core;

public sealed class FileExecutionJournal : IExecutionJournal
{
    private readonly string _journalPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileExecutionJournal(string journalPath)
    {
        _journalPath = journalPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };
    }

    public Task AppendAsync(ExecutionJournalEntry entry, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_journalPath)
            ?? throw new InvalidOperationException("Journal directory is missing.");

        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(entry, _jsonOptions);

        return File.AppendAllTextAsync(
            _journalPath,
            json + Environment.NewLine,
            Encoding.UTF8,
            cancellationToken);
    }
}
