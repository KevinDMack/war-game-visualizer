using Microsoft.EntityFrameworkCore;

namespace WargameData.Entities;

/// <summary>
/// Owned entity representing a geographic bounding box, corresponding to
/// the <c>BoundingBox</c> message defined in scenario.proto.
/// Stored as columns within the parent Scenarios table.
/// </summary>
[Owned]
public class BoundingBoxEntity
{
    /// <summary>Minimum (south-west) latitude in decimal degrees.</summary>
    public double MinLatitude { get; set; }

    /// <summary>Minimum (south-west) longitude in decimal degrees.</summary>
    public double MinLongitude { get; set; }

    /// <summary>Maximum (north-east) latitude in decimal degrees.</summary>
    public double MaxLatitude { get; set; }

    /// <summary>Maximum (north-east) longitude in decimal degrees.</summary>
    public double MaxLongitude { get; set; }
}
