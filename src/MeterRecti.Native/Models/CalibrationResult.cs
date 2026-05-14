namespace MeterRecti.Native.Models;

public sealed record CalibrationResult(
	string SerialNumber,
	string MeterSum,
	string RawPayload);
