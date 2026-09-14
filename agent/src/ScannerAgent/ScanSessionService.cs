using System.Collections.Concurrent;
using System.IO.Compression;

public sealed class ScanSessionService(IScannerBackend backend, ILogger<ScanSessionService> logger)
{
    private readonly ConcurrentDictionary<string, ScanSessionState> _sessions = new();
    private readonly SemaphoreSlim _scannerLock = new(1, 1);
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "ScannerAgent");

    public ScanSessionDto Start(StartScanRequest request)
    {
        var id = Guid.NewGuid().ToString("N");
        var state = new ScanSessionState(id, Path.Combine(_rootDirectory, id));
        _sessions[id] = state;
        _ = RunAsync(state, request);
        return state.Snapshot();
    }

    public ScanSessionDto? Get(string id) =>
        _sessions.TryGetValue(id, out var state) ? state.Snapshot() : null;

    public bool Cancel(string id)
    {
        if (!_sessions.TryGetValue(id, out var state)) return false;
        state.Cancel();
        return true;
    }

    public string? GetPagePath(string sessionId, string pageId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) ||
            !state.HasPage(pageId)) return null;

        var path = Path.Combine(state.OutputDirectory, $"{pageId}.png");
        return File.Exists(path) ? path : null;
    }

    public async Task<(string Path, string ContentType, string DownloadName)?> GetExportAsync(
        string sessionId,
        string format,
        CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) || state.Status != "completed") return null;

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdfPath = Path.Combine(state.OutputDirectory, "scan.pdf");
            return File.Exists(pdfPath) ? (pdfPath, "application/pdf", $"scan-{sessionId}.pdf") : null;
        }

        if (!format.Equals("images", StringComparison.OrdinalIgnoreCase)) return null;

        var zipPath = Path.Combine(state.OutputDirectory, "images.zip");
        if (!File.Exists(zipPath))
        {
            await using var stream = File.Create(zipPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            foreach (var page in state.Snapshot().Pages)
            {
                var pagePath = Path.Combine(state.OutputDirectory, $"{page.Id}.png");
                archive.CreateEntryFromFile(pagePath, $"pagina-{page.Number:D3}.png");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return (zipPath, "application/zip", $"scan-{sessionId}.zip");
    }

    private async Task RunAsync(ScanSessionState state, StartScanRequest request)
    {
        try
        {
            Directory.CreateDirectory(state.OutputDirectory);
            await _scannerLock.WaitAsync(state.CancellationToken);
            try
            {
                state.SetStatus("scanning");
                await backend.ScanAsync(request, state.OutputDirectory, state.AddPage, state.CancellationToken);
                state.SetStatus("completed");
            }
            finally
            {
                _scannerLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            state.SetStatus("cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scan {ScanId} failed", state.Id);
            state.Fail(exception.Message);
        }
    }
}

internal sealed class ScanSessionState(string id, string outputDirectory)
{
    private readonly Lock _sync = new();
    private readonly List<ScanPageDto> _pages = [];
    private readonly CancellationTokenSource _cancellation = new();
    private string _status = "pending";
    private string? _error;

    public string Id { get; } = id;
    public string OutputDirectory { get; } = outputDirectory;
    public CancellationToken CancellationToken => _cancellation.Token;
    public string Status { get { lock (_sync) return _status; } }

    public void AddPage(ScanPageDto page)
    {
        lock (_sync) _pages.Add(page);
    }

    public bool HasPage(string pageId)
    {
        lock (_sync) return _pages.Any(page => page.Id == pageId);
    }

    public void SetStatus(string status)
    {
        lock (_sync) _status = status;
    }

    public void Fail(string error)
    {
        lock (_sync)
        {
            _status = "failed";
            _error = error;
        }
    }

    public void Cancel() => _cancellation.Cancel();

    public ScanSessionDto Snapshot()
    {
        lock (_sync) return new ScanSessionDto(Id, _status, [.. _pages], _error);
    }
}