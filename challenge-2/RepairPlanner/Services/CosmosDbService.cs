using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

/// <summary>
/// Encapsulates Cosmos DB queries and writes needed by the Repair Planner Agent.
/// Handles:
/// - Querying available technicians by required skills
/// - Fetching parts from inventory
/// - Creating work orders
/// </summary>
public sealed class CosmosDbService
{
    private readonly ILogger<CosmosDbService> _logger;
    private readonly Container _technicians;
    private readonly Container _partsInventory;
    private readonly Container _workOrders;

    /// <summary>
    /// Initializes the Cosmos DB service with containers.
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="techniciansContainerName">Technicians container name (default: "Technicians").</param>
    /// <param name="partsInventoryContainerName">Parts inventory container name (default: "PartsInventory").</param>
    /// <param name="workOrdersContainerName">Work orders container name (default: "WorkOrders").</param>
    /// <exception cref="ArgumentNullException">Thrown if cosmosClient or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown if databaseName is null or whitespace.</exception>
    public CosmosDbService(
        CosmosClient cosmosClient,
        string databaseName,
        ILogger<CosmosDbService> logger,
        string techniciansContainerName = "Technicians",
        string partsInventoryContainerName = "PartsInventory",
        string workOrdersContainerName = "WorkOrders")
    {
        if (cosmosClient is null) 
            throw new ArgumentNullException(nameof(cosmosClient));
        if (string.IsNullOrWhiteSpace(databaseName)) 
            throw new ArgumentException("Database name is required.", nameof(databaseName));
        if (logger is null) 
            throw new ArgumentNullException(nameof(logger));

        _logger = logger;
        _technicians = cosmosClient.GetContainer(databaseName, techniciansContainerName);
        _partsInventory = cosmosClient.GetContainer(databaseName, partsInventoryContainerName);
        _workOrders = cosmosClient.GetContainer(databaseName, workOrdersContainerName);
    }

