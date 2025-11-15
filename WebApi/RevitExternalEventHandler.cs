using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;

namespace BimShady.WebApi;

/// <summary>
/// Thread-safe handler for executing Revit API calls from web requests.
/// Revit API is single-threaded, so all operations must be marshalled through ExternalEvent.
/// </summary>
public class RevitExternalEventHandler : IExternalEventHandler
{
    private static RevitExternalEventHandler? _instance;
    private static ExternalEvent? _externalEvent;

    private readonly ConcurrentQueue<RevitApiRequest> _requestQueue = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RevitApiResponse>> _pendingRequests = new();

    public static RevitExternalEventHandler Instance => _instance ??= new RevitExternalEventHandler();
    public static ExternalEvent? ExternalEvent => _externalEvent;

    public static void Initialize(UIControlledApplication app)
    {
        _instance = new RevitExternalEventHandler();
        _externalEvent = ExternalEvent.Create(_instance);
        BimShadyLogger.Log("RevitExternalEventHandler initialized");
    }

    public string GetName() => "BimShady REST API Handler";

    public void Execute(UIApplication app)
    {
        // Process all queued requests
        while (_requestQueue.TryDequeue(out var request))
        {
            BimShadyLogger.LogRequest(request.Action, request.Parameters);
            var response = ProcessRequest(app, request);
            BimShadyLogger.LogResponse(response.Success, response.Error ?? "OK");

            if (_pendingRequests.TryRemove(request.RequestId, out var tcs))
            {
                tcs.SetResult(response);
            }
        }
    }

    /// <summary>
    /// Queue a request to be executed on Revit's main thread
    /// </summary>
    public Task<RevitApiResponse> QueueRequestAsync(RevitApiRequest request)
    {
        BimShadyLogger.Log($"Queuing request: {request.Action} [ID: {request.RequestId}]");
        var tcs = new TaskCompletionSource<RevitApiResponse>();
        _pendingRequests[request.RequestId] = tcs;
        _requestQueue.Enqueue(request);

        // Raise the external event to trigger Execute()
        _externalEvent?.Raise();

        return tcs.Task;
    }

    private RevitApiResponse ProcessRequest(UIApplication app, RevitApiRequest request)
    {
        try
        {
            BimShadyLogger.LogMethodEntry(request.Action, "ProcessRequest");
            var result = request.Action switch
            {
                "get_project_info" => GetProjectInfo(app),
                "get_elements_by_category" => GetElementsByCategory(app, request.Parameters),
                "get_element_by_id" => GetElementById(app, request.Parameters),
                "get_all_categories" => GetAllCategories(app),
                "create_walls" => CreateWalls(app, request.Parameters),
                "import_drawing" => ImportDrawing(app, request.Parameters),
                "create_room_schedule" => CreateRoomSchedule(app),
                "create_documentation_sheet" => CreateDocumentationSheet(app),
                "export_sheet_to_svg" => ExportSheetToSvg(app, request.Parameters),
                "import_comprehensive_plan" => ImportComprehensivePlan(app, request.Parameters),
                "ping" => new RevitApiResponse { Success = true, Data = new { message = "pong", timestamp = DateTime.UtcNow } },
                _ => new RevitApiResponse { Success = false, Error = $"Unknown action: {request.Action}" }
            };
            BimShadyLogger.LogMethodExit(request.Action, "ProcessRequest");
            return result;
        }
        catch (Exception ex)
        {
            BimShadyLogger.LogError($"Exception in ProcessRequest for {request.Action}", ex);
            return new RevitApiResponse { Success = false, Error = ex.Message };
        }
    }

    private RevitApiResponse GetProjectInfo(UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        var info = new
        {
            title = doc.Title,
            pathName = doc.PathName,
            isWorkshared = doc.IsWorkshared,
            projectInfo = new
            {
                name = doc.ProjectInformation.Name,
                number = doc.ProjectInformation.Number,
                client = doc.ProjectInformation.ClientName,
                address = doc.ProjectInformation.Address,
                buildingName = doc.ProjectInformation.BuildingName
            }
        };

        return new RevitApiResponse { Success = true, Data = info };
    }

