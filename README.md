# BIM Shady Server

A Revit 2025 plugin that provides a REST API server for programmatic BIM model generation. Import floor plans from JSON, create walls, doors, rooms, and automatically generate documentation sheets with schedules.

## Features

- **REST API Server** - EmbedIO-based web server running inside Revit
- **Sketch Import** - Import walls, doors, and rooms from JSON (coordinates in inches, auto-converted to feet)
- **Automatic Tagging** - Room tags with area, door tags, wall dimensions
- **View Generation** - Creates floor plan views with Fine detail level and 3D views
- **Documentation** - Room schedules, sheet creation with multiple views
- **Export** - PDF/DWG export capabilities
- **Logging** - Comprehensive logging system with built-in log viewer

## Prerequisites

- **Revit 2025** (uses .NET 8.0)
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** or VS Code with C# extension (optional)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/caforneris/bim-shady-server.git
cd bim-shady-server
```

### 2. Build the Plugin

```bash
dotnet build
```

This will:
- Compile the plugin
- Automatically deploy to `%APPDATA%\Autodesk\ApplicationPlugins\bim-shady.bundle\`
- Copy all dependencies (EmbedIO, Newtonsoft.Json, etc.)

### 3. Start Revit

1. Launch Revit 2025
2. Open or create a new project
3. Look for the **"BIM Shady"** tab in the ribbon

### 4. Start the API Server

1. Click **"Start API Server"** button in the BIM Shady tab
2. Server starts on `http://localhost:8080`
3. Button changes to **"Stop API Server"**

### 5. Test the API

```bash
# Health check
curl http://localhost:8080/api/health

# Import a sketch
curl -X POST http://localhost:8080/api/import-sketch \
  -H "Content-Type: application/json" \
  -d @your-sketch.json
```

Or use the included test script:
```bash
test-api health
test-api import-sketch
test-api full-pipeline
```

## API Endpoints

### Core Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Server health check |
| `/api/ping` | GET | Ping Revit thread |
| `/api/project` | GET | Get project information |
| `/api/categories` | GET | List all categories |

### Import Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/import-sketch` | POST | **Primary** - Import sketch (walls, doors, rooms) |
| `/api/import-plan` | POST | Import comprehensive floor plan |
| `/api/import` | POST | Legacy simple import |
| `/api/walls` | POST | Create walls only |

### Documentation Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/schedule` | POST | Create room schedule |
| `/api/sheet` | POST | Create documentation sheet with views |
| `/api/export` | POST | Export sheet to PDF/DWG |

## JSON Schema (import-sketch)

The primary import format. Coordinates are in **feet**.

```json
{
  "walls": [
    {
      "wall_id": "wall_1",
      "start_point": { "x": 0, "y": 0 },
      "end_point": { "x": 240, "y": 0 }
    }
  ],
  "doors": [
    {
      "start": { "x": 100, "y": 0 },
      "end": { "x": 136, "y": 0 },
      "original": {
        "start": { "x": 100, "y": 0 },
        "end": { "x": 136, "y": 0 }
      }
    }
  ],
  "rooms": [
    {
      "room_name": "Living Room",
      "center_point": { "x": 120, "y": 120 }
    }
  ],
  "wall_height": 10.0
}
```

**Coordinate System:**
- X, Y in feet (used directly)
- Y is negated internally for Revit's coordinate system
- Wall height is in feet (default: 10.0)

## Project Structure

```
bim-shady-server/
├── App.cs                          # Main entry point, ribbon setup
├── BimShady.csproj                 # Project file with auto-deployment
├── BimShadyLogger.cs               # Logging infrastructure
├── ViewLogsCommand.cs              # Log viewer UI
├── HelloWorldCommand.cs            # Example command (unused)
├── test-api.cmd                    # API testing script
├── Deployment/
│   ├── BimShady.addin              # Revit add-in manifest
│   └── PackageContents.xml         # Plugin package descriptor
└── WebApi/
    ├── StartApiServerCommand.cs    # Server start/stop command
    ├── RevitWebServer.cs           # EmbedIO server configuration
    ├── RevitApiController.cs       # REST API route definitions
    ├── RevitExternalEventHandler.cs # Thread-safe Revit API operations
    ├── SketchPayloadModels.cs      # JSON models for sketch import
    ├── DrawingPayloadModels.cs     # Legacy JSON models
    └── ComprehensivePayloadModels.cs # Extended JSON models
```

