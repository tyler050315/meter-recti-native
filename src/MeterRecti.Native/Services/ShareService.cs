namespace MeterRecti.Native.Services;

public sealed class ShareService : IShareService
{
	public async Task ShareTextFileAsync(string fileName, string content, string contentType, CancellationToken cancellationToken)
	{
		var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
		await File.WriteAllTextAsync(filePath, content, cancellationToken);

		await Share.Default.RequestAsync(new ShareFileRequest
		{
			Title = fileName,
			File = new ShareFile(filePath, contentType)
		});
	}
}
