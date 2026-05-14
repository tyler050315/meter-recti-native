using SQLite;

namespace MeterRecti.Native.Models;

public sealed class HistoryRecord
{
	[PrimaryKey, AutoIncrement]
	public long Id { get; set; }

	public string SerialNumber { get; set; } = string.Empty;

	public string MeterReading { get; set; } = string.Empty;

	public string MeterSum { get; set; } = string.Empty;

	public DateTimeOffset CalibratedAtLocal { get; set; }

	public DateTimeOffset CalibratedAtUtc { get; set; }

	public string RawPayload { get; set; } = string.Empty;

	public string SubscribeTopic { get; set; } = string.Empty;

	public string PublishTopic { get; set; } = string.Empty;
}
