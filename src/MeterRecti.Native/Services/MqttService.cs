using MeterRecti.Native.Models;
using MQTTnet;
using MQTTnet.Packets;

namespace MeterRecti.Native.Services;

public sealed class MqttService : IMqttService
{
	private IMqttClient? client;

	public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

	public bool IsConnected => client?.IsConnected == true;

	public async Task ConnectAsync(MqttSettings settings, CancellationToken cancellationToken)
	{
		Validate(settings);

		if (client?.IsConnected == true)
		{
			await client.DisconnectAsync(cancellationToken: cancellationToken);
		}

		client = new MqttClientFactory().CreateMqttClient();
		client.ApplicationMessageReceivedAsync += args =>
		{
			var payload = args.ApplicationMessage.ConvertPayloadToString();
			MessageReceived?.Invoke(
				this,
				new MqttMessageReceivedEventArgs(
					args.ApplicationMessage.Topic,
					payload,
					args.ApplicationMessage.Retain));

			return Task.CompletedTask;
		};

		var optionsBuilder = new MqttClientOptionsBuilder()
			.WithClientId(settings.ClientId)
			.WithTcpServer(settings.Host, settings.Port)
			.WithCleanSession()
			.WithTimeout(TimeSpan.FromSeconds(15));

		if (!string.IsNullOrWhiteSpace(settings.Username))
		{
			optionsBuilder.WithCredentials(settings.Username, settings.Password);
		}

		if (settings.UseTls)
		{
			optionsBuilder.WithTlsOptions(tls =>
			{
				tls.UseTls();
				tls.WithTargetHost(settings.Host);
			});
		}

		await client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken)
	{
		if (client?.IsConnected == true)
		{
			await client.DisconnectAsync(cancellationToken: cancellationToken);
		}
	}

	public async Task SubscribeAsync(string topic, CancellationToken cancellationToken)
	{
		EnsureConnected();
		var options = new MqttClientSubscribeOptions
		{
			TopicFilters =
			[
				new MqttTopicFilter
				{
					Topic = topic
				}
			]
		};

		await client!.SubscribeAsync(options, cancellationToken);
	}

	public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken)
	{
		if (client?.IsConnected != true)
		{
			return;
		}

		var options = new MqttClientUnsubscribeOptions
		{
			TopicFilters = [topic]
		};

		await client.UnsubscribeAsync(options, cancellationToken);
	}

	public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
	{
		EnsureConnected();
		var message = new MqttApplicationMessageBuilder()
			.WithTopic(topic)
			.WithPayload(payload)
			.Build();

		await client!.PublishAsync(message, cancellationToken);
	}

	private static void Validate(MqttSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.Host))
		{
			throw new InvalidOperationException("Broker 地址不能为空。");
		}

		if (settings.Port is < 1 or > 65535)
		{
			throw new InvalidOperationException("端口必须在 1 到 65535 之间。");
		}

		if (string.IsNullOrWhiteSpace(settings.SubscribeTopic))
		{
			throw new InvalidOperationException("订阅 Topic 不能为空。");
		}

		if (string.IsNullOrWhiteSpace(settings.PublishTopic))
		{
			throw new InvalidOperationException("发布 Topic 不能为空。");
		}
	}

	private void EnsureConnected()
	{
		if (client?.IsConnected != true)
		{
			throw new InvalidOperationException("MQTT 未连接。");
		}
	}
}
