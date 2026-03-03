using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents the usage of a part within a work order.
/// Captures which part is needed and in what quantity.
/// </summary>
public sealed class WorkOrderPartUsage
{
    /// <summary>
    /// The unique identifier of the part in the Parts Inventory.
    /// </summary>
    [JsonPropertyName("partId")]
    [JsonProperty("partId")]
    public string PartId { get; set; } = string.Empty;

    /// <summary>
    /// The part number (SKU) for reference.
    /// </summary>
    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the part.
    /// </summary>
    [JsonPropertyName("partName")]
    [JsonProperty("partName")]
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of this part needed for the work order.
    /// </summary>
    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Unit cost of the part at time of order.
    /// </summary>
    [JsonPropertyName("unitCost")]
    [JsonProperty("unitCost")]
    public decimal UnitCost { get; set; } = 0m;

    /// <summary>
    /// Total cost for this line item (quantity * unitCost).
    /// </summary>
    [JsonPropertyName("totalCost")]
    [JsonProperty("totalCost")]
    public decimal TotalCost => Quantity * UnitCost;

    /// <summary>
    /// Status of the part for this work order (e.g., "needed", "allocated", "used", "unavailable").
    /// </summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "needed";
}
