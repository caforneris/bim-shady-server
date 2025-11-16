using Newtonsoft.Json;

namespace BimShady.WebApi;

/// <summary>
/// Schema for sketch/drawing application output
/// Simple format: walls, doors (snapped to walls), rooms (with labels)
/// </summary>
public class SketchPayload
{
    [JsonProperty("walls")]
    public List<SketchWall>? Walls { get; set; }

    [JsonProperty("doors")]
    public List<SketchDoor>? Doors { get; set; }

    [JsonProperty("rooms")]
    public List<SketchRoom>? Rooms { get; set; }

    [JsonProperty("wall_height")]
    public double WallHeight { get; set; } = 10.0; // Default 10 feet
}

public class SketchWall
{
    [JsonProperty("wall_id")]
    public string WallId { get; set; } = "";

    [JsonProperty("start_point")]
    public SketchPoint? StartPoint { get; set; }

    [JsonProperty("end_point")]
    public SketchPoint? EndPoint { get; set; }
}

public class SketchDoor
{
    [JsonProperty("start")]
    public SketchPoint? Start { get; set; }

    [JsonProperty("end")]
    public SketchPoint? End { get; set; }

    [JsonProperty("original")]
    public SketchDoorOriginal? Original { get; set; }
}

public class SketchDoorOriginal
{
    [JsonProperty("start")]
    public SketchPoint? Start { get; set; }

    [JsonProperty("end")]
    public SketchPoint? End { get; set; }
}

public class SketchRoom
{
    [JsonProperty("room_name")]
    public string RoomName { get; set; } = "";

    [JsonProperty("center_point")]
    public SketchPoint? CenterPoint { get; set; }
}

public class SketchPoint
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }
}
