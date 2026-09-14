using NAPS2.Images;
using NAPS2.Images.Gdi;
using NAPS2.Pdf;
using NAPS2.Scan;

public sealed class Naps2ScannerBackend : IScannerBackend
{
    public async Task<IReadOnlyList<ScannerDeviceDto>> ListDevicesAsync(
        string driver,
        CancellationToken cancellationToken)
    {
        using var context = CreateContext();
        var controller = new ScanController(context);
        var naps2Driver = ParseDriver(driver);
        var devices = new List<ScannerDeviceDto>();

        await foreach (var device in controller.GetDevices(naps2Driver, cancellationToken))
        {
            devices.Add(new ScannerDeviceDto(device.ID, device.Name, driver.ToLowerInvariant()));
        }

        return devices;
    }

    public async Task ScanAsync(
        StartScanRequest request,
        string outputDirectory,
        Action<ScanPageDto> onPage,
        CancellationToken cancellationToken)
    {
        using var context = CreateContext();
        var controller = new ScanController(context);
        var driver = ParseDriver(request.Driver);
        var devices = await controller.GetDeviceList(driver);
        var device = devices.FirstOrDefault(candidate => candidate.ID == request.DeviceId)
            ?? throw new InvalidOperationException("Scanner selecionado nao foi encontrado.");
        var options = new NAPS2.Scan.ScanOptions
        {
            Device = device,
            Driver = driver,
            UseNativeUI = request.Mode.Equals("native-ui", StringComparison.OrdinalIgnoreCase),
            PaperSource = ParsePaperSource(request.PaperSource),
            BitDepth = ParseBitDepth(request.ColorMode),
            Dpi = request.Dpi
        };

        var images = new List<ProcessedImage>();
        try
        {
            await foreach (var image in controller.Scan(options, cancellationToken))
            {
                images.Add(image);
                var pageNumber = images.Count;
                var pageId = $"page-{pageNumber:D3}";
                image.Save(Path.Combine(outputDirectory, $"{pageId}.png"), ImageFileFormat.Png);
                using var rendered = image.Render();
                onPage(new ScanPageDto(
                    pageId,
                    pageNumber,
                    "image/png",
                    rendered.Width,
                    rendered.Height));
            }

            if (images.Count == 0) throw new InvalidOperationException("O scanner nao retornou paginas.");
            await new PdfExporter(context).Export(Path.Combine(outputDirectory, "scan.pdf"), images);
        }
        finally
        {
            foreach (var image in images) image.Dispose();
        }
    }

    private static ScanningContext CreateContext()
    {
        var context = new ScanningContext(new GdiImageContext());
        context.SetUpWin32Worker();
        return context;
    }

    private static Driver ParseDriver(string driver) => driver.ToLowerInvariant() switch
    {
        "twain" => Driver.Twain,
        "wia" => Driver.Wia,
        _ => throw new ArgumentException("Driver deve ser twain ou wia.")
    };

    private static PaperSource ParsePaperSource(string source) => source.ToLowerInvariant() switch
    {
        "flatbed" => PaperSource.Flatbed,
        "feeder" => PaperSource.Feeder,
        "duplex" => PaperSource.Duplex,
        _ => throw new ArgumentException("Origem de papel invalida.")
    };

    private static BitDepth ParseBitDepth(string colorMode) => colorMode.ToLowerInvariant() switch
    {
        "color" => BitDepth.Color,
        "grayscale" => BitDepth.Grayscale,
        "black-white" => BitDepth.BlackAndWhite,
        _ => throw new ArgumentException("Modo de cor invalido.")
    };
}