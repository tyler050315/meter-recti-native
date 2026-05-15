using System.Windows.Input;
using MeterRecti.Native.Models;
using MeterRecti.Native.Services;

namespace MeterRecti.Native.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
	private const string NetworkNoticeShownKey = "mqtt.networkNoticeShown";
	private readonly IAppSettingsStore settingsStore;
	private readonly IMqttService mqttService;
	private string host = string.Empty;
	private string port = "8883";
	private bool useTls = true;
	private string username = string.Empty;
	private string password = string.Empty;
	private string subscribeTopic = string.Empty;
	private string publishTopic = string.Empty;
	private string statusText = "未连接";
	private Color statusColor = Color.FromArgb("#657684");

	public SettingsViewModel(IAppSettingsStore settingsStore, IMqttService mqttService)
	{
		this.settingsStore = settingsStore;
		this.mqttService = mqttService;
		SaveAndConnectCommand = new AsyncCommand(SaveAndConnectAsync);
		DisconnectCommand = new AsyncCommand(DisconnectAsync);
	}

	public string Host
	{
		get => host;
		set => SetProperty(ref host, value);
	}

	public string Port
	{
		get => port;
		set => SetProperty(ref port, value);
	}

	public bool UseTls
	{
		get => useTls;
		set
		{
			if (SetProperty(ref useTls, value) &&
				(string.IsNullOrWhiteSpace(Port) || Port is "1883" or "8883"))
			{
				Port = value ? "8883" : "1883";
			}
		}
	}

	public string Username
	{
		get => username;
		set => SetProperty(ref username, value);
	}

	public string Password
	{
		get => password;
		set => SetProperty(ref password, value);
	}

	public string SubscribeTopic
	{
		get => subscribeTopic;
		set => SetProperty(ref subscribeTopic, value);
	}

	public string PublishTopic
	{
		get => publishTopic;
		set => SetProperty(ref publishTopic, value);
	}

	public string StatusText
	{
		get => statusText;
		private set => SetProperty(ref statusText, value);
	}

	public Color StatusColor
	{
		get => statusColor;
		private set => SetProperty(ref statusColor, value);
	}

	public string AppIdentity => $"{AppInfo.Current.Name} / {AppInfo.Current.PackageName}";

	public ICommand SaveAndConnectCommand { get; }

	public ICommand DisconnectCommand { get; }

	public void Load()
	{
		var settings = settingsStore.ReadMqttSettings();
		if (settings is null)
		{
			StatusText = "请填写 MQTT 设置";
			StatusColor = Color.FromArgb("#657684");
			return;
		}

		Host = settings.Host;
		Port = settings.Port.ToString();
		UseTls = settings.UseTls;
		Username = settings.Username ?? string.Empty;
		Password = settings.Password ?? string.Empty;
		SubscribeTopic = settings.SubscribeTopic;
		PublishTopic = settings.PublishTopic;
		StatusText = "已读取本地设置";
		StatusColor = Color.FromArgb("#657684");
	}

	private async Task SaveAndConnectAsync()
	{
		try
		{
			var settings = CreateSettingsFromInput();
			await ShowNetworkPermissionNoticeAsync();
			settingsStore.SaveMqttSettings(settings);
			StatusText = "正在连接...";
			StatusColor = Color.FromArgb("#657684");
			await mqttService.ConnectAsync(settings, CancellationToken.None);
			StatusText = "已连接";
			StatusColor = Color.FromArgb("#2D9B75");
		}
		catch (Exception ex)
		{
			StatusText = ex.Message;
			StatusColor = Color.FromArgb("#C6514A");
		}
	}

	private static async Task ShowNetworkPermissionNoticeAsync()
	{
		if (Preferences.Default.Get(NetworkNoticeShownKey, false))
		{
			return;
		}

		await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			await Shell.Current.DisplayAlert(
				"需要网络权限",
				"首次连接 MQTT 时，请允许 Meter Recti Native 使用网络。如果连接失败，请到 iOS 设置 -> Meter Recti Native，打开“无线数据 / WLAN 与蜂窝网络”。",
				"我知道了");
		});

		Preferences.Default.Set(NetworkNoticeShownKey, true);
	}

	private async Task DisconnectAsync()
	{
		await mqttService.DisconnectAsync(CancellationToken.None);
		StatusText = "已断开";
		StatusColor = Color.FromArgb("#657684");
	}

	private MqttSettings CreateSettingsFromInput()
	{
		if (!int.TryParse(Port, out var parsedPort))
		{
			throw new InvalidOperationException("端口必须是数字。");
		}

		return new MqttSettings(
			Host.Trim(),
			parsedPort,
			UseTls,
			NullIfWhiteSpace(Username),
			NullIfWhiteSpace(Password),
			SubscribeTopic.Trim(),
			PublishTopic.Trim(),
			PreferencesAppSettingsStore.CreateClientId());
	}

	private static string? NullIfWhiteSpace(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
