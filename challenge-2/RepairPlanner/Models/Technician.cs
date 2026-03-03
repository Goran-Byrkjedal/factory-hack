using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a technician available to perform repairs.
/// Read from Cosmos DB Technicians container.
/// </summary>
public sealed class Technician
{
    /// <summary>
    /// Unique identifier (partition key: department).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Technician's full name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Department or team the technician belongs to.
    /// Used as partition key in Cosmos DB.
    /// </summary>
    [JsonPropertyName("department")]
    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Email address for contact.
    /// </summary>
    [JsonPropertyName("email")]
    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Phone number for contact.
    /// </summary>
    [JsonPropertyName("phone")]
    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// List of technical skills the technician possesses.
    /// Examples: "tire_curing_press", "temperature_control", "plc_programming".
    /// </summary>
    [JsonPropertyName("skills")]
    [JsonProperty("skills")]
    public List<string> Skills { get; set; } = new();

    /// <summary>
    /// Experience level (e.g., "junior", "mid", "senior", "expert").
    /// </summary>
    [JsonPropertyName("experienceLevel")]
    [JsonProperty("experienceLevel")]
    public string ExperienceLevel { get; set; } = "mid";

    /// <summary>
    /// Current availability status (e.g., "available", "on_job", "on_leave").
    /// </summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "available";

    /// <summary>
    /// Estimated time when technician becomes available again (if status != "available").
    /// </summary>
    [JsonPropertyName("availableAt")]
    [JsonProperty("availableAt")]
    public DateTime? AvailableAt { get; set; }

    /// <summary>
    /// Maximum hours the technician can work per day.
    /// </summary>
    [JsonPropertyName("maxHoursPerDay")]
    [JsonProperty("maxHoursPerDay")]
    public int MaxHoursPerDay { get; set; } = 8;

    /// <summary>
    /// Certification or qualification details.
    /// </summary>
    [JsonPropertyName("certifications")]
    [JsonProperty("certifications")]
    public List<string> Certifications { get; set; } = new();
}
