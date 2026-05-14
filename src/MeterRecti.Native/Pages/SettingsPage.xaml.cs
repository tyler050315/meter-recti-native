namespace MeterRecti.Native.Pages;

public partial class SettingsPage : ContentPage
{
	private readonly ViewModels.SettingsViewModel viewModel;

	public SettingsPage()
	{
		InitializeComponent();
		viewModel = IPlatformApplication.Current!.Services.GetRequiredService<ViewModels.SettingsViewModel>();
		BindingContext = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		viewModel.Load();
	}
}
