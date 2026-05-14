using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public interface IMqttService
{
	event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

	bool IsConnected { get; }

	Task ConnectAsync(MqttSettings settings, CancellationToken cancellationToken);

	Task DisconnectAsync(CancellationToken cancellationToken);

	Task SubscribeAsync(string topic, CancellationToken cancellationToken);

	Task UnsubscribeAsync(string topic, CancellationToken cancellationToken);

	Task PublishAsync(string topic, string payload, CancellationToken cancellationToken);
}
