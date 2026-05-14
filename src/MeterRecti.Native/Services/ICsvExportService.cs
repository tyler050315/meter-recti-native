using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public interface ICsvExportService
{
	string CreateHistoryCsv(IEnumerable<HistoryRecord> records);
}
