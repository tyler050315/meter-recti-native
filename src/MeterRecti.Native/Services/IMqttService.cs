using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public interface IMqttService
{
	bool IsConnected { get; }

	Task ConnectAsync(MqttSettings settings, CancellationToken cancellationToken);

	Task DisconnectAsync(CancellationToken cancellationToken);
}