## Code Architecture

### Thread Safety with External Events

Revit API can only be called from the main Revit thread. This plugin uses `IExternalEventHandler` to safely queue operations:

```
HTTP Request → RevitApiController → Queue Request → External Event → Revit API
```

**Key Components:**

1. **RevitApiController** - Handles HTTP requests, validates JSON, queues operations
2. **RevitExternalEventHandler** - Executes queued operations on Revit's main thread
3. **RevitApiRequest/Response** - Request/response models for the queue

### Import Pipeline (import-sketch)

```csharp
// 1. Parse JSON payload
var sketch = JsonConvert.DeserializeObject<SketchPayload>(body);

// 2. Create floor plan view
var newFloorPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
newFloorPlan.DetailLevel = ViewDetailLevel.Fine;

// 3. Create walls (coordinates already in feet)
var startPt = new XYZ(wallDef.StartPoint.X, -wallDef.StartPoint.Y, 0);
var wall = Wall.Create(doc, line, wallType.Id, level.Id, wallHeight, 0, false, false);

// 4. Create doors (auto-host to nearest wall)
var door = doc.Create.NewFamilyInstance(doorCenter, doorType, hostWall, ...);

// 5. Create rooms
var room = doc.Create.NewRoom(level, new UV(centerX, centerY));
room.Name = roomDef.TextContent;

// 6. Add tags (room tags with area, door tags, wall dimensions)
var roomTag = doc.Create.NewRoomTag(...);
roomTag.ChangeTypeId(roomTagTypeWithArea.Id);
```

## Building from Source

### Debug Build

```bash
dotnet build
```

### Release Build

```bash
dotnet build -c Release
```

### Clean Build

```bash
dotnet clean
dotnet build
```

### Manual Deployment

If auto-deployment fails (DLL locked by Revit):

1. Close Revit
2. Build the project
3. Restart Revit

Or manually copy files:
```bash
copy bin\Debug\net8.0-windows\*.dll %APPDATA%\Autodesk\ApplicationPlugins\bim-shady.bundle\Contents\
```

## Configuration

### Server Port

Default: `8080`

To change, modify `RevitWebServer.cs`:
```csharp
_server = new WebServer("http://localhost:YOUR_PORT");
```

### Wall Height

Default: `10.0` feet

Set in JSON payload:
```json
{
  "wall_height": 12.0,
  ...
}
```

## Logging

Logs are stored in: `%APPDATA%\BimShady\Logs\BimShady.log`

### View Logs in Revit

Click **"View Logs"** button in the BIM Shady tab.

Features:
- Auto-refresh (2 second interval)
- Copy to clipboard
- Clear log
- Open log folder

### Log Levels

- `[INFO]` - General information
- `[ERROR]` - Errors with stack traces
- `[WARN]` - Warnings
- `[SUCCESS]` - Successful operations
- `[API]` - HTTP request/response
- `[REVIT]` - Revit-specific operations
- `[ELEMENT]` - Element creation
- `[TAG]` - Tag creation
- `[SERVER]` - Server start/stop

## Troubleshooting

### "No level found in project"

Open a project with at least one level defined.

### "No wall type found"

Your template needs basic wall types. Use the default Revit architectural template.

### DLL Locked During Build

Close Revit before building, or wait for the build retry mechanism.

### Room Tags Not Showing Area

The plugin searches for Room Tag types containing "Area" in the name. Load appropriate tag families.

### Server Won't Start

1. Check if port 8080 is in use: `netstat -an | findstr 8080`
2. Try running Revit as Administrator
3. Check Windows Firewall settings

## Full Pipeline Example

```bash
# Run complete sketch-to-BIM workflow
test-api full-pipeline
```

This executes:
1. **Import Sketch** - Create walls, doors, rooms with centered tags
2. **Create Schedule** - Room schedule with areas
3. **Create Sheet** - Documentation sheet (floor plan @ 1/2"=1'-0", 3D view, schedule)
4. **Export** - PDF/DWG output

## Dependencies

- **EmbedIO 3.5.2** - Lightweight embedded web server
- **Newtonsoft.Json 13.0.3** - JSON serialization
- **RevitAPI** - Autodesk Revit 2025 API
- **RevitAPIUI** - Autodesk Revit 2025 UI API

## License

See [LICENSE](LICENSE) file.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## Acknowledgments

Built for automating BIM workflows from sketch/drawing applications to full Revit models.