    private RevitApiResponse GetElementsByCategory(UIApplication app, Dictionary<string, object>? parameters)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        if (parameters == null || !parameters.ContainsKey("categoryName"))
            return new RevitApiResponse { Success = false, Error = "Missing categoryName parameter" };

        var categoryName = parameters["categoryName"].ToString();
        var category = doc.Settings.Categories.Cast<Category>()
            .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

        if (category == null)
            return new RevitApiResponse { Success = false, Error = $"Category not found: {categoryName}" };

        var collector = new FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType();

        var elements = collector.Select(e => new
        {
            id = e.Id.IntegerValue,
            name = e.Name,
            category = e.Category?.Name,
            typeName = doc.GetElement(e.GetTypeId())?.Name
        }).Take(100).ToList(); // Limit to 100 for performance

        return new RevitApiResponse { Success = true, Data = new { count = elements.Count, elements } };
    }

    private RevitApiResponse GetElementById(UIApplication app, Dictionary<string, object>? parameters)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        if (parameters == null || !parameters.ContainsKey("elementId"))
            return new RevitApiResponse { Success = false, Error = "Missing elementId parameter" };

        var elementId = Convert.ToInt32(parameters["elementId"]);
        var element = doc.GetElement(new ElementId(elementId));

        if (element == null)
            return new RevitApiResponse { Success = false, Error = $"Element not found: {elementId}" };

        var elementData = new
        {
            id = element.Id.IntegerValue,
            name = element.Name,
            category = element.Category?.Name,
            typeName = doc.GetElement(element.GetTypeId())?.Name,
            parameters = element.Parameters.Cast<Parameter>()
                .Where(p => p.HasValue)
                .Select(p => new
                {
                    name = p.Definition.Name,
                    value = GetParameterValue(p),
                    type = p.StorageType.ToString()
                }).ToList()
        };

        return new RevitApiResponse { Success = true, Data = elementData };
    }

    private RevitApiResponse GetAllCategories(UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        var categories = doc.Settings.Categories.Cast<Category>()
            .Where(c => c.AllowsBoundParameters)
            .Select(c => new { id = c.Id.IntegerValue, name = c.Name })
            .OrderBy(c => c.name)
            .ToList();

        return new RevitApiResponse { Success = true, Data = new { count = categories.Count, categories } };
    }

    private object? GetParameterValue(Parameter param)
    {
        return param.StorageType switch
        {
            StorageType.String => param.AsString(),
            StorageType.Integer => param.AsInteger(),
            StorageType.Double => param.AsDouble(),
            StorageType.ElementId => param.AsElementId().IntegerValue,
            _ => null
        };
    }

    private RevitApiResponse CreateWalls(UIApplication app, Dictionary<string, object>? parameters)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        if (parameters == null || !parameters.ContainsKey("walls"))
            return new RevitApiResponse { Success = false, Error = "Missing walls parameter" };

        var wallsJson = parameters["walls"].ToString();
        var wallDataList = JsonConvert.DeserializeObject<List<WallData>>(wallsJson!);

        if (wallDataList == null || wallDataList.Count == 0)
            return new RevitApiResponse { Success = false, Error = "No wall data provided" };

        // Get default wall type
        var wallType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .FirstOrDefault() as WallType;

        if (wallType == null)
            return new RevitApiResponse { Success = false, Error = "No wall type found in project" };

        // Get the first level
        var level = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .FirstOrDefault() as Level;

        if (level == null)
            return new RevitApiResponse { Success = false, Error = "No level found in project" };

        var createdWalls = new List<object>();

        using (var trans = new Transaction(doc, "Create Walls from API"))
        {
            trans.Start();

            foreach (var wallData in wallDataList)
            {
                try
                {
                    // Coordinates in feet (Revit internal units)
                    var startPt = new XYZ(wallData.StartX, wallData.StartY, wallData.StartZ);
                    var endPt = new XYZ(wallData.EndX, wallData.EndY, wallData.EndZ);
                    var line = Line.CreateBound(startPt, endPt);

                    var height = wallData.Height > 0 ? wallData.Height : 10.0; // Default 10 feet

                    var wall = Wall.Create(doc, line, wallType.Id, level.Id, height, 0, false, false);

                    createdWalls.Add(new
                    {
                        id = wall.Id.Value,
                        startX = wallData.StartX,
                        startY = wallData.StartY,
                        endX = wallData.EndX,
                        endY = wallData.EndY,
                        height = height
                    });
                }
                catch (Exception ex)
                {
                    createdWalls.Add(new
                    {
                        error = ex.Message,
                        startX = wallData.StartX,
                        startY = wallData.StartY,
                        endX = wallData.EndX,
                        endY = wallData.EndY
                    });
                }
            }

            trans.Commit();
        }

        return new RevitApiResponse
        {
            Success = true,
            Data = new
            {
                message = $"Created {createdWalls.Count} walls",
                walls = createdWalls,
                level = level.Name,
                wallType = wallType.Name
            }
        };
    }

    private RevitApiResponse ImportDrawing(UIApplication app, Dictionary<string, object>? parameters)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        if (parameters == null || !parameters.ContainsKey("payload"))
            return new RevitApiResponse { Success = false, Error = "Missing payload parameter" };

        var payloadJson = parameters["payload"].ToString();
        var payload = JsonConvert.DeserializeObject<DrawingPayload>(payloadJson!);

        if (payload == null)
            return new RevitApiResponse { Success = false, Error = "Invalid drawing payload" };

        var scale = payload.Scale;
        var defaultHeight = payload.WallHeight;

        // Get default wall type
        var wallType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .FirstOrDefault() as WallType;

        if (wallType == null)
            return new RevitApiResponse { Success = false, Error = "No wall type found in project" };

        // Get the first level
        var level = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .FirstOrDefault() as Level;

        if (level == null)
            return new RevitApiResponse { Success = false, Error = "No level found in project" };

        var createdWalls = new List<object>();
        var createdRooms = new List<object>();
        var errors = new List<string>();

        using (var trans = new Transaction(doc, "Import Drawing"))
        {
            trans.Start();

            // Create walls
            if (payload.Walls != null)
            {
                foreach (var wallData in payload.Walls)
                {
                    try
                    {
                        // Apply scale factor to convert drawing units to feet
                        var startPt = new XYZ(
                            wallData.StartPoint.X * scale,
                            wallData.StartPoint.Y * scale,
                            wallData.StartPoint.Z * scale
                        );
                        var endPt = new XYZ(
                            wallData.EndPoint.X * scale,
                            wallData.EndPoint.Y * scale,
                            wallData.EndPoint.Z * scale
                        );
                        var line = Line.CreateBound(startPt, endPt);

                        var height = wallData.Height ?? defaultHeight;
                        var wall = Wall.Create(doc, line, wallType.Id, level.Id, height, 0, false, false);

                        createdWalls.Add(new
                        {
                            wallId = wallData.WallId,
                            revitId = wall.Id.Value,
                            startX = wallData.StartPoint.X,
                            startY = wallData.StartPoint.Y,
                            endX = wallData.EndPoint.X,
                            endY = wallData.EndPoint.Y,
                            height = height
                        });
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Wall {wallData.WallId}: {ex.Message}");
                    }
                }
            }

            // Create rooms (place room separation lines and room tags)
            if (payload.Rooms != null)
            {
                // Get a floor plan view for placing room tags
                var floorPlanView = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate);

                foreach (var roomData in payload.Rooms)
                {
                    try
                    {
                        // Create room at the center point
                        var centerPt = new XYZ(
                            roomData.CenterPoint.X * scale,
                            roomData.CenterPoint.Y * scale,
                            level.Elevation
                        );

                        var room = doc.Create.NewRoom(level, new UV(centerPt.X, centerPt.Y));
                        if (room != null)
                        {
                            room.Name = roomData.RoomName;

                            // Get room area (in square feet)
                            var area = room.Area;

                            // Place room tag if we have a view
                            long? tagId = null;
                            if (floorPlanView != null)
                            {
                                try
                                {
                                    var tagPt = new UV(centerPt.X, centerPt.Y);
                                    var roomTag = doc.Create.NewRoomTag(
                                        new LinkElementId(room.Id),
                                        tagPt,
                                        floorPlanView.Id
                                    );
                                    if (roomTag != null)
                                    {
                                        tagId = roomTag.Id.Value;
                                    }
                                }
                                catch
                                {
                                    // Tag placement failed, continue without tag
                                }
                            }

                            createdRooms.Add(new
                            {
                                roomName = roomData.RoomName,
                                revitId = room.Id.Value,
                                tagId = tagId,
                                centerX = roomData.CenterPoint.X,
                                centerY = roomData.CenterPoint.Y,
                                areaSqFt = area
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Room {roomData.RoomName}: {ex.Message}");
                    }
                }
            }

            trans.Commit();
        }

        return new RevitApiResponse
        {
            Success = true,
            Data = new
            {
                message = $"Imported {createdWalls.Count} walls and {createdRooms.Count} rooms",
                walls = createdWalls,
                rooms = createdRooms,
                errors = errors,
                level = level.Name,
                wallType = wallType.Name,
                scale = scale
            }
        };
    }

    private RevitApiResponse CreateRoomSchedule(UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        using (var trans = new Transaction(doc, "Create Room Schedule"))
        {
            trans.Start();

            // Create a new schedule for rooms
            var schedule = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rooms));
            schedule.Name = $"Room Schedule - {DateTime.Now:yyyy-MM-dd HH-mm}";

            // Get schedulable fields
            var definition = schedule.Definition;
            var schedulableFields = definition.GetSchedulableFields();

            // Add Room Name field
            var nameField = schedulableFields.FirstOrDefault(f => f.GetName(doc) == "Name");
            if (nameField != null)
                definition.AddField(nameField);

            // Add Room Number field
            var numberField = schedulableFields.FirstOrDefault(f => f.GetName(doc) == "Number");
            if (numberField != null)
                definition.AddField(numberField);

            // Add Area field
            var areaField = schedulableFields.FirstOrDefault(f => f.GetName(doc) == "Area");
            if (areaField != null)
                definition.AddField(areaField);

            // Add Level field
            var levelField = schedulableFields.FirstOrDefault(f => f.GetName(doc) == "Level");
            if (levelField != null)
                definition.AddField(levelField);

            trans.Commit();

            return new RevitApiResponse
            {
                Success = true,
                Data = new
                {
                    message = "Room schedule created",
                    scheduleId = schedule.Id.Value,
                    scheduleName = schedule.Name
                }
            };
        }
    }

    private RevitApiResponse CreateDocumentationSheet(UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        using (var trans = new Transaction(doc, "Create Documentation Sheet"))
        {
            trans.Start();

            // Get a title block
            var titleBlock = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .FirstOrDefault() as FamilySymbol;

            if (titleBlock == null)
                return new RevitApiResponse { Success = false, Error = "No title block found" };

            // Create the sheet
            var sheet = ViewSheet.Create(doc, titleBlock.Id);
            sheet.SheetNumber = $"A{DateTime.Now:MMddHHmm}";
            sheet.Name = "Generated Floor Plan Documentation";

            var viewsPlaced = new List<object>();

            // Layout: Left side for plan views, right side for schedule
            // Sheet coordinates are in feet from bottom-left

            // 1. Place Floor Plan View (top-left area)
            var floorPlan = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && Viewport.CanAddViewToSheet(doc, sheet.Id, v.Id))
                .OrderByDescending(v => v.Id.Value) // Get most recent
                .FirstOrDefault();

            if (floorPlan != null)
            {
                var viewport = Viewport.Create(doc, sheet.Id, floorPlan.Id, new XYZ(0.8, 1.8, 0));
                viewsPlaced.Add(new { type = "FloorPlan", viewId = floorPlan.Id.Value, viewportId = viewport.Id.Value, name = floorPlan.Name });
            }

            // 2. Place 3D View (bottom-left area)
            var view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(v => !v.IsTemplate && Viewport.CanAddViewToSheet(doc, sheet.Id, v.Id))
                .OrderByDescending(v => v.Id.Value) // Get most recent
                .FirstOrDefault();

            if (view3D != null)
            {
                var viewport = Viewport.Create(doc, sheet.Id, view3D.Id, new XYZ(0.8, 0.7, 0));
                viewsPlaced.Add(new { type = "3D", viewId = view3D.Id.Value, viewportId = viewport.Id.Value, name = view3D.Name });
            }

            // 3. Place Room Schedule (right side)
            var roomSchedule = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(v => v.Name.Contains("Room") && !v.IsTemplate)
                .OrderByDescending(v => v.Id.Value) // Get most recent
                .FirstOrDefault();

            if (roomSchedule != null)
            {
                try
                {
                    // Schedules are placed using ScheduleSheetInstance
                    var scheduleInstance = ScheduleSheetInstance.Create(doc, sheet.Id, roomSchedule.Id, new XYZ(2.0, 1.5, 0));
                    viewsPlaced.Add(new { type = "RoomSchedule", viewId = roomSchedule.Id.Value, instanceId = scheduleInstance.Id.Value, name = roomSchedule.Name });
                }
                catch
                {
                    // Schedule placement failed, continue without it
                }
            }

            trans.Commit();

            return new RevitApiResponse
            {
                Success = true,
                Data = new
                {
                    message = "Documentation sheet created with floor plan, 3D view, and room schedule",
                    sheetId = sheet.Id.Value,
                    sheetNumber = sheet.SheetNumber,
                    sheetName = sheet.Name,
                    viewsPlaced = viewsPlaced
                }
            };
        }
    }

    private RevitApiResponse ExportSheetToSvg(UIApplication app, Dictionary<string, object>? parameters)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
            return new RevitApiResponse { Success = false, Error = "No document open" };

        // Get sheet ID from parameters or use the most recent sheet
        ViewSheet? sheet = null;

        if (parameters != null && parameters.ContainsKey("sheetId"))
        {
            var sheetId = Convert.ToInt64(parameters["sheetId"]);
            sheet = doc.GetElement(new ElementId(sheetId)) as ViewSheet;
        }
        else
        {
            // Get the most recently created sheet
            sheet = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderByDescending(s => s.Id.Value)
                .FirstOrDefault();
        }

        if (sheet == null)
            return new RevitApiResponse { Success = false, Error = "No sheet found" };

        // Export to DWG first (Revit doesn't have direct SVG export)
        // We'll export to DWG then the frontend can convert, OR export to PDF
        var exportPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BimShady_Exports"
        );
        Directory.CreateDirectory(exportPath);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"Sheet_{sheet.SheetNumber}_{timestamp}";

        // Export as PDF (more universally useful)
        var pdfOptions = new PDFExportOptions
        {
            FileName = fileName,
            Combine = true,
            ExportQuality = PDFExportQualityType.DPI300
        };

        var viewIds = new List<ElementId> { sheet.Id };
        var pdfPath = Path.Combine(exportPath, $"{fileName}.pdf");

        try
        {
            doc.Export(exportPath, viewIds, pdfOptions);
        }
        catch (Exception ex)
        {
            return new RevitApiResponse
            {
                Success = false,
                Error = $"PDF export failed: {ex.Message}"
            };
        }

        // Also export as DWG for potential SVG conversion
        var dwgOptions = new DWGExportOptions
        {
            MergedViews = true,
            FileVersion = ACADVersion.R2018
        };

        var dwgPath = Path.Combine(exportPath, $"{fileName}.dwg");
        try
        {
            doc.Export(exportPath, fileName, viewIds, dwgOptions);
        }
        catch
        {
            // DWG export is optional, continue if it fails
        }

        return new RevitApiResponse
        {
            Success = true,
            Data = new
            {
                message = "Sheet exported",
                sheetId = sheet.Id.Value,
                sheetNumber = sheet.SheetNumber,
                pdfPath = pdfPath,
                dwgPath = dwgPath,
                exportFolder = exportPath,
                note = "PDF exported. For SVG, use a PDF-to-SVG converter or process the DWG file."
            }
        };
    }

    private RevitApiResponse ImportComprehensivePlan(UIApplication app, Dictionary<string, object>? parameters)
    {
        BimShadyLogger.Log("Starting ImportComprehensivePlan...");
        var doc = app.ActiveUIDocument?.Document;
        if (doc == null)
        {
            BimShadyLogger.LogError("No document open");
            return new RevitApiResponse { Success = false, Error = "No document open" };
        }

        BimShadyLogger.Log($"Active document: {doc.Title}");

        if (parameters == null || !parameters.ContainsKey("payload"))
        {
            BimShadyLogger.LogError("Missing payload parameter");
            return new RevitApiResponse { Success = false, Error = "Missing payload parameter" };
        }

        var payloadJson = parameters["payload"].ToString();
        BimShadyLogger.Log($"Payload size: {payloadJson?.Length ?? 0} characters");

        var plan = JsonConvert.DeserializeObject<ComprehensiveFloorPlan>(payloadJson!);

        if (plan == null || plan.Levels == null || plan.Levels.Count == 0)
        {
            BimShadyLogger.LogError("Invalid comprehensive plan payload");
            return new RevitApiResponse { Success = false, Error = "Invalid comprehensive plan payload" };
        }

        BimShadyLogger.Log($"Plan parsed: {plan.Levels.Count} levels, Project: {plan.Project?.Name ?? "unnamed"}");

        var results = new
        {
            walls = new List<object>(),
            rooms = new List<object>(),
            doors = new List<object>(),
            fixtures = new List<object>(),
            tags = new List<object>(),
            views = new List<object>(),
            errors = new List<string>()
        };

        // Get or create level
        var level = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .FirstOrDefault() as Level;

        if (level == null)
            return new RevitApiResponse { Success = false, Error = "No level found in project" };

        // Get wall type
        var wallType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .FirstOrDefault() as WallType;

        if (wallType == null)
            return new RevitApiResponse { Success = false, Error = "No wall type found" };

        // Track created elements for tagging
        var wallMap = new Dictionary<string, Wall>();
        var createdRooms = new List<Room>();
        var createdDoors = new List<FamilyInstance>();
        var createdFixtures = new List<FamilyInstance>();
        ViewPlan? newFloorPlan = null;

        using (var trans = new Transaction(doc, "Import Comprehensive Floor Plan"))
        {
            trans.Start();
            BimShadyLogger.Log("Transaction started: Import Comprehensive Floor Plan");

            // 1. Create a new floor plan view for this import
            BimShadyLogger.Log("Creating new floor plan view...");
            var viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

            if (viewFamilyType != null)
            {
                newFloorPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
                newFloorPlan.Name = $"Imported Plan - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
                BimShadyLogger.LogElementCreated("FloorPlan View", newFloorPlan.Id.Value, newFloorPlan.Name);
                results.views.Add(new
                {
                    type = "FloorPlan",
                    viewId = newFloorPlan.Id.Value,
                    name = newFloorPlan.Name
                });
            }
            else
            {
                BimShadyLogger.LogWarning("No floor plan view family type found");
            }

            foreach (var floorLevel in plan.Levels)
            {
                // Create walls from all rooms
                if (floorLevel.Rooms != null)
                {
                    foreach (var roomDef in floorLevel.Rooms)
                    {
                        if (roomDef.Walls != null)
                        {
                            foreach (var wallDef in roomDef.Walls)
                            {
                                // Skip if wall already created (shared walls)
                                if (wallMap.ContainsKey(wallDef.Id))
                                    continue;

                                try
                                {
                                    var startPt = new XYZ(wallDef.Start!.X, wallDef.Start.Y, 0);
                                    var endPt = new XYZ(wallDef.End!.X, wallDef.End.Y, 0);
                                    var line = Line.CreateBound(startPt, endPt);

                                    var wall = Wall.Create(doc, line, wallType.Id, level.Id, wallDef.Height, 0, false, false);
                                    wallMap[wallDef.Id] = wall;

                                    results.walls.Add(new
                                    {
                                        wallId = wallDef.Id,
                                        revitId = wall.Id.Value,
                                        type = wallDef.Type,
                                        hasOpening = wallDef.HasOpening
                                    });
                                }
                                catch (Exception ex)
                                {
                                    results.errors.Add($"Wall {wallDef.Id}: {ex.Message}");
                                }
                            }
                        }

                        // Create room at bounding box center
                        if (roomDef.BoundingBox?.Origin != null)
                        {
                            try
                            {
                                var centerX = roomDef.BoundingBox.Origin.X + roomDef.BoundingBox.Width / 2;
                                var centerY = roomDef.BoundingBox.Origin.Y + roomDef.BoundingBox.Depth / 2;

                                var room = doc.Create.NewRoom(level, new UV(centerX, centerY));
                                if (room != null)
                                {
                                    room.Name = roomDef.Name;
                                    createdRooms.Add(room);

                                    results.rooms.Add(new
                                    {
                                        roomId = roomDef.Id,
                                        revitId = room.Id.Value,
                                        name = roomDef.Name,
                                        type = roomDef.Type,
                                        area = room.Area
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                results.errors.Add($"Room {roomDef.Name}: {ex.Message}");
                            }
                        }
                    }
                }

                // Create doors
                if (floorLevel.Doors != null)
                {
                    // Get door family type
                    var doorType = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .FirstOrDefault() as FamilySymbol;

                    if (doorType != null && !doorType.IsActive)
                        doorType.Activate();

                    foreach (var doorDef in floorLevel.Doors)
                    {
                        try
                        {
                            if (doorType != null && wallMap.TryGetValue(doorDef.HostWall, out var hostWall))
                            {
                                var doorPt = new XYZ(doorDef.Position!.X, doorDef.Position.Y, 0);
                                var door = doc.Create.NewFamilyInstance(doorPt, doorType, hostWall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                createdDoors.Add(door);

                                results.doors.Add(new
                                {
                                    doorId = doorDef.Id,
                                    revitId = door.Id.Value,
                                    hostWall = doorDef.HostWall,
                                    width = doorDef.Width,
                                    height = doorDef.Height
                                });
                            }
                            else
                            {
                                results.errors.Add($"Door {doorDef.Id}: Host wall {doorDef.HostWall} not found or no door family");
                            }
                        }
                        catch (Exception ex)
                        {
                            results.errors.Add($"Door {doorDef.Id}: {ex.Message}");
                        }
                    }
                }

                // Place fixtures (skip if family not found)
                if (floorLevel.Rooms != null)
                {
                    foreach (var roomDef in floorLevel.Rooms)
                    {
                        if (roomDef.Fixtures != null)
                        {
                            foreach (var fixture in roomDef.Fixtures)
                            {
                                try
                                {
                                    // Try to find the family by name
                                    var familySymbol = new FilteredElementCollector(doc)
                                        .OfClass(typeof(FamilySymbol))
                                        .Cast<FamilySymbol>()
                                        .FirstOrDefault(fs => fs.Name.Contains(fixture.FamilyName) ||
                                                              fs.Family.Name.Contains(fixture.FamilyName) ||
                                                              fs.Name.Contains(fixture.Type));

                                    if (familySymbol != null)
                                    {
                                        if (!familySymbol.IsActive)
                                            familySymbol.Activate();

                                        var pt = new XYZ(fixture.Position!.X, fixture.Position.Y, 0);
                                        var instance = doc.Create.NewFamilyInstance(pt, familySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                        createdFixtures.Add(instance);

                                        results.fixtures.Add(new
                                        {
                                            type = fixture.Type,
                                            familyName = fixture.FamilyName,
                                            revitId = instance.Id.Value,
                                            placed = true
                                        });
                                    }
                                    else
                                    {
                                        results.fixtures.Add(new
                                        {
                                            type = fixture.Type,
                                            familyName = fixture.FamilyName,
                                            placed = false,
                                            reason = "Family not found in project"
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    results.errors.Add($"Fixture {fixture.Type}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }

            // 2. Add tags to all elements in the new floor plan view
            if (newFloorPlan != null)
            {
                // Tag rooms with area
                foreach (var room in createdRooms)
                {
                    try
                    {
                        var roomLocation = (room.Location as LocationPoint)?.Point;
                        if (roomLocation == null)
                        {
                            // Use room's center
                            var bbox = room.get_BoundingBox(null);
                            if (bbox != null)
                            {
                                roomLocation = (bbox.Min + bbox.Max) / 2;
                            }
                        }

                        if (roomLocation != null)
                        {
                            var tagPt = new UV(roomLocation.X, roomLocation.Y);
                            var roomTag = doc.Create.NewRoomTag(
                                new LinkElementId(room.Id),
                                tagPt,
                                newFloorPlan.Id
                            );
                            if (roomTag != null)
                            {
                                results.tags.Add(new
                                {
                                    type = "RoomTag",
                                    elementId = room.Id.Value,
                                    tagId = roomTag.Id.Value,
                                    roomName = room.Name,
                                    areaSqFt = room.Area
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results.errors.Add($"Room tag for {room.Name}: {ex.Message}");
                    }
                }

                // Tag doors
                var doorTagType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_DoorTags)
                    .FirstOrDefault() as FamilySymbol;

                if (doorTagType != null)
                {
                    foreach (var door in createdDoors)
                    {
                        try
                        {
                            var doorLocation = (door.Location as LocationPoint)?.Point;
                            if (doorLocation != null)
                            {
                                var tag = IndependentTag.Create(doc, newFloorPlan.Id, new Reference(door), false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, doorLocation);
                                if (tag != null)
                                {
                                    results.tags.Add(new
                                    {
                                        type = "DoorTag",
                                        elementId = door.Id.Value,
                                        tagId = tag.Id.Value
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            results.errors.Add($"Door tag: {ex.Message}");
                        }
                    }
                }

                // Tag walls
                var wallTagType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_WallTags)
                    .FirstOrDefault() as FamilySymbol;

                if (wallTagType != null)
                {
                    foreach (var wall in wallMap.Values)
                    {
                        try
                        {
                            var wallLocation = wall.Location as LocationCurve;
                            if (wallLocation != null)
                            {
                                var curve = wallLocation.Curve;
                                var midPoint = curve.Evaluate(0.5, true);
                                var tag = IndependentTag.Create(doc, newFloorPlan.Id, new Reference(wall), false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, midPoint);
                                if (tag != null)
                                {
                                    results.tags.Add(new
                                    {
                                        type = "WallTag",
                                        elementId = wall.Id.Value,
                                        tagId = tag.Id.Value
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            results.errors.Add($"Wall tag: {ex.Message}");
                        }
                    }
                }

                // Tag fixtures (generic annotation tags)
                foreach (var fixture in createdFixtures)
                {
                    try
                    {
                        var fixtureLocation = (fixture.Location as LocationPoint)?.Point;
                        if (fixtureLocation != null)
                        {
                            var tag = IndependentTag.Create(doc, newFloorPlan.Id, new Reference(fixture), false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, fixtureLocation);
                            if (tag != null)
                            {
                                results.tags.Add(new
                                {
                                    type = "FixtureTag",
                                    elementId = fixture.Id.Value,
                                    tagId = tag.Id.Value
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results.errors.Add($"Fixture tag: {ex.Message}");
                    }
                }
            }

            // 3. Create a 3D view
            var view3DType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

            if (view3DType != null)
            {
                try
                {
                    var view3D = View3D.CreateIsometric(doc, view3DType.Id);
                    view3D.Name = $"3D View - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
                    results.views.Add(new
                    {
                        type = "3D",
                        viewId = view3D.Id.Value,
                        name = view3D.Name
                    });
                }
                catch (Exception ex)
                {
                    results.errors.Add($"3D View creation: {ex.Message}");
                }
            }

            trans.Commit();
            BimShadyLogger.LogSuccess("Transaction committed successfully");
        }

        var summaryMessage = $"Imported {results.walls.Count} walls, {results.rooms.Count} rooms, {results.doors.Count} doors, {results.fixtures.Count} fixtures, created {results.tags.Count} tags";
        BimShadyLogger.LogSuccess(summaryMessage);
        if (results.errors.Count > 0)
        {
            BimShadyLogger.LogWarning($"Encountered {results.errors.Count} errors during import");
            foreach (var error in results.errors)
            {
                BimShadyLogger.LogWarning($"  - {error}");
            }
        }

        return new RevitApiResponse
        {
            Success = true,
            Data = new
            {
                message = summaryMessage,
                projectName = plan.Project?.Name,
                results = results
            }
        };
    }
}

public class WallData
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; } = 0;
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; } = 0;
    public double Height { get; set; } = 10.0; // Default 10 feet
}

public class RevitApiRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = "";
    public Dictionary<string, object>? Parameters { get; set; }
}

public class RevitApiResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
}
