using Microsoft.Extensions.Logging.Abstractions;

namespace ScannerAgent.Tests;

public class PairingServiceTests
{
    [Fact]
    public void Pairing_IssuesTokenOnlyForDisplayedCode()
    {
        var pairing = new PairingService();

        Assert.False(pairing.TryPair("invalid", out _));
        Assert.True(pairing.TryPair(pairing.Code, out var token));
        Assert.NotNull(token);
        Assert.True(pairing.IsAuthorized(token!));
        Assert.False(pairing.IsAuthorized("invalid"));
    }
}

public class ScanSessionServiceTests
{
    [Fact]
    public async Task Start_CompletesAndPublishesPages()
    {
        var service = CreateService(new FakeScannerBackend());

        var started = service.Start(Request());
        var completed = await WaitForTerminalState(service, started.Id);

        Assert.Equal("completed", completed.Status);
        Assert.Single(completed.Pages);
        Assert.NotNull(service.GetPagePath(completed.Id, completed.Pages[0].Id));
    }

    [Fact]
    public async Task Cancel_MarksActiveSessionAsCancelled()
    {
        var backend = new FakeScannerBackend(waitUntilCancelled: true);
        var service = CreateService(backend);
        var started = service.Start(Request());
        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(service.Cancel(started.Id));
        var cancelled = await WaitForTerminalState(service, started.Id);

        Assert.Equal("cancelled", cancelled.Status);
    }

    private static ScanSessionService CreateService(IScannerBackend backend) =>
        new(backend, NullLogger<ScanSessionService>.Instance);

    private static StartScanRequest Request() =>
        new("device", "twain", "silent", "flatbed", "color", 300);

    private static async Task<ScanSessionDto> WaitForTerminalState(ScanSessionService service, string id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var session = service.Get(id)!;
            if (session.Status is "completed" or "cancelled" or "failed") return session;
            await Task.Delay(10, timeout.Token);
        }
    }
}

internal sealed class FakeScannerBackend(bool waitUntilCancelled = false) : IScannerBackend
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<IReadOnlyList<ScannerDeviceDto>> ListDevicesAsync(
        string driver,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScannerDeviceDto>>([new("device", "Scanner de teste", driver)]);

    public async Task ScanAsync(
        StartScanRequest request,
        string outputDirectory,
        Action<ScanPageDto> onPage,
        CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        if (waitUntilCancelled)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return;
        }

        var pageId = "page-001";
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, $"{pageId}.png"), [1, 2, 3], cancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "scan.pdf"), [1, 2, 3], cancellationToken);
        onPage(new ScanPageDto(pageId, 1, "image/png", 100, 200));
    }
}
