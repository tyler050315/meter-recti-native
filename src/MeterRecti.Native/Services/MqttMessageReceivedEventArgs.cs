namespace MeterRecti.Native.Services;

public sealed class MqttMessageReceivedEventArgs : EventArgs
{
	public MqttMessageReceivedEventArgs(string topic, string payload, bool retain)
	{
		Topic = topic;
		Payload = payload;
		Retain = retain;
	}

	public string Topic { get; }

	public string Payload { get; }

	public bool Retain { get; }
}
