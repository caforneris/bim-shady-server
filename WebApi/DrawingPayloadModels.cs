using Newtonsoft.Json;

namespace BimShady.WebApi;

/// <summary>
/// Models matching the drawing application JSON schema
/// </summary>
public class DrawingPayload
{
    [JsonProperty("walls")]
    public List<DrawingWall>? Walls { get; set; }

    [JsonProperty("rooms")]
    public List<DrawingRoom>? Rooms { get; set; }

    /// <summary>
    /// Scale factor to convert drawing units to feet.
    /// Default assumes 1 drawing unit = 1 inch, so divide by 12 for feet.
    /// Set to 1.0 if drawing units are already in feet.
    /// </summary>
    [JsonProperty("scale")]
    public double Scale { get; set; } = 1.0 / 12.0; // Default: inches to feet

    /// <summary>
    /// Default wall height in feet
    /// </summary>
    [JsonProperty("wallHeight")]
    public double WallHeight { get; set; } = 10.0;
}

public class DrawingWall
{
    [JsonProperty("wall_id")]
    public string WallId { get; set; } = "";

    [JsonProperty("start_point")]
    public DrawingPoint StartPoint { get; set; } = new();

    [JsonProperty("end_point")]
    public DrawingPoint EndPoint { get; set; } = new();

    [JsonProperty("height")]
    public double? Height { get; set; }
}

public class DrawingRoom
{
    [JsonProperty("room_name")]
    public string RoomName { get; set; } = "";

    [JsonProperty("center_point")]
    public DrawingPoint CenterPoint { get; set; } = new();
}

public class DrawingPoint
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }

    [JsonProperty("z")]
    public double Z { get; set; } = 0;
}
