using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public sealed class MqttService : IMqttService
{
	private const ushort KeepAliveSeconds = 60;
	private readonly SemaphoreSlim writeLock = new(1, 1);
	private TcpClient? tcpClient;
	private Stream? stream;
	private CancellationTokenSource? connectionCancellation;
	private Task? readTask;
	private Task? pingTask;
	private ushort nextPacketId;
	private bool connected;

	public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

	public bool IsConnected => connected && tcpClient?.Connected == true && stream is not null;

	public async Task ConnectAsync(MqttSettings settings, CancellationToken cancellationToken)
	{
		Validate(settings);
		await DisconnectAsync(CancellationToken.None);

		var host = settings.Host.Trim();
		connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var token = connectionCancellation.Token;

		try
		{
			tcpClient = new TcpClient(AddressFamily.InterNetwork)
			{
				NoDelay = true
			};

			var address = await ResolveIPv4AddressAsync(host, token);
			await tcpClient.ConnectAsync(address, settings.Port, token);

			stream = tcpClient.GetStream();
			if (settings.UseTls)
			{
				var sslStream = new SslStream(stream, false);
				await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
				{
					TargetHost = host,
					EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
				}, token);
				stream = sslStream;
			}

			await SendConnectPacketAsync(settings, token);
			await ReadConnAckAsync(token);
			connected = true;
			readTask = Task.Run(() => ReadLoopAsync(connectionCancellation.Token), CancellationToken.None);
			pingTask = Task.Run(() => PingLoopAsync(connectionCancellation.Token), CancellationToken.None);
		}
		catch (Exception ex)
		{
			await DisconnectAsync(CancellationToken.None);
			throw new InvalidOperationException($"MQTT connect failed: {DescribeException(ex)}", ex);
		}
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken)
	{
		var wasConnected = connected;
		connected = false;

		if (wasConnected && stream is not null)
		{
			try
			{
				await WritePacketAsync(0xE0, [], cancellationToken);
			}
			catch
			{
			}
		}

		connectionCancellation?.Cancel();
		connectionCancellation?.Dispose();
		connectionCancellation = null;

		stream?.Dispose();
		stream = null;
		tcpClient?.Dispose();
		tcpClient = null;
	}

	public async Task SubscribeAsync(string topic, CancellationToken cancellationToken)
	{
		EnsureConnected();
		var packetId = NextPacketId();
		using var body = new MemoryStream();
		WriteUInt16(body, packetId);
		WriteUtf8(body, topic);
		body.WriteByte(0);
		await WritePacketAsync(0x82, body.ToArray(), cancellationToken);
	}

	public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken)
	{
		if (!IsConnected)
		{
			return;
		}

		var packetId = NextPacketId();
		using var body = new MemoryStream();
		WriteUInt16(body, packetId);
		WriteUtf8(body, topic);
		await WritePacketAsync(0xA2, body.ToArray(), cancellationToken);
	}

	public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
	{
		EnsureConnected();
		using var body = new MemoryStream();
		WriteUtf8(body, topic);
		var payloadBytes = Encoding.UTF8.GetBytes(payload);
		body.Write(payloadBytes);
		await WritePacketAsync(0x30, body.ToArray(), cancellationToken);
	}

	private static async Task<IPAddress> ResolveIPv4AddressAsync(string host, CancellationToken cancellationToken)
	{
		if (IPAddress.TryParse(host, out var parsed))
		{
			if (parsed.AddressFamily == AddressFamily.InterNetwork)
			{
				return parsed;
			}

			throw new InvalidOperationException("Only IPv4 MQTT hosts are supported in this build.");
		}

		var addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancellationToken);
		return addresses.FirstOrDefault() ?? throw new InvalidOperationException($"No IPv4 address found for {host}.");
	}

	private async Task SendConnectPacketAsync(MqttSettings settings, CancellationToken cancellationToken)
	{
		using var body = new MemoryStream();
		WriteUtf8(body, "MQTT");
		body.WriteByte(4);

		var flags = 0x02;
		if (!string.IsNullOrWhiteSpace(settings.Password))
		{
			flags |= 0x40;
		}

		if (!string.IsNullOrWhiteSpace(settings.Username))
		{
			flags |= 0x80;
		}

		body.WriteByte((byte)flags);
		WriteUInt16(body, KeepAliveSeconds);
		WriteUtf8(body, settings.ClientId);

		if (!string.IsNullOrWhiteSpace(settings.Username))
		{
			WriteUtf8(body, settings.Username);
		}

		if (!string.IsNullOrWhiteSpace(settings.Password))
		{
			WriteUtf8(body, settings.Password);
		}

		await WritePacketAsync(0x10, body.ToArray(), cancellationToken);
	}

	private async Task ReadConnAckAsync(CancellationToken cancellationToken)
	{
		var packet = await ReadPacketAsync(cancellationToken);
		if (packet.PacketType != 2 || packet.Payload.Length < 2)
		{
			throw new InvalidOperationException($"Unexpected MQTT CONNACK packet type {packet.PacketType}.");
		}

		var returnCode = packet.Payload[1];
		if (returnCode != 0)
		{
			throw new InvalidOperationException($"MQTT broker rejected connection, CONNACK return code {returnCode}.");
		}
	}

	private async Task ReadLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var packet = await ReadPacketAsync(cancellationToken);
				if (packet.PacketType == 3)
				{
					await HandlePublishAsync(packet, cancellationToken);
				}
				else if (packet.PacketType == 14)
				{
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
			connected = false;
		}
	}

	private async Task PingLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(KeepAliveSeconds / 2), cancellationToken);
				if (IsConnected)
				{
					await WritePacketAsync(0xC0, [], cancellationToken);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
			connected = false;
		}
	}

	private async Task HandlePublishAsync(MqttPacket packet, CancellationToken cancellationToken)
	{
		var payload = packet.Payload;
		if (payload.Length < 2)
		{
			return;
		}

		var topicLength = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
		var offset = 2 + topicLength;
		if (topicLength == 0 || offset > payload.Length)
		{
			return;
		}

		var topic = Encoding.UTF8.GetString(payload, 2, topicLength);
		var qos = (packet.Header >> 1) & 0x03;
		if (qos > 0)
		{
			if (offset + 2 > payload.Length)
			{
				return;
			}

			var packetId = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset, 2));
			offset += 2;
			if (qos == 1)
			{
				await WritePacketAsync(0x40, [(byte)(packetId >> 8), (byte)(packetId & 0xFF)], cancellationToken);
			}
		}

		var message = Encoding.UTF8.GetString(payload, offset, payload.Length - offset);
		var retain = (packet.Header & 0x01) == 0x01;
		MessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs(topic, message, retain));
	}

	private async Task<MqttPacket> ReadPacketAsync(CancellationToken cancellationToken)
	{
		var activeStream = stream ?? throw new InvalidOperationException("MQTT stream is not open.");
		var headerBuffer = new byte[1];
		await activeStream.ReadExactlyAsync(headerBuffer, cancellationToken);

		var remainingLength = await ReadRemainingLengthAsync(activeStream, cancellationToken);
		var payload = new byte[remainingLength];
		if (remainingLength > 0)
		{
			await activeStream.ReadExactlyAsync(payload, cancellationToken);
		}

		return new MqttPacket(headerBuffer[0], headerBuffer[0] >> 4, payload);
	}

	private async Task WritePacketAsync(byte header, byte[] payload, CancellationToken cancellationToken)
	{
		var activeStream = stream ?? throw new InvalidOperationException("MQTT stream is not open.");
		var length = EncodeRemainingLength(payload.Length);
		var packet = new byte[1 + length.Length + payload.Length];
		packet[0] = header;
		length.CopyTo(packet, 1);
		payload.CopyTo(packet, 1 + length.Length);

		await writeLock.WaitAsync(cancellationToken);
		try
		{
			await activeStream.WriteAsync(packet, cancellationToken);
			await activeStream.FlushAsync(cancellationToken);
		}
		finally
		{
			writeLock.Release();
		}
	}

	private static async Task<int> ReadRemainingLengthAsync(Stream activeStream, CancellationToken cancellationToken)
	{
		var multiplier = 1;
		var value = 0;
		var buffer = new byte[1];

		do
		{
			await activeStream.ReadExactlyAsync(buffer, cancellationToken);
			value += (buffer[0] & 127) * multiplier;
			if (multiplier > 128 * 128 * 128)
			{
				throw new InvalidOperationException("Malformed MQTT remaining length.");
			}

			multiplier *= 128;
		}
		while ((buffer[0] & 128) != 0);

		return value;
	}

	private static byte[] EncodeRemainingLength(int length)
	{
		var bytes = new List<byte>();
		do
		{
			var encoded = length % 128;
			length /= 128;
			if (length > 0)
			{
				encoded |= 128;
			}

			bytes.Add((byte)encoded);
		}
		while (length > 0);

		return bytes.ToArray();
	}

	private ushort NextPacketId()
	{
		nextPacketId++;
		if (nextPacketId == 0)
		{
			nextPacketId = 1;
		}

		return nextPacketId;
	}

	private static void WriteUtf8(Stream target, string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		WriteUInt16(target, (ushort)bytes.Length);
		target.Write(bytes);
	}

	private static void WriteUInt16(Stream target, ushort value)
	{
		target.WriteByte((byte)(value >> 8));
		target.WriteByte((byte)(value & 0xFF));
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
		if (!IsConnected)
		{
			throw new InvalidOperationException("MQTT is not connected.");
		}
	}

	private sealed record MqttPacket(byte Header, int PacketType, byte[] Payload);
}
