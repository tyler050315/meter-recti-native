using System.Windows.Input;
using MeterRecti.Native.Models;

namespace MeterRecti.Native.ViewModels;

public sealed class HistoryRecordItemViewModel
{
	public HistoryRecordItemViewModel(HistoryRecord record, ICommand deleteCommand)
	{
		Record = record;
		DeleteCommand = deleteCommand;
	}

	public HistoryRecord Record { get; }

	public string SerialNumber => Record.SerialNumber;

	public string MeterSum => Record.MeterSum;

	public DateTimeOffset CalibratedAtLocal => Record.CalibratedAtLocal;

	public ICommand DeleteCommand { get; }
}
