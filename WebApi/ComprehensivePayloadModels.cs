using Newtonsoft.Json;

namespace BimShady.WebApi;

/// <summary>
/// Comprehensive floor plan schema with levels, rooms, doors, fixtures
/// </summary>
public class ComprehensiveFloorPlan
{
    [JsonProperty("project")]
    public ProjectInfo? Project { get; set; }

    [JsonProperty("levels")]
    public List<FloorLevel>? Levels { get; set; }

    [JsonProperty("metadata")]
    public PlanMetadata? Metadata { get; set; }
}

public class ProjectInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("units")]
    public string Units { get; set; } = "feet";

    [JsonProperty("totalDimensions")]
    public Dimensions? TotalDimensions { get; set; }

    [JsonProperty("totalArea")]
    public double TotalArea { get; set; }
}

public class Dimensions
{
    [JsonProperty("width")]
    public double Width { get; set; }

    [JsonProperty("depth")]
    public double Depth { get; set; }
}

public class FloorLevel
{
    [JsonProperty("name")]
    public string Name { get; set; } = "Level 1";

    [JsonProperty("elevation")]
    public double Elevation { get; set; } = 0;

    [JsonProperty("ceilingHeight")]
    public double CeilingHeight { get; set; } = 9;

    [JsonProperty("rooms")]
    public List<RoomDefinition>? Rooms { get; set; }

    [JsonProperty("doors")]
    public List<DoorDefinition>? Doors { get; set; }

    [JsonProperty("windows")]
    public List<WindowDefinition>? Windows { get; set; }
}

public class RoomDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("type")]
    public string Type { get; set; } = "";

    [JsonProperty("boundingBox")]
    public BoundingBox? BoundingBox { get; set; }

    [JsonProperty("area")]
    public double Area { get; set; }

    [JsonProperty("walls")]
    public List<WallDefinition>? Walls { get; set; }

    [JsonProperty("fixtures")]
    public List<FixtureDefinition>? Fixtures { get; set; }
}

public class BoundingBox
{
    [JsonProperty("origin")]
    public Point3D? Origin { get; set; }

    [JsonProperty("width")]
    public double Width { get; set; }

    [JsonProperty("depth")]
    public double Depth { get; set; }
}

public class Point3D
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }

    [JsonProperty("z")]
    public double Z { get; set; } = 0;
}

public class WallDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("start")]
    public Point3D? Start { get; set; }

    [JsonProperty("end")]
    public Point3D? End { get; set; }

    [JsonProperty("thickness")]
    public double Thickness { get; set; } = 0.5;

    [JsonProperty("height")]
    public double Height { get; set; } = 9;

    [JsonProperty("type")]
    public string Type { get; set; } = "interior"; // interior, exterior

    [JsonProperty("hasOpening")]
    public bool HasOpening { get; set; } = false;
}

public class DoorDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("type")]
    public string Type { get; set; } = "Single_Hinged";

    [JsonProperty("roomFrom")]
    public string RoomFrom { get; set; } = "";

    [JsonProperty("roomTo")]
    public string RoomTo { get; set; } = "";

    [JsonProperty("hostWall")]
    public string HostWall { get; set; } = "";

    [JsonProperty("position")]
    public Point3D? Position { get; set; }

    [JsonProperty("width")]
    public double Width { get; set; } = 3;

    [JsonProperty("height")]
    public double Height { get; set; } = 7;

    [JsonProperty("swing")]
    public string Swing { get; set; } = "";

    [JsonProperty("swingAngle")]
    public double SwingAngle { get; set; } = 90;

    [JsonProperty("handedness")]
    public string Handedness { get; set; } = "left";
}

public class WindowDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("hostWall")]
    public string HostWall { get; set; } = "";

    [JsonProperty("position")]
    public Point3D? Position { get; set; }

    [JsonProperty("width")]
    public double Width { get; set; } = 3;

    [JsonProperty("height")]
    public double Height { get; set; } = 4;

    [JsonProperty("sillHeight")]
    public double SillHeight { get; set; } = 3;
}

public class FixtureDefinition
{
    [JsonProperty("type")]
    public string Type { get; set; } = "";

    [JsonProperty("position")]
    public Point3D? Position { get; set; }

    [JsonProperty("rotation")]
    public double Rotation { get; set; } = 0;

    [JsonProperty("familyName")]
    public string FamilyName { get; set; } = "";
}

public class PlanMetadata
{
    [JsonProperty("createdDate")]
    public string CreatedDate { get; set; } = "";

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0";

    [JsonProperty("scale")]
    public string Scale { get; set; } = "";

    [JsonProperty("coordinateSystem")]
    public string CoordinateSystem { get; set; } = "origin-bottom-left";

    [JsonProperty("sourceFile")]
    public string SourceFile { get; set; } = "";
}
