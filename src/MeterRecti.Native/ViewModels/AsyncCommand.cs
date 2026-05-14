using System.Windows.Input;

namespace MeterRecti.Native.ViewModels;

public sealed class AsyncCommand : ICommand
{
	private readonly Func<Task> execute;
	private readonly Func<bool>? canExecute;
	private bool isRunning;

	public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
	{
		this.execute = execute;
		this.canExecute = canExecute;
	}

	public event EventHandler? CanExecuteChanged;

	public bool CanExecute(object? parameter)
	{
		return !isRunning && (canExecute?.Invoke() ?? true);
	}

	public async void Execute(object? parameter)
	{
		if (!CanExecute(parameter))
		{
			return;
		}

		try
		{
			isRunning = true;
			RaiseCanExecuteChanged();
			await execute();
		}
		finally
		{
			isRunning = false;
			RaiseCanExecuteChanged();
		}
	}

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}
