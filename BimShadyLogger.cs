using System;
using System.IO;
using System.Diagnostics;
using Autodesk.Revit.UI;

namespace BimShady;

public static class BimShadyLogger
{
    private static string _logFilePath;
    private static readonly object _lockObject = new object();
    private static bool _loggingEnabled = true;

    static BimShadyLogger()
    {
        // Create logs directory in AppData
        string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimShady", "Logs");
        Directory.CreateDirectory(logsDir);

        // Use a single log file that appends
        _logFilePath = Path.Combine(logsDir, "BimShady.log");

        // Add session separator
        AppendToLog($"\n\n{'=',-80}\n=== NEW SESSION: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n{'=',-80}");
        LogInternal($"Log file: {_logFilePath}", "INFO");
        LogInternal($"Process ID: {Process.GetCurrentProcess().Id}", "INFO");
        LogInternal($"BimShady Server Version: 1.0.0", "INFO");
    }

    public static string LogFilePath => _logFilePath;

    private static void AppendToLog(string text)
    {
        try
        {
            lock (_lockObject)
            {
                File.AppendAllText(_logFilePath, text + Environment.NewLine);
            }
        }
        catch { }
    }

    public static void Log(string message, string category = "INFO")
    {
        if (!_loggingEnabled && category != "ERROR" && category != "WARNING")
        {
            return;
        }

        LogInternal(message, category);
    }

    private static void LogInternal(string message, string category)
    {
        try
        {
            lock (_lockObject)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{category,-8}] {message}";

                // Write to file
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);

                // Also write to Debug output (visible in VS Output window)
                Debug.WriteLine($"BimShady: {logEntry}");

                // Write to console if available
                Console.WriteLine(logEntry);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BimShady Logger Error: {ex.Message}");
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        string errorMsg = ex != null ? $"{message} - Exception: {ex}" : message;
        Log(errorMsg, "ERROR");
    }

    public static void LogWarning(string message)
    {
        Log(message, "WARN");
    }

    public static void LogSuccess(string message)
    {
        Log(message, "SUCCESS");
    }

    public static void LogApi(string message)
    {
        Log(message, "API");
    }

    public static void LogRevit(string message)
    {
        Log(message, "REVIT");
    }

    public static void LogMethodEntry(string methodName, string className = "")
    {
        string fullName = string.IsNullOrEmpty(className) ? methodName : $"{className}.{methodName}";
        Log($">>> ENTERING: {fullName}", "TRACE");
    }

    public static void LogMethodExit(string methodName, string className = "")
    {
        string fullName = string.IsNullOrEmpty(className) ? methodName : $"{className}.{methodName}";
        Log($"<<< EXITING: {fullName}", "TRACE");
    }

    public static void LogRequest(string action, Dictionary<string, object>? parameters = null)
    {
        string paramInfo = parameters != null ? $" with {parameters.Count} parameters" : "";
        Log($"=== API REQUEST: {action}{paramInfo} ===", "REQUEST");
    }

    public static void LogResponse(bool success, string message)
    {
        string status = success ? "SUCCESS" : "FAILED";
        Log($"=== API RESPONSE: {status} - {message} ===", "RESPONSE");
    }

    public static string GetLogFilePath()
    {
        return _logFilePath;
    }

    public static void OpenLogFile()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _logFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to open log file", ex);
        }
    }

    public static void LogServerStart(string url, int port)
    {
        Log($"=== SERVER STARTING ===", "SERVER");
        Log($"URL: {url}", "SERVER");
        Log($"Port: {port}", "SERVER");
    }

    public static void LogServerStop()
    {
        Log($"=== SERVER STOPPED ===", "SERVER");
    }

    public static void LogElementCreated(string elementType, long elementId, string details = "")
    {
        string detailsStr = string.IsNullOrEmpty(details) ? "" : $" - {details}";
        Log($"Created {elementType} [ID: {elementId}]{detailsStr}", "ELEMENT");
    }

    public static void LogTagCreated(string tagType, long elementId, long tagId)
    {
        Log($"Tagged {tagType} [Element: {elementId}, Tag: {tagId}]", "TAG");
    }
}
