using System.Text.Json;
using System.Windows.Input;
using MeterRecti.Native.Models;
using MeterRecti.Native.Services;

namespace MeterRecti.Native.ViewModels;

public sealed class CalibrationViewModel : ObservableObject
{
	private readonly IAppSettingsStore settingsStore;
	private readonly IMqttService mqttService;
	private readonly IHistoryStore historyStore;
	private readonly IScannerService scannerService;
	private CancellationTokenSource? timeoutSource;
	private MqttSettings? settings;
	private string? activeSubscribeTopic;
	private string serialNumber = string.Empty;
	private string meterReading = string.Empty;
	private string statusText = "请先连接 MQTT。";
	private Color statusColor = Color.FromArgb("#657684");
	private bool isBusy;

	public CalibrationViewModel(
		IAppSettingsStore settingsStore,
		IMqttService mqttService,
		IHistoryStore historyStore,
		IScannerService scannerService)
	{
		this.settingsStore = settingsStore;
		this.mqttService = mqttService;
		this.historyStore = historyStore;
		this.scannerService = scannerService;
		this.mqttService.MessageReceived += OnMessageReceived;
		StartCalibrationCommand = new AsyncCommand(StartCalibrationAsync, () => !IsBusy);
		ScanCommand = new AsyncCommand(ScanAsync, () => !IsBusy);
	}

	public string SerialNumber
	{
		get => serialNumber;
		set => SetProperty(ref serialNumber, value);
	}

	public string MeterReading
	{
		get => meterReading;
		set => SetProperty(ref meterReading, value);
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

	public bool IsBusy
	{
		get => isBusy;
		private set
		{
			if (SetProperty(ref isBusy, value))
			{
				(StartCalibrationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
				(ScanCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			}
		}
	}

	public ICommand StartCalibrationCommand { get; }

	public ICommand ScanCommand { get; }

	public void RefreshStatus()
	{
		settings = settingsStore.ReadMqttSettings();
		if (mqttService.IsConnected)
		{
			SetStatus("MQTT 已连接，可开始校准。", "#2D9B75");
		}
		else if (settings is null)
		{
			SetStatus("请先在 MQTT 页填写并保存连接设置。", "#657684");
		}
		else
		{
			SetStatus("MQTT 未连接，请先到 MQTT 页连接。", "#C6514A");
		}
	}

	private async Task StartCalibrationAsync()
	{
		try
		{
			settings = settingsStore.ReadMqttSettings();
			if (settings is null)
			{
				throw new InvalidOperationException("请先保存 MQTT 设置。");
			}

			if (!mqttService.IsConnected)
			{
				throw new InvalidOperationException("MQTT 未连接。");
			}

			CalibrationProtocol.ValidateInputs(SerialNumber, MeterReading);
			IsBusy = true;
			activeSubscribeTopic = CalibrationProtocol.AppendSerialTopic(settings.SubscribeTopic, SerialNumber);
			await mqttService.SubscribeAsync(activeSubscribeTopic, CancellationToken.None);
			SetStatus("等待设备 M0 回包...", "#657684");
			StartTimeout();
		}
		catch (Exception ex)
		{
			IsBusy = false;
			SetStatus(ex.Message, "#C6514A");
		}
	}

	private async Task ScanAsync()
	{
		try
		{
			var result = await scannerService.ScanAsync(CancellationToken.None);
			if (!string.IsNullOrWhiteSpace(result))
			{
				SerialNumber = result.Trim();
				SetStatus("扫码成功，SN 已填入。", "#2D9B75");
			}
			else
			{
				SetStatus("已取消扫码。", "#657684");
			}
		}
		catch (Exception ex)
		{
			SetStatus(ex.Message, "#C6514A");
		}
	}

	private async void OnMessageReceived(object? sender, MqttMessageReceivedEventArgs args)
	{
		if (!IsBusy ||
			settings is null ||
			activeSubscribeTopic is null ||
			!string.Equals(args.Topic, activeSubscribeTopic, StringComparison.Ordinal) ||
			args.Retain)
		{
			return;
		}

		try
		{
			if (CalibrationProtocol.IsExpectedM0(args.Payload, SerialNumber))
			{
				var publishTopic = CalibrationProtocol.AppendSerialTopic(settings.PublishTopic, SerialNumber);
				var command = CalibrationProtocol.BuildCommand(SerialNumber, MeterReading);
				SetStatus("收到 M0，正在发送校准命令...", "#657684");
				await mqttService.PublishAsync(publishTopic, command, CancellationToken.None);
				SetStatus("命令已发送，等待 M1 结果...", "#657684");
				StartTimeout();
				return;
			}

			var result = CalibrationProtocol.TryReadM1Result(args.Payload, SerialNumber);
			if (result is not null)
			{
				await CompleteCalibrationAsync(result);
			}
		}
		catch (JsonException)
		{
			SetStatus("收到非 JSON MQTT 消息，已忽略。", "#657684");
		}
		catch (Exception ex)
		{
			await StopCalibrationAsync();
			SetStatus(ex.Message, "#C6514A");
		}
	}

	private async Task CompleteCalibrationAsync(CalibrationResult result)
	{
		var now = DateTimeOffset.Now;
		var publishTopic = settings is null ? string.Empty : CalibrationProtocol.AppendSerialTopic(settings.PublishTopic, result.SerialNumber);
		var subscribeTopic = activeSubscribeTopic ?? string.Empty;
		await historyStore.AddRecordAsync(new HistoryRecord
		{
			SerialNumber = result.SerialNumber,
			MeterReading = MeterReading,
			MeterSum = result.MeterSum,
			CalibratedAtLocal = now,
			CalibratedAtUtc = now.ToUniversalTime(),
			RawPayload = result.RawPayload,
			SubscribeTopic = subscribeTopic,
			PublishTopic = publishTopic
		}, CancellationToken.None);

		await StopCalibrationAsync();
		SerialNumber = string.Empty;
		MeterReading = string.Empty;
		SetStatus($"校准成功，METERSUM: {result.MeterSum}", "#2D9B75");
	}

	private void StartTimeout()
	{
		timeoutSource?.Cancel();
		timeoutSource = new CancellationTokenSource();
		var token = timeoutSource.Token;

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(CalibrationProtocol.Timeout, token);
				if (!token.IsCancellationRequested)
				{
					await StopCalibrationAsync();
					MainThread.BeginInvokeOnMainThread(() =>
						SetStatus("校准等待超时，请检查设备 MQTT 回包。", "#C6514A"));
				}
			}
			catch (TaskCanceledException)
			{
			}
		}, token);
	}

	private async Task StopCalibrationAsync()
	{
		timeoutSource?.Cancel();
		timeoutSource = null;

		if (activeSubscribeTopic is not null)
		{
			await mqttService.UnsubscribeAsync(activeSubscribeTopic, CancellationToken.None);
			activeSubscribeTopic = null;
		}

		IsBusy = false;
	}

	private void SetStatus(string text, string color)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			StatusText = text;
			StatusColor = Color.FromArgb(color);
		});
	}
}
