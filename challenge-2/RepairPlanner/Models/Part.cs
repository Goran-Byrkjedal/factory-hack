using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents an inventory part available for repairs.
/// Read from Cosmos DB PartsInventory container.
/// </summary>
public sealed class Part
{
    /// <summary>
    /// Unique identifier (partition key: category).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Part number (SKU).
    /// </summary>
    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the part.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Part category (e.g., "heating", "bearings", "seals", "sensors").
    /// Used as partition key in Cosmos DB.
    /// </summary>
    [JsonPropertyName("category")]
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the part.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Current quantity available in inventory.
    /// </summary>
    [JsonPropertyName("quantityAvailable")]
    [JsonProperty("quantityAvailable")]
    public int QuantityAvailable { get; set; } = 0;

    /// <summary>
    /// Reorder threshold - alerts when stock drops below this.
    /// </summary>
    [JsonPropertyName("reorderLevel")]
    [JsonProperty("reorderLevel")]
    public int ReorderLevel { get; set; } = 5;

    /// <summary>
    /// Unit cost of the part.
    /// </summary>
    [JsonPropertyName("unitCost")]
    [JsonProperty("unitCost")]
    public decimal UnitCost { get; set; } = 0m;

    /// <summary>
    /// Supplier information.
    /// </summary>
    [JsonPropertyName("supplier")]
    [JsonProperty("supplier")]
    public string? Supplier { get; set; }

    /// <summary>
    /// Lead time for ordering new stock (in days).
    /// </summary>
    [JsonPropertyName("leadTimeDays")]
    [JsonProperty("leadTimeDays")]
    public int LeadTimeDays { get; set; } = 7;

    /// <summary>
    /// Whether the part is currently in stock and ready for use.
    /// </summary>
    [JsonPropertyName("inStock")]
    [JsonProperty("inStock")]
    public bool InStock => QuantityAvailable > 0;

    /// <summary>
    /// List of machines or equipment this part is used in.
    /// </summary>
    [JsonPropertyName("applicableMachines")]
    [JsonProperty("applicableMachines")]
    public List<string> ApplicableMachines { get; set; } = new();
}
