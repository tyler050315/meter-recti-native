using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public interface IHistoryStore
{
	Task<IReadOnlyList<HistoryRecord>> GetRecordsAsync(CancellationToken cancellationToken);

	Task AddRecordAsync(HistoryRecord record, CancellationToken cancellationToken);

	Task DeleteRecordAsync(long id, CancellationToken cancellationToken);

	Task ClearAsync(CancellationToken cancellationToken);
}
