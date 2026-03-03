namespace RepairPlanner.Services;

/// <summary>
/// Configuration for Cosmos DB access.
/// </summary>
public sealed class CosmosDbOptions
{
    /// <summary>
    /// Cosmos DB account endpoint URL.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Cosmos DB account key (primary or secondary).
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Cosmos DB database name (e.g., "factory-db").
    /// </summary>
    public required string DatabaseName { get; set; }

    /// <summary>
    /// Container name for technicians (default: "Technicians").
    /// Partition key: department
    /// </summary>
    public string TechniciansContainerName { get; set; } = "Technicians";

    /// <summary>
    /// Container name for parts inventory (default: "PartsInventory").
    /// Partition key: category
    /// </summary>
    public string PartsInventoryContainerName { get; set; } = "PartsInventory";

    /// <summary>
    /// Container name for work orders (default: "WorkOrders").
    /// Partition key: status
    /// </summary>
    public string WorkOrdersContainerName { get; set; } = "WorkOrders";
}
