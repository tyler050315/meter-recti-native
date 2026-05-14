namespace MeterRecti.Native.Models;

public sealed record MqttSettings(
	string Host,
	int Port,
	bool UseTls,
	string? Username,
	string? Password,
	string SubscribeTopic,
	string PublishTopic,
	string ClientId);
