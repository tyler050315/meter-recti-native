using System.Collections.ObjectModel;
using System.Windows.Input;
using MeterRecti.Native.Models;
using MeterRecti.Native.Services;

namespace MeterRecti.Native.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
	private readonly IHistoryStore historyStore;
	private readonly ICsvExportService csvExportService;
	private readonly IShareService shareService;
	private string statusText = "暂无历史记录";
	private Color statusColor = Color.FromArgb("#657684");

	public HistoryViewModel(
		IHistoryStore historyStore,
		ICsvExportService csvExportService,
		IShareService shareService)
	{
		this.historyStore = historyStore;
		this.csvExportService = csvExportService;
		this.shareService = shareService;
		Records = [];
		RefreshCommand = new AsyncCommand(RefreshAsync);
		ExportCommand = new AsyncCommand(ExportAsync);
		ClearCommand = new AsyncCommand(ClearAsync);
		DeleteRecordCommand = new Command<HistoryRecord>(async record => await DeleteRecordAsync(record));
	}

	public ObservableCollection<HistoryRecordItemViewModel> Records { get; }

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

	public ICommand RefreshCommand { get; }

	public ICommand ExportCommand { get; }

	public ICommand ClearCommand { get; }

	public ICommand DeleteRecordCommand { get; }

	public async Task RefreshAsync()
	{
		var records = await historyStore.GetRecordsAsync(CancellationToken.None);
		Records.Clear();
		foreach (var record in records)
		{
			Records.Add(new HistoryRecordItemViewModel(record, DeleteRecordCommand));
		}

		SetStatus(records.Count == 0 ? "暂无历史记录" : $"共 {records.Count} 条记录。", "#657684");
	}

	private async Task ExportAsync()
	{
		var records = await historyStore.GetRecordsAsync(CancellationToken.None);
		if (records.Count == 0)
		{
			SetStatus("暂无可导出的历史记录。", "#657684");
			return;
		}

		var csv = csvExportService.CreateHistoryCsv(records);
		var fileName = $"meter-recti-history-{DateTimeOffset.Now:yyyy-MM-dd}.csv";
		await shareService.ShareTextFileAsync(fileName, csv, "text/csv", CancellationToken.None);
		SetStatus("历史记录已打开分享面板。", "#2D9B75");
	}

	private async Task ClearAsync()
	{
		var confirm = await Shell.Current.DisplayAlert("清空历史", "确定要清空全部历史记录吗？", "清空", "取消");
		if (!confirm)
		{
			return;
		}

		await historyStore.ClearAsync(CancellationToken.None);
		await RefreshAsync();
		SetStatus("历史记录已清空。", "#657684");
	}

	private async Task DeleteRecordAsync(HistoryRecord? record)
	{
		if (record is null)
		{
			return;
		}

		var confirm = await Shell.Current.DisplayAlert(
			"删除记录",
			$"确定要删除 SN {record.SerialNumber} 的记录吗？",
			"删除",
			"取消");

		if (!confirm)
		{
			return;
		}

		await historyStore.DeleteRecordAsync(record.Id, CancellationToken.None);
		await RefreshAsync();
		SetStatus("历史记录已删除。", "#657684");
	}

	private void SetStatus(string text, string color)
	{
		StatusText = text;
		StatusColor = Color.FromArgb(color);
	}
}
