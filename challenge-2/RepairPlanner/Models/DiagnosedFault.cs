using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Input model: represents a fault diagnosed by the Fault Diagnosis Agent.
/// </summary>
public sealed class DiagnosedFault
{
    /// <summary>
    /// Unique identifier for the fault record.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The type of fault (e.g., "curing_temperature_excessive", "building_drum_vibration").
    /// </summary>
    [JsonPropertyName("faultType")]
    [JsonProperty("faultType")]
    public string FaultType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the machine experiencing the fault.
    /// </summary>
    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    /// <summary>
    /// Name or description of the machine.
    /// </summary>
    [JsonPropertyName("machineName")]
    [JsonProperty("machineName")]
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Severity of the fault (e.g., "critical", "high", "medium", "low").
    /// </summary>
    [JsonPropertyName("severity")]
    [JsonProperty("severity")]
    public string Severity { get; set; } = "medium";

    /// <summary>
    /// Detailed description of the fault.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the fault was detected.
    /// </summary>
    [JsonPropertyName("detectedAt")]
    [JsonProperty("detectedAt")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Confidence score from the diagnosis (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    [JsonProperty("confidence")]
    public double Confidence { get; set; } = 0.95;

    /// <summary>
    /// Optional: recommended repair type (e.g., "corrective", "preventive", "emergency").
    /// </summary>
    [JsonPropertyName("recommendedRepairType")]
    [JsonProperty("recommendedRepairType")]
    public string? RecommendedRepairType { get; set; }
}