    /// <summary>
    /// Queries available technicians ranked by skill match.
    /// Returns technicians who match at least one required skill, sorted by match count (descending).
    /// </summary>
    /// <param name="requiredSkills">List of required skills.</param>
    /// <param name="department">Optional: filter by department (uses partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of technicians sorted by skill match, or empty list if none found.</returns>
    public async Task<IReadOnlyList<Technician>> QueryAvailableTechniciansBySkillsAsync(
        IEnumerable<string> requiredSkills,
        string? department = null,
        CancellationToken cancellationToken = default)
    {
        requiredSkills ??= Array.Empty<string>();

        var skills = requiredSkills
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Build query: find technicians with status="available"
        QueryDefinition query = string.IsNullOrWhiteSpace(department)
            ? new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", "available")
            : new QueryDefinition("SELECT * FROM c WHERE c.status = @status AND c.department = @department")
                .WithParameter("@status", "available")
                .WithParameter("@department", department);

        var results = new List<Technician>();

        try
        {
            QueryRequestOptions? requestOptions = null;
            // Use partition key for department filter to optimize queries
            if (!string.IsNullOrWhiteSpace(department))
            {
                requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(department) };
            }

            using FeedIterator<Technician> iterator = _technicians.GetItemQueryIterator<Technician>(
                query,
                requestOptions: requestOptions);

            while (iterator.HasMoreResults)
            {
                FeedResponse<Technician> page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }

            // If no skills specified, return all available technicians
            if (skills.Length == 0)
            {
                _logger.LogInformation(
                    "Found {TechnicianCount} available technicians (department={Department})",
                    results.Count,
                    department ?? "<any>");
                return results;
            }

            // Rank technicians by skill match count
            var ranked = results
                .Select(t => new
                {
                    Technician = t,
                    MatchCount = t.Skills.Count(s => skills.Contains(s, StringComparer.OrdinalIgnoreCase))
                })
                .Where(x => x.MatchCount > 0) // Only return technicians with at least one matching skill
                .OrderByDescending(x => x.MatchCount) // Most matched skills first
                .ThenBy(x => x.Technician.Name, StringComparer.OrdinalIgnoreCase) // Secondary sort: alphabetical
                .Select(x => x.Technician)
                .ToList();

            _logger.LogInformation(
                "Found {TechnicianCount} available technicians matching at least one required skill [{Skills}] (department={Department})",
                ranked.Count,
                string.Join(", ", skills),
                department ?? "<any>");

            return ranked;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Technicians container not found or database missing.");
            return Array.Empty<Technician>();
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error querying technicians. StatusCode={StatusCode}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error querying technicians.");
            throw;
        }
    }

    /// <summary>
    /// Fetches parts from inventory by part numbers.
    /// Returns only parts that are in stock.
    /// </summary>
    /// <param name="partNumbers">List of part numbers (SKUs) to fetch.</param>
    /// <param name="category">Optional: filter by category (uses partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of parts, or empty list if none found or part numbers list is empty.</returns>
    public async Task<IReadOnlyList<Part>> GetPartsByPartNumbersAsync(
        IEnumerable<string> partNumbers,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (partNumbers is null) 
            throw new ArgumentNullException(nameof(partNumbers));

        var numbers = partNumbers
            .Where(pn => !string.IsNullOrWhiteSpace(pn))
            .Select(pn => pn.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (numbers.Length == 0)
        {
            _logger.LogInformation("No part numbers provided to fetch.");
            return Array.Empty<Part>();
        }

        // Build query to find parts by part numbers
        var queryText = "SELECT * FROM c WHERE ARRAY_CONTAINS(@partNumbers, c.partNumber) AND c.quantityAvailable > 0";
        if (!string.IsNullOrWhiteSpace(category))
        {
            queryText += " AND c.category = @category";
        }

        var query = new QueryDefinition(queryText)
            .WithParameter("@partNumbers", numbers);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.WithParameter("@category", category);
        }

        var results = new List<Part>();

        try
        {
            QueryRequestOptions? requestOptions = null;
            // Use partition key for category filter to optimize queries
            if (!string.IsNullOrWhiteSpace(category))
            {
                requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(category) };
            }

            using FeedIterator<Part> iterator = _partsInventory.GetItemQueryIterator<Part>(
                query,
                requestOptions: requestOptions);

            while (iterator.HasMoreResults)
            {
                FeedResponse<Part> page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }

            _logger.LogInformation(
                "Fetched {PartCount} in-stock parts for partNumbers [{PartNumbers}] (category={Category})",
                results.Count,
                string.Join(", ", numbers),
                category ?? "<any>");

            return results;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Parts inventory container not found or database missing.");
            return Array.Empty<Part>();
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error fetching parts inventory. StatusCode={StatusCode}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching parts inventory.");
            throw;
        }
    }

    /// <summary>
    /// Creates a new work order in Cosmos DB.
    /// Auto-generates ID and work order number if not provided.
    /// </summary>
    /// <param name="workOrder">The work order to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created work order with auto-generated fields populated.</returns>
    /// <exception cref="ArgumentNullException">Thrown if workOrder is null.</exception>
    public async Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        if (workOrder is null) 
            throw new ArgumentNullException(nameof(workOrder));

        // Auto-generate ID if not provided
        if (string.IsNullOrWhiteSpace(workOrder.Id))
        {
            workOrder.Id = $"wo-{Guid.NewGuid():N}";
        }

        // Auto-generate work order number if not provided
        if (string.IsNullOrWhiteSpace(workOrder.WorkOrderNumber))
        {
            workOrder.WorkOrderNumber = $"WO-{DateTimeOffset.UtcNow:yyyyMMdd}-{Random.Shared.Next(1, 999):D3}";
        }

        // Set default status if not provided
        if (string.IsNullOrWhiteSpace(workOrder.Status))
        {
            workOrder.Status = "draft";
        }

        try
        {
            ItemResponse<WorkOrder> response = await _workOrders.CreateItemAsync(
                workOrder,
                partitionKey: new PartitionKey(workOrder.Status),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Created work order {WorkOrderNumber} (id={Id}, status={Status}, machineId={MachineId}, assignedTo={AssignedTo}, requestCharge={RequestCharge})",
                response.Resource.WorkOrderNumber,
                response.Resource.Id,
                response.Resource.Status,
                response.Resource.MachineId,
                response.Resource.AssignedTo ?? "<unassigned>",
                response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning(ex, "Work order id conflict. Id={Id}", workOrder.Id);
            throw;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error creating work order. StatusCode={StatusCode}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating work order.");
            throw;
        }
    }
}
