namespace MeterRecti.Native.Pages;

public partial class HistoryPage : ContentPage
{
	private readonly ViewModels.HistoryViewModel viewModel;

	public HistoryPage()
	{
		InitializeComponent();
		viewModel = IPlatformApplication.Current!.Services.GetRequiredService<ViewModels.HistoryViewModel>();
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await viewModel.RefreshAsync();
	}
}
