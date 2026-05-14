namespace MeterRecti.Native.Pages;

public partial class CalibrationPage : ContentPage
{
	private readonly ViewModels.CalibrationViewModel viewModel;

	public CalibrationPage()
	{
		InitializeComponent();
		viewModel = IPlatformApplication.Current!.Services.GetRequiredService<ViewModels.CalibrationViewModel>();
		BindingContext = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		viewModel.RefreshStatus();
	}
}
