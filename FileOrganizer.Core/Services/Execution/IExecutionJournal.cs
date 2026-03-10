using System.Threading;
using System.Threading.Tasks;

namespace FileOrganizer.Core;

public interface IExecutionJournal
{
    Task AppendAsync(ExecutionJournalEntry entry, CancellationToken cancellationToken = default);
}
