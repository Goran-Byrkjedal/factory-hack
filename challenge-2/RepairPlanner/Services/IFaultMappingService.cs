namespace RepairPlanner.Services;

/// <summary>
/// Service for mapping fault types to required skills and parts.
/// </summary>
public interface IFaultMappingService
{
    /// <summary>
    /// Gets the list of technical skills required to repair a given fault type.
    /// </summary>
    /// <param name="faultType">The fault type (e.g., "curing_temperature_excessive").</param>
    /// <returns>List of required skill names. Never null; returns ["general_maintenance"] if fault type is unknown.</returns>
    IReadOnlyList<string> GetRequiredSkills(string faultType);

    /// <summary>
    /// Gets the list of parts needed to repair a given fault type.
    /// </summary>
    /// <param name="faultType">The fault type (e.g., "curing_temperature_excessive").</param>
    /// <returns>List of part numbers. Never null; returns empty list if no parts needed or fault type is unknown.</returns>
    IReadOnlyList<string> GetRequiredParts(string faultType);
}
