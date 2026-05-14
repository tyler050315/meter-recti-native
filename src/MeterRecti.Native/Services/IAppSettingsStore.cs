using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public interface IAppSettingsStore
{
	MqttSettings? ReadMqttSettings();

	void SaveMqttSettings(MqttSettings settings);
}
