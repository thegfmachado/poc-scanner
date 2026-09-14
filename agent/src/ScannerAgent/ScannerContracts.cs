public sealed record ScannerDeviceDto(string Id, string Name, string Driver);

public sealed record StartScanRequest(
    string DeviceId,
    string Driver,
    string Mode,
    string PaperSource,
    string ColorMode,
    int Dpi);

public sealed record ScanPageDto(
    string Id,
    int Number,
    string ContentType,
    int Width,
    int Height);

public sealed record ScanSessionDto(
    string Id,
    string Status,
    IReadOnlyList<ScanPageDto> Pages,
    string? Error = null);

public interface IScannerBackend
{
    Task<IReadOnlyList<ScannerDeviceDto>> ListDevicesAsync(string driver, CancellationToken cancellationToken);

    Task ScanAsync(
        StartScanRequest request,
        string outputDirectory,
        Action<ScanPageDto> onPage,
        CancellationToken cancellationToken);
}