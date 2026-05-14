namespace MeterRecti.Native.Services;

public interface IShareService
{
	Task ShareTextFileAsync(string fileName, string content, string contentType, CancellationToken cancellationToken);
}
