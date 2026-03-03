using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairPlanner;
using RepairPlanner.Models;
using RepairPlanner.Services;

// ============================================================================
// Dependency Injection Setup
// 
// Like Python's dependency injection frameworks, we register services
// in a container and resolve them at runtime. This wires up all components.
// ============================================================================
var services = new ServiceCollection();

// Configure logging with colored output and timestamps
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

// Azure AI Project Client (uses DefaultAzureCredential: managed identity or az login)
var aiProjectEndpoint = GetRequiredEnvVar("AZURE_AI_PROJECT_ENDPOINT");
services.AddSingleton(_ => new AIProjectClient(new Uri(aiProjectEndpoint), new DefaultAzureCredential()));

// Cosmos DB Options
var cosmosOptions = new CosmosDbOptions
{
    Endpoint = GetRequiredEnvVar("COSMOS_ENDPOINT"),
    Key = GetRequiredEnvVar("COSMOS_KEY"),
    DatabaseName = GetRequiredEnvVar("COSMOS_DATABASE_NAME"),
};
services.AddSingleton(cosmosOptions);

// Cosmos DB Client (with Gateway mode for simplicity)
services.AddSingleton(_ =>
{
    var clientOptions = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
    };
    return new CosmosClient(cosmosOptions.Endpoint, cosmosOptions.Key, clientOptions);
});

// Cosmos DB Service (encapsulates queries and writes)
services.AddSingleton<CosmosDbService>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosDbService>>();
    return new CosmosDbService(
        client,
        cosmosOptions.DatabaseName,
        logger,
        cosmosOptions.TechniciansContainerName,
        cosmosOptions.PartsInventoryContainerName,
        cosmosOptions.WorkOrdersContainerName);
});

// Fault Mapping Service (hardcoded fault → skills/parts mappings)
services.AddSingleton<IFaultMappingService, FaultMappingService>();

// Repair Planner Agent (main orchestration class)
services.AddSingleton(sp => new RepairPlannerAgent(
    sp.GetRequiredService<AIProjectClient>(),
    sp.GetRequiredService<CosmosDbService>(),
    sp.GetRequiredService<IFaultMappingService>(),
    GetRequiredEnvVar("MODEL_DEPLOYMENT_NAME"),
    sp.GetRequiredService<ILogger<RepairPlannerAgent>>()));

// ============================================================================
// Initialize and Run the Workflow
// ============================================================================

// "await using" ensures proper disposal of resources (like Python's "async with")
await using var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

logger.LogInformation("Starting Repair Planner Agent workflow...");

try
{
    // Step 1: Get the Repair Planner Agent from the container
    var planner = provider.GetRequiredService<RepairPlannerAgent>();

    // Step 2: Ensure the agent version is registered with Azure AI Foundry
    logger.LogInformation("Registering agent with Azure AI Foundry...");
    await planner.EnsureAgentVersionAsync();

    // Step 3: Create a sample diagnosed fault for demonstration
    var sampleFault = new DiagnosedFault
    {
        Id = Guid.NewGuid().ToString(),
        MachineId = "tire-curer-001",
        MachineName = "Tire Curing Press - Line 1",
        FaultType = "curing_temperature_excessive",
        Severity = "high",
        Description = "Temperature readings are 25°C above setpoint, indicating potential thermocouple drift or failing heater element.",
        DetectedAt = DateTime.UtcNow,
        Confidence = 0.92
    };

    logger.LogInformation(
        "Processing sampled fault: machine={Machine}, type={FaultType}, severity={Severity}",
        sampleFault.MachineId,
        sampleFault.FaultType,
        sampleFault.Severity);

    // Step 4: Execute the repair planning workflow
    var workOrder = await planner.PlanAndCreateWorkOrderAsync(sampleFault);

    logger.LogInformation(
        "✓ Workflow completed successfully. Work order: {WorkOrderNumber} (id={Id})",
        workOrder.WorkOrderNumber,
        workOrder.Id);

    // Step 5: Display the generated work order
    Console.WriteLine("\n" + new string('=', 80));
    Console.WriteLine("GENERATED WORK ORDER");
    Console.WriteLine(new string('=', 80) + "\n");
    Console.WriteLine(JsonSerializer.Serialize(workOrder, new JsonSerializerOptions { WriteIndented = true }));
}
catch (Exception ex)
{
    logger.LogError(ex, "Repair planning workflow failed.");
    Console.WriteLine("\nError Details:");
    Console.WriteLine(ex.Message);
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
    Environment.ExitCode = 1;
}

// Helper function: Load required environment variables (throws if missing)
// Similar to Python's os.environ.get() but ensures variable exists
static string GetRequiredEnvVar(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Missing required environment variable: {name}");
