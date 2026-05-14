using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public sealed class CsvExportService : ICsvExportService
{
	public string CreateHistoryCsv(IEnumerable<HistoryRecord> records)
	{
		var rows = new List<string>
		{
			string.Join(",", ["SN", "METERSUM", "校准时间", "ISO 时间", "原始数据"])
		};

		rows.AddRange(records.Select(record => string.Join(
			",",
			CsvCell(record.SerialNumber),
			CsvCell(record.MeterSum),
			CsvCell(record.CalibratedAtLocal.ToString("yyyy-MM-dd HH:mm:ss")),
			CsvCell(record.CalibratedAtUtc.ToString("O")),
			CsvCell(record.RawPayload))));

		return "\uFEFF" + string.Join(Environment.NewLine, rows);
	}

	private static string CsvCell(string? value)
	{
		return $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
	}
}
