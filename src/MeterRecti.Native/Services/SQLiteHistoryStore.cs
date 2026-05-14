using MeterRecti.Native.Models;
using SQLite;

namespace MeterRecti.Native.Services;

public sealed class SQLiteHistoryStore : IHistoryStore
{
	private readonly Lazy<SQLiteAsyncConnection> connection;
	private bool initialized;

	public SQLiteHistoryStore()
	{
		connection = new Lazy<SQLiteAsyncConnection>(() =>
		{
			var databasePath = Path.Combine(FileSystem.AppDataDirectory, "meter-recti-history.db3");
			return new SQLiteAsyncConnection(databasePath);
		});
	}

	public async Task<IReadOnlyList<HistoryRecord>> GetRecordsAsync(CancellationToken cancellationToken)
	{
		await InitializeAsync();
		cancellationToken.ThrowIfCancellationRequested();
		return await connection.Value
			.Table<HistoryRecord>()
			.OrderByDescending(record => record.Id)
			.ToListAsync();
	}

	public async Task AddRecordAsync(HistoryRecord record, CancellationToken cancellationToken)
	{
		await InitializeAsync();
		cancellationToken.ThrowIfCancellationRequested();
		await connection.Value.InsertAsync(record);
	}

	public async Task DeleteRecordAsync(long id, CancellationToken cancellationToken)
	{
		await InitializeAsync();
		cancellationToken.ThrowIfCancellationRequested();
		await connection.Value.DeleteAsync<HistoryRecord>(id);
	}

	public async Task ClearAsync(CancellationToken cancellationToken)
	{
		await InitializeAsync();
		cancellationToken.ThrowIfCancellationRequested();
		await connection.Value.DeleteAllAsync<HistoryRecord>();
	}

	private async Task InitializeAsync()
	{
		if (initialized)
		{
			return;
		}

		SQLitePCL.Batteries_V2.Init();
		await connection.Value.CreateTableAsync<HistoryRecord>();
		initialized = true;
	}
}
