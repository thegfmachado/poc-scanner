using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:17841");

var allowedOrigins = builder.Configuration
	.GetSection("AllowedOrigins")
	.Get<string[]>() ?? ["http://localhost:5173", "http://127.0.0.1:5173"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
	.WithOrigins(allowedOrigins)
	.AllowAnyHeader()
	.AllowAnyMethod()));
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<IScannerBackend, Naps2ScannerBackend>();
builder.Services.AddSingleton<ScanSessionService>();

var app = builder.Build();
app.UseCors();

app.Use(async (context, next) =>
{
	if (!context.Request.Path.StartsWithSegments("/api") ||
		context.Request.Path == "/api/health" ||
		context.Request.Path == "/api/pair")
	{
		await next();
		return;
	}

	var pairing = context.RequestServices.GetRequiredService<PairingService>();
	var bearerToken = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
	var token = string.IsNullOrWhiteSpace(bearerToken)
		? context.Request.Query["access_token"].ToString()
		: bearerToken;

	if (!pairing.IsAuthorized(token))
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		await context.Response.WriteAsJsonAsync(new { message = "Agente nao pareado." });
		return;
	}

	await next();
});

app.MapGet("/api/health", (HttpRequest request, PairingService pairing) =>
{
	var token = request.Headers.Authorization.ToString()
		.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
	var paired = pairing.IsAuthorized(token);
	return new
	{
		available = true,
		paired,
		version = typeof(Program).Assembly.GetName().Version?.ToString(),
		message = paired ? "Agente conectado." : "Informe o codigo exibido pelo agente."
	};
});

app.MapPost("/api/pair", (PairRequest request, PairingService pairing) =>
	pairing.TryPair(request.Code, out var token)
		? Results.Ok(new { token })
		: Results.BadRequest(new { message = "Codigo de pareamento invalido." }));

app.MapGet("/api/devices", async (
	string driver,
	IScannerBackend backend,
	CancellationToken cancellationToken) =>
	Results.Ok(await backend.ListDevicesAsync(driver, cancellationToken)));

app.MapPost("/api/scans", (StartScanRequest request, ScanSessionService sessions) =>
{
	if (request.Dpi is < 75 or > 1200)
		return Results.BadRequest(new { message = "Resolucao deve estar entre 75 e 1200 DPI." });

	return Results.Accepted($"/api/scans/{{id}}", sessions.Start(request));
});

app.MapGet("/api/scans/{id}", (string id, ScanSessionService sessions) =>
	sessions.Get(id) is { } session ? Results.Ok(session) : Results.NotFound());

app.MapPost("/api/scans/{id}/cancel", (string id, ScanSessionService sessions) =>
	sessions.Cancel(id) ? Results.NoContent() : Results.NotFound());

app.MapGet("/api/scans/{id}/pages/{pageId}", (string id, string pageId, ScanSessionService sessions) =>
	sessions.GetPagePath(id, pageId) is { } path
		? Results.File(path, "image/png", enableRangeProcessing: true)
		: Results.NotFound());

app.MapGet("/api/scans/{id}/export", async (
	string id,
	string format,
	ScanSessionService sessions,
	CancellationToken cancellationToken) =>
{
	var export = await sessions.GetExportAsync(id, format, cancellationToken);
	return export is { } file
		? Results.File(file.Path, file.ContentType, file.DownloadName, enableRangeProcessing: true)
		: Results.NotFound();
});

_ = app.Services.GetRequiredService<PairingService>();
app.Run();

public partial class Program;

public sealed record PairRequest(string Code);

public sealed class PairingService
{
	private string? _token;

	public PairingService()
	{
		Code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
		Console.WriteLine($"Scanner Agent pairing code: {Code}");
	}

	public string Code { get; }
	public bool HasIssuedToken => _token is not null;

	public bool TryPair(string code, out string? token)
	{
		if (!CryptographicOperations.FixedTimeEquals(
				System.Text.Encoding.UTF8.GetBytes(code),
				System.Text.Encoding.UTF8.GetBytes(Code)))
		{
			token = null;
			return false;
		}

		_token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		token = _token;
		return true;
	}

	public bool IsAuthorized(string token) =>
		_token is not null &&
		CryptographicOperations.FixedTimeEquals(
			System.Text.Encoding.UTF8.GetBytes(token),
			System.Text.Encoding.UTF8.GetBytes(_token));
}
