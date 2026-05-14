using MeterRecti.Native.Pages;
using MeterRecti.Native.Services;
using MeterRecti.Native.ViewModels;
using Microsoft.Extensions.Logging;
#if IOS
using MeterRecti.Native.Platforms.iOS;
#endif

namespace MeterRecti.Native;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IAppSettingsStore, PreferencesAppSettingsStore>();
		builder.Services.AddSingleton<IMqttService, MqttService>();
		builder.Services.AddSingleton<IHistoryStore, SQLiteHistoryStore>();
		builder.Services.AddSingleton<ICsvExportService, CsvExportService>();
		builder.Services.AddSingleton<IShareService, ShareService>();
#if IOS
		builder.Services.AddSingleton<IScannerService, IosScannerService>();
#endif
		builder.Services.AddSingleton<CalibrationViewModel>();
		builder.Services.AddSingleton<SettingsViewModel>();
		builder.Services.AddSingleton<HistoryViewModel>();
		builder.Services.AddSingleton<CalibrationPage>();
		builder.Services.AddSingleton<SettingsPage>();
		builder.Services.AddSingleton<HistoryPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
