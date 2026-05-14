using MeterRecti.Native.Models;

namespace MeterRecti.Native.Services;

public sealed class PreferencesAppSettingsStore : IAppSettingsStore
{
	private const string HostKey = "mqtt.host";
	private const string PortKey = "mqtt.port";
	private const string UseTlsKey = "mqtt.useTls";
	private const string UsernameKey = "mqtt.username";
	private const string PasswordKey = "mqtt.password";
	private const string SubscribeTopicKey = "mqtt.subscribeTopic";
	private const string PublishTopicKey = "mqtt.publishTopic";
	private const string ClientIdKey = "mqtt.clientId";

	public MqttSettings? ReadMqttSettings()
	{
		var host = Preferences.Default.Get(HostKey, string.Empty);
		var subscribeTopic = Preferences.Default.Get(SubscribeTopicKey, string.Empty);
		var publishTopic = Preferences.Default.Get(PublishTopicKey, string.Empty);

		if (string.IsNullOrWhiteSpace(host) ||
			string.IsNullOrWhiteSpace(subscribeTopic) ||
			string.IsNullOrWhiteSpace(publishTopic))
		{
			return null;
		}

		var useTls = Preferences.Default.Get(UseTlsKey, true);
		var port = Preferences.Default.Get(PortKey, useTls ? 8883 : 1883);
		var clientId = Preferences.Default.Get(ClientIdKey, CreateClientId());

		return new MqttSettings(
			host.Trim(),
			port,
			useTls,
			NullIfWhiteSpace(Preferences.Default.Get(UsernameKey, string.Empty)),
			NullIfWhiteSpace(Preferences.Default.Get(PasswordKey, string.Empty)),
			subscribeTopic.Trim(),
			publishTopic.Trim(),
			clientId);
	}

	public void SaveMqttSettings(MqttSettings settings)
	{
		Preferences.Default.Set(HostKey, settings.Host.Trim());
		Preferences.Default.Set(PortKey, settings.Port);
		Preferences.Default.Set(UseTlsKey, settings.UseTls);
		Preferences.Default.Set(UsernameKey, settings.Username ?? string.Empty);
		Preferences.Default.Set(PasswordKey, settings.Password ?? string.Empty);
		Preferences.Default.Set(SubscribeTopicKey, settings.SubscribeTopic.Trim());
		Preferences.Default.Set(PublishTopicKey, settings.PublishTopic.Trim());
		Preferences.Default.Set(ClientIdKey, settings.ClientId);
	}

	public static string CreateClientId()
	{
		return $"meter-recti-{Guid.NewGuid():N}";
	}

	private static string? NullIfWhiteSpace(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
