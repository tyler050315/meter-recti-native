namespace MeterRecti.Native;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Dispatcher.Dispatch(async () => await GoToAsync("//calibration"));
	}
}
