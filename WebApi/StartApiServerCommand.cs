using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;

namespace BimShady.WebApi;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class StartApiServerCommand : IExternalCommand
{
    private static RevitWebServer? _webServer;
    public static RevitWebServer? WebServer => _webServer;
    public static bool IsServerRunning => _webServer?.IsRunning ?? false;

    public static event Action<bool>? OnServerStateChanged;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (_webServer == null)
            {
                _webServer = new RevitWebServer(8080);
                _webServer.OnLogMessage += msg => System.Diagnostics.Debug.WriteLine($"[BimShady API] {msg}");
                _webServer.OnServerStateChanged += running => OnServerStateChanged?.Invoke(running);
            }

            if (_webServer.IsRunning)
            {
                // Stop the server
                _webServer.Stop();

                var dialog = new Autodesk.Revit.UI.TaskDialog("BimShady API Server");
                dialog.MainInstruction = "Server Stopped";
                dialog.MainContent = "The REST API server has been stopped.";
                dialog.CommonButtons = TaskDialogCommonButtons.Ok;
                dialog.Show();
            }
            else
            {
                // Start the server
                _webServer.Start();

                var dialog = new Autodesk.Revit.UI.TaskDialog("BimShady API Server");
                dialog.MainInstruction = "Server Started";
                dialog.MainContent = $"REST API server is now running at:\n\n{_webServer.Url}\n\n" +
                    "Test endpoints:\n" +
                    "  GET /api/health - Health check\n" +
                    "  GET /api/project - Project info\n" +
                    "  GET /api/categories - All categories\n\n" +
                    "Click the button again to stop the server.";
                dialog.CommonButtons = TaskDialogCommonButtons.Ok;
                dialog.Show();
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }

    public static void Shutdown()
    {
        _webServer?.Dispose();
        _webServer = null;
    }
}
