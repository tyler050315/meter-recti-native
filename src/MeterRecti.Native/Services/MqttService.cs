using System.Net.Sockets;
using MeterRecti.Native.Models;
using MQTTnet;
using MQTTnet.Formatter;
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
		var host = settings.Host.Trim();

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
			.WithTcpServer(host, settings.Port)
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
				tls.WithTargetHost(host);
			});
		}

		var options = optionsBuilder.Build();
		options.ProtocolVersion = MqttProtocolVersion.V311;
		if (options.ChannelOptions is MqttClientTcpOptions tcpOptions)
		{
			tcpOptions.DualMode = false;
			tcpOptions.ProtocolType = ProtocolType.Tcp;
		}

		try
		{
			await client.ConnectAsync(options, cancellationToken);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"MQTT connect failed: {DescribeException(ex)}", ex);
		}
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
			throw new InvalidOperationException("Broker host is required.");
		}

		if (settings.Port is < 1 or > 65535)
		{
			throw new InvalidOperationException("Port must be between 1 and 65535.");
		}

		if (string.IsNullOrWhiteSpace(settings.SubscribeTopic))
		{
			throw new InvalidOperationException("Subscribe topic is required.");
		}

		if (string.IsNullOrWhiteSpace(settings.PublishTopic))
		{
			throw new InvalidOperationException("Publish topic is required.");
		}
	}

	private static string DescribeException(Exception exception)
	{
		var parts = new List<string>();
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current is SocketException socketException)
			{
				parts.Add($"{current.GetType().Name}: {current.Message} (SocketError={socketException.SocketErrorCode}, NativeError={socketException.ErrorCode})");
			}
			else
			{
				parts.Add($"{current.GetType().Name}: {current.Message}");
			}
		}

		return string.Join(" -> ", parts);
	}

	private void EnsureConnected()
	{
		if (client?.IsConnected != true)
		{
			throw new InvalidOperationException("MQTT is not connected.");
		}
	}
}
