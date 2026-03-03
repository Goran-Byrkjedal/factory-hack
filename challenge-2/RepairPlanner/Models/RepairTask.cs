using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a single repair task within a work order.
/// Includes sequence, timeline, required skills, and safety considerations.
/// </summary>
public sealed class RepairTask
{
    /// <summary>
    /// Sequence number for ordering tasks (1, 2, 3, ...).
    /// </summary>
    [JsonPropertyName("sequence")]
    [JsonProperty("sequence")]
    public int Sequence { get; set; }

    /// <summary>
    /// Brief title of the repair task.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what needs to be done.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Estimated duration in minutes as an integer.
    /// Examples: 30, 90, 120 (not "30 minutes" or strings).
    /// </summary>
    [JsonPropertyName("estimatedDurationMinutes")]
    [JsonProperty("estimatedDurationMinutes")]
    public int EstimatedDurationMinutes { get; set; }

    /// <summary>
    /// List of technical skills required to perform this task.
    /// Examples: "plc_programming", "bearing_replacement", "temperature_control".
    /// </summary>
    [JsonPropertyName("requiredSkills")]
    [JsonProperty("requiredSkills")]
    public List<string> RequiredSkills { get; set; } = new();

    /// <summary>
    /// Safety notes or warnings for this task.
    /// </summary>
    [JsonPropertyName("safetyNotes")]
    [JsonProperty("safetyNotes")]
    public string? SafetyNotes { get; set; }

    /// <summary>
    /// Optional: parts IDs required for this specific task.
    /// </summary>
    [JsonPropertyName("requiredPartIds")]
    [JsonProperty("requiredPartIds")]
    public List<string>? RequiredPartIds { get; set; }

    /// <summary>
    /// Optional: dependencies on other tasks (task sequence numbers).
    /// Empty if no dependencies.
    /// </summary>
    [JsonPropertyName("dependsOnSequences")]
    [JsonProperty("dependsOnSequences")]
    public List<int>? DependsOnSequences { get; set; }

    /// <summary>
    /// Status of the task (e.g., "pending", "in_progress", "completed", "blocked").
    /// </summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "pending";
}
