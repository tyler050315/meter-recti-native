namespace MeterRecti.Native.Models;

public sealed record HistoryRecord(
	long Id,
	string SerialNumber,
	string MeterReading,
	string MeterSum,
	DateTimeOffset CalibratedAtLocal,
	DateTimeOffset CalibratedAtUtc,
	string RawPayload,
	string SubscribeTopic,
	string PublishTopic);
