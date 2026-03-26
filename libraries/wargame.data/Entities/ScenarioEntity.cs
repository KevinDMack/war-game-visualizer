using System.ComponentModel.DataAnnotations;

namespace WargameData.Entities;

/// <summary>
/// EF Core entity representing a wargame scenario, corresponding to the
/// <c>Scenario</c> message defined in scenario.proto.
/// </summary>
public class ScenarioEntity
{
    /// <summary>Unique identifier for the scenario (UUID string).</summary>
    [Key]
    [MaxLength(36)]
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>Human-readable name of the scenario.</summary>
    [MaxLength(256)]
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>Short narrative summary of the scenario.</summary>
    [MaxLength(2048)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Geographic bounding box for the scenario on the map.</summary>
    public BoundingBoxEntity BoundingBox { get; set; } = new();
}
