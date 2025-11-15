using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.WebApi;
using Swan.Logging;

namespace BimShady.WebApi;

/// <summary>
/// EmbedIO web server for hosting Revit REST API
/// </summary>
public class RevitWebServer : IDisposable
{
    private WebServer? _server;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public bool IsRunning { get; private set; }
    public string Url { get; private set; } = "http://localhost:8080";
    public int Port { get; private set; } = 8080;

    public event Action<string>? OnLogMessage;
    public event Action<bool>? OnServerStateChanged;

    public RevitWebServer(int port = 8080)
    {
        Port = port;
        Url = $"http://localhost:{port}";

        // Configure Swan logging to be quiet
        try
        {
            Logger.UnregisterLogger<ConsoleLogger>();
        }
        catch
        {
            // Logger may not be registered, ignore
        }
    }

    public void Start()
    {
        if (IsRunning)
        {
            BimShadyLogger.LogWarning("Server is already running");
            OnLogMessage?.Invoke("Server is already running");
            return;
        }

        try
        {
            BimShadyLogger.LogServerStart(Url, Port);
            _cts = new CancellationTokenSource();

            _server = new WebServer(o => o
                .WithUrlPrefix(Url)
                .WithMode(HttpListenerMode.EmbedIO))
                .WithCors()
                .WithWebApi("/api", m => m
                    .WithController<RevitApiController>());

            // Add custom response serializer for JSON
            _server.WithModule(new ActionModule("/", HttpVerbs.Any, ctx =>
            {
                ctx.Response.ContentType = "application/json";
                return Task.CompletedTask;
            }));

            _serverTask = _server.RunAsync(_cts.Token);
            IsRunning = true;

            BimShadyLogger.LogSuccess($"Server started successfully at {Url}");
            OnLogMessage?.Invoke($"Server started at {Url}");
            OnLogMessage?.Invoke("Available endpoints:");
            OnLogMessage?.Invoke("  GET  /api/health - Health check");
            OnLogMessage?.Invoke("  GET  /api/ping - Ping Revit");
            OnLogMessage?.Invoke("  GET  /api/project - Project info");
            OnLogMessage?.Invoke("  GET  /api/categories - All categories");
            OnLogMessage?.Invoke("  GET  /api/elements/{category} - Elements by category");
            OnLogMessage?.Invoke("  GET  /api/element/{id} - Element by ID");
            OnLogMessage?.Invoke("  POST /api/execute - Execute custom action");

            OnServerStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            BimShadyLogger.LogError("Failed to start server", ex);
            OnLogMessage?.Invoke($"Failed to start server: {ex.Message}");
            IsRunning = false;
            OnServerStateChanged?.Invoke(false);
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            BimShadyLogger.LogWarning("Server is not running");
            OnLogMessage?.Invoke("Server is not running");
            return;
        }

        try
        {
            BimShadyLogger.Log("Stopping server...");
            _cts?.Cancel();
            _server?.Dispose();
            _server = null;
            _cts = null;
            IsRunning = false;

            BimShadyLogger.LogServerStop();
            OnLogMessage?.Invoke("Server stopped");
            OnServerStateChanged?.Invoke(false);
        }
        catch (Exception ex)
        {
            BimShadyLogger.LogError("Error stopping server", ex);
            OnLogMessage?.Invoke($"Error stopping server: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
