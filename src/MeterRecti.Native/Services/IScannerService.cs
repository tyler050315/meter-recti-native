namespace MeterRecti.Native.Services;

public interface IScannerService
{
	Task<string?> ScanAsync(CancellationToken cancellationToken);
}
