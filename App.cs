using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System.Reflection;
using System.Windows.Media.Imaging;
using BimShady.WebApi;

namespace BimShady;

public class App : IExternalApplication
{
    private static PushButton? _apiServerButton;

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            BimShadyLogger.Log("BimShady plugin starting up...");
            BimShadyLogger.Log($"Revit Version: {application.ControlledApplication.VersionName}");

            // Initialize the External Event Handler for thread-safe Revit API calls
            RevitExternalEventHandler.Initialize(application);
            BimShadyLogger.Log("External Event Handler initialized");

            // Create ribbon tab
            string tabName = "BIM Shady";
            application.CreateRibbonTab(tabName);
            BimShadyLogger.Log($"Created ribbon tab: {tabName}");

            // Create ribbon panel
            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Tools");

            // Get assembly path for button data
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            BimShadyLogger.Log($"Assembly path: {assemblyPath}");

            // Create API Server button
            PushButtonData apiServerButtonData = new PushButtonData(
                "StartApiServer",
                "Start\nAPI Server",
                assemblyPath,
                "BimShady.WebApi.StartApiServerCommand"
            );
            apiServerButtonData.ToolTip = "Start/Stop the REST API server for external JSON requests";
            _apiServerButton = panel.AddItem(apiServerButtonData) as PushButton;

            // Create View Logs button
            PushButtonData viewLogsButtonData = new PushButtonData(
                "ViewLogs",
                "View\nLogs",
                assemblyPath,
                "BimShady.ViewLogsCommand"
            );
            viewLogsButtonData.ToolTip = "Open the BimShady log viewer for debugging";
            panel.AddItem(viewLogsButtonData);

            // Subscribe to server state changes to update button text
            StartApiServerCommand.OnServerStateChanged += UpdateApiServerButton;

            BimShadyLogger.LogSuccess("BimShady plugin initialized successfully");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            BimShadyLogger.LogError("Failed to initialize BimShady plugin", ex);
            Autodesk.Revit.UI.TaskDialog.Show("BIM Shady Error", $"Failed to initialize: {ex.Message}");
            return Result.Failed;
        }
    }

    private static void UpdateApiServerButton(bool isRunning)
    {
        if (_apiServerButton != null)
        {
            _apiServerButton.ItemText = isRunning ? "Stop\nAPI Server" : "Start\nAPI Server";
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        BimShadyLogger.Log("BimShady plugin shutting down...");
        // Clean up the web server on shutdown
        StartApiServerCommand.Shutdown();
        BimShadyLogger.LogSuccess("BimShady plugin shutdown complete");
        return Result.Succeeded;
    }
}
