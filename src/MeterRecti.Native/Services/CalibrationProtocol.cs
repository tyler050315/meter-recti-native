using System.Text.Json;
using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public static class CalibrationProtocol
{
	public const int MaxMeterReading = 999999999;
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

	public static string AppendSerialTopic(string baseTopic, string serialNumber)
	{
		return string.Concat(baseTopic?.Trim() ?? string.Empty, serialNumber.Trim());
	}

	public static string BuildCommand(string serialNumber, string meterReading)
	{
		return $"D3{serialNumber.Trim()}B2{meterReading.Trim()}";
	}

	public static void ValidateInputs(string serialNumber, string meterReading)
	{
		if (string.IsNullOrWhiteSpace(serialNumber))
		{
			throw new InvalidOperationException("请扫描或输入 SN。");
		}

		if (string.IsNullOrWhiteSpace(meterReading) || !meterReading.All(char.IsDigit))
		{
			throw new InvalidOperationException("水表读数必须是数字。");
		}

		if (!int.TryParse(meterReading, out var reading) || reading > MaxMeterReading)
		{
			throw new InvalidOperationException("水表读数不能超过 999999999。");
		}
	}

	public static bool IsExpectedM0(string payload, string serialNumber)
	{
		using var document = JsonDocument.Parse(payload);
		var root = document.RootElement;
		return HasStringValue(root, "SN", serialNumber) && HasStringValue(root, "DEVTYPE", "M0");
	}

	public static CalibrationResult? TryReadM1Result(string payload, string serialNumber)
	{
		using var document = JsonDocument.Parse(payload);
		var root = document.RootElement;

		if (!HasStringValue(root, "SN", serialNumber) ||
			!HasStringValue(root, "DEVTYPE", "M1") ||
			!root.TryGetProperty("METERSUM", out var meterSum))
		{
			return null;
		}

		return new CalibrationResult(
			serialNumber,
			meterSum.ToString(),
			payload);
	}

	private static bool HasStringValue(JsonElement element, string propertyName, string expectedValue)
	{
		return element.TryGetProperty(propertyName, out var value) &&
			string.Equals(value.ToString(), expectedValue, StringComparison.Ordinal);
	}
}
