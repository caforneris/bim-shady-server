using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace BimShady.WebApi;

/// <summary>
/// REST API controller for Revit operations using EmbedIO
/// </summary>
public class RevitApiController : WebApiController
{
    /// <summary>
    /// GET /api/health - Health check endpoint
    /// </summary>
    [Route(HttpVerbs.Get, "/health")]
    public async Task<object> GetHealth()
    {
        return new
        {
            status = "running",
            server = "BimShady Revit API",
            version = "1.0.0",
            timestamp = DateTime.UtcNow,
            revitConnected = RevitExternalEventHandler.ExternalEvent != null
        };
    }

    /// <summary>
    /// GET /api/ping - Simple ping to Revit
    /// </summary>
    [Route(HttpVerbs.Get, "/ping")]
    public async Task<object> Ping()
    {
        var request = new RevitApiRequest { Action = "ping" };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// GET /api/project - Get current project information
    /// </summary>
    [Route(HttpVerbs.Get, "/project")]
    public async Task<object> GetProjectInfo()
    {
        var request = new RevitApiRequest { Action = "get_project_info" };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// GET /api/categories - Get all available categories
    /// </summary>
    [Route(HttpVerbs.Get, "/categories")]
    public async Task<object> GetCategories()
    {
        var request = new RevitApiRequest { Action = "get_all_categories" };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// GET /api/elements/{categoryName} - Get elements by category
    /// </summary>
    [Route(HttpVerbs.Get, "/elements/{categoryName}")]
    public async Task<object> GetElementsByCategory(string categoryName)
    {
        var request = new RevitApiRequest
        {
            Action = "get_elements_by_category",
            Parameters = new Dictionary<string, object> { { "categoryName", categoryName } }
        };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// GET /api/element/{id} - Get element by ID
    /// </summary>
    [Route(HttpVerbs.Get, "/element/{id}")]
    public async Task<object> GetElementById(int id)
    {
        var request = new RevitApiRequest
        {
            Action = "get_element_by_id",
            Parameters = new Dictionary<string, object> { { "elementId", id } }
        };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/execute - Execute a custom action with JSON body
    /// </summary>
    [Route(HttpVerbs.Post, "/execute")]
    public async Task<object> ExecuteAction()
    {
        using var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        var request = JsonConvert.DeserializeObject<RevitApiRequest>(body);
        if (request == null)
        {
            return new RevitApiResponse { Success = false, Error = "Invalid JSON body" };
        }

        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/walls - Create walls from drawing application payload
    /// Expects JSON body with array of wall line segments
    /// </summary>
    [Route(HttpVerbs.Post, "/walls")]
    public async Task<object> CreateWalls()
    {
        using var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        // Parse the incoming payload
        var payload = JsonConvert.DeserializeObject<WallCreationPayload>(body);
        if (payload == null || payload.Walls == null || payload.Walls.Count == 0)
        {
            return new RevitApiResponse { Success = false, Error = "Invalid payload. Expected: { \"walls\": [...] }" };
        }

        // Convert to internal request format
        var request = new RevitApiRequest
        {
            Action = "create_walls",
            Parameters = new Dictionary<string, object>
            {
                { "walls", JsonConvert.SerializeObject(payload.Walls) }
            }
        };

        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/import - Import full drawing with walls and rooms
    /// Accepts the exact JSON format from the drawing application
    /// </summary>
    [Route(HttpVerbs.Post, "/import")]
    public async Task<object> ImportDrawing()
    {
        using var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        // Validate the payload
        var payload = JsonConvert.DeserializeObject<DrawingPayload>(body);
        if (payload == null)
        {
            return new RevitApiResponse { Success = false, Error = "Invalid JSON payload" };
        }

        if ((payload.Walls == null || payload.Walls.Count == 0) &&
            (payload.Rooms == null || payload.Rooms.Count == 0))
        {
            return new RevitApiResponse { Success = false, Error = "Payload must contain walls or rooms" };
        }

        // Queue the import request
        var request = new RevitApiRequest
        {
            Action = "import_drawing",
            Parameters = new Dictionary<string, object>
            {
                { "payload", body }
            }
        };

        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/schedule - Create a room schedule
    /// </summary>
    [Route(HttpVerbs.Post, "/schedule")]
    public async Task<object> CreateRoomSchedule()
    {
        var request = new RevitApiRequest { Action = "create_room_schedule" };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/sheet - Create documentation sheet with plan, 3D, and elevation views
    /// </summary>
    [Route(HttpVerbs.Post, "/sheet")]
    public async Task<object> CreateDocumentationSheet()
    {
        var request = new RevitApiRequest { Action = "create_documentation_sheet" };
        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/export - Export sheet to PDF/DWG (for SVG conversion)
    /// Optional body: { "sheetId": 12345 }
    /// </summary>
    [Route(HttpVerbs.Post, "/export")]
    public async Task<object> ExportSheet()
    {
        using var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        var request = new RevitApiRequest { Action = "export_sheet_to_svg" };

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                request.Parameters = parameters;
            }
            catch
            {
                // No parameters, will use most recent sheet
            }
        }

        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        return response;
    }

    /// <summary>
    /// POST /api/import-plan - Import comprehensive floor plan with rooms, doors, fixtures
    /// Accepts full schema with project, levels, rooms, walls, doors, fixtures
    /// </summary>
    [Route(HttpVerbs.Post, "/import-plan")]
    public async Task<object> ImportComprehensivePlan()
    {
        BimShadyLogger.LogApi("POST /api/import-plan received");
        using var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        BimShadyLogger.Log($"Request body size: {body.Length} characters");

        var plan = JsonConvert.DeserializeObject<ComprehensiveFloorPlan>(body);
        if (plan == null)
        {
            BimShadyLogger.LogError("Failed to deserialize comprehensive floor plan");
            return new RevitApiResponse { Success = false, Error = "Invalid JSON payload" };
        }

        BimShadyLogger.Log($"Parsed plan: {plan.Levels?.Count ?? 0} levels");

        var request = new RevitApiRequest
        {
            Action = "import_comprehensive_plan",
            Parameters = new Dictionary<string, object>
            {
                { "payload", body }
            }
        };

        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        BimShadyLogger.LogApi($"POST /api/import-plan completed: {(response.Success ? "SUCCESS" : "FAILED")}");
        return response;
    }

    /// <summary>
    /// POST /api/import-sketch - Import sketch from drawing app
    /// Simple format: walls (with IDs), doors (snapped), rooms (with labels)
    /// Accepts either:
    /// - Direct JSON payload in body
    /// - File path string (e.g., "C:\path\to\file.json") to read from disk
    /// Coordinates are in feet
    /// </summary>
    [Route(HttpVerbs.Post, "/import-sketch")]
    public async Task<object> ImportSketch()
    {
        BimShadyLogger.LogApi("POST /api/import-sketch received");
        using var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        BimShadyLogger.Log($"Request body size: {body.Length} characters");

        string jsonPayload = body;

        // Check if body is a file path (starts with drive letter or is a path)
        var trimmedBody = body.Trim().Trim('"');
        if ((trimmedBody.Length > 2 && trimmedBody[1] == ':' && (trimmedBody[2] == '\\' || trimmedBody[2] == '/')) ||
            trimmedBody.StartsWith("\\\\") || trimmedBody.StartsWith("//"))
        {
            // It's a file path, read the file
            BimShadyLogger.Log($"Body appears to be a file path: {trimmedBody}");
            if (!System.IO.File.Exists(trimmedBody))
            {
                BimShadyLogger.LogError($"File not found: {trimmedBody}");
                return new RevitApiResponse { Success = false, Error = $"File not found: {trimmedBody}" };
            }

            try
            {
                jsonPayload = await System.IO.File.ReadAllTextAsync(trimmedBody);
                BimShadyLogger.Log($"Read {jsonPayload.Length} characters from file");
            }
            catch (Exception ex)
            {
                BimShadyLogger.LogError($"Failed to read file: {ex.Message}");
                return new RevitApiResponse { Success = false, Error = $"Failed to read file: {ex.Message}" };
            }
        }

        var sketch = JsonConvert.DeserializeObject<SketchPayload>(jsonPayload);
        if (sketch == null)
        {
            BimShadyLogger.LogError("Failed to deserialize sketch payload");
            return new RevitApiResponse { Success = false, Error = "Invalid JSON payload" };
        }

        BimShadyLogger.Log($"Parsed sketch: {sketch.Walls?.Count ?? 0} walls, {sketch.Doors?.Count ?? 0} doors, {sketch.Rooms?.Count ?? 0} rooms");

        var request = new RevitApiRequest
        {
            Action = "import_sketch",
            Parameters = new Dictionary<string, object>
            {
                { "payload", jsonPayload }
            }
        };

        var response = await RevitExternalEventHandler.Instance.QueueRequestAsync(request);
        BimShadyLogger.LogApi($"POST /api/import-sketch completed: {(response.Success ? "SUCCESS" : "FAILED")}");
        return response;
    }
}

/// <summary>
/// Payload format for wall creation from drawing applications
/// </summary>
public class WallCreationPayload
{
    public List<WallLineData>? Walls { get; set; }
}

public class WallLineData
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; } = 0;
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; } = 0;
    public double Height { get; set; } = 10.0;
}
