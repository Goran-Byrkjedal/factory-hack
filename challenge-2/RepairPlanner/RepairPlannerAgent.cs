using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using RepairPlanner.Services;

namespace RepairPlanner;

/// <summary>
/// Repair Planner Agent: generates work orders for diagnosed faults using the Foundry Agents SDK.
/// 
/// Workflow:
/// 1. Register agent definition with Azure AI Foundry
/// 2. Get required skills and parts from fault mapping
/// 3. Query Cosmos DB for available technicians and parts
/// 4. Build rich prompt with business context
/// 5. Invoke the agent to generate work order JSON
/// 6. Parse response with LLM-aware number handling
/// 7. Apply business logic defaults
/// 8. Save work order to Cosmos DB
/// </summary>
/// <remarks>
/// Uses a "primary constructor" - the parameters after the class name become fields
/// (similar to Python's __init__). This is equivalent to:
/// public sealed class RepairPlannerAgent {
///     private readonly AIProjectClient projectClient;
///     private readonly CosmosDbService cosmosDb;
///     ...
/// }
/// </remarks>
public sealed class RepairPlannerAgent(
    AIProjectClient projectClient,
    CosmosDbService cosmosDb,
    IFaultMappingService faultMapping,
    string modelDeploymentName,
    ILogger<RepairPlannerAgent> logger)
{
    private const string AgentName = "RepairPlannerAgent";

    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Your job is to generate a comprehensive repair plan with tasks, timeline, and resource allocation
        when faults are detected in production equipment.
        
        You will receive:
        - Diagnosed fault details (type, machine, severity)
        - Available technicians with their skills and experience
        - Available parts from inventory
        
        Generate a repair plan and return ONLY valid JSON matching this exact schema:
        {
          "workOrderNumber": "WO-...",
          "machineId": "...",
          "title": "Brief summary of repair",
          "description": "Detailed description of the work",
          "type": "corrective" | "preventive" | "emergency",
          "priority": "critical" | "high" | "medium" | "low",
          "status": "draft",
          "assignedTo": "technician-id or null",
          "estimatedDuration": 120,
          "partsUsed": [
            { "partId": "...", "partNumber": "...", "quantity": 1 }
          ],
          "tasks": [
            {
              "sequence": 1,
              "title": "Task name",
              "description": "What to do",
              "estimatedDurationMinutes": 30,
              "requiredSkills": ["skill1", "skill2"],
              "safetyNotes": "Any safety warnings"
            }
          ],
          "notes": "Additional notes"
        }
        
        CRITICAL RULES:
        - All duration fields (estimatedDuration, estimatedDurationMinutes) MUST be integers, NOT strings.
            Examples: 60, 90, 120 - NOT "60 minutes" or "1.5 hours"
        - Assign the most qualified available technician based on skill match
        - Include ONLY relevant parts; use empty array if no parts needed
        - Tasks must be sequential and actionable
        - Return ONLY the JSON, no markdown or extra text
        """;

    /// <summary>
    /// JSON deserialization options that handle LLM quirks.
    /// PropertyNameCaseInsensitive: Handles "EstimatedDuration" or "estimatedDuration"
    /// AllowReadingFromString: Handles LLMs returning "60" instead of 60 for numbers
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Creates or updates the agent definition in Azure AI Foundry.
    /// Must be called once before invoking the agent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureAgentVersionAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing Repair Planner Agent (name={AgentName}, model={Model})", AgentName, modelDeploymentName);

        var definition = new PromptAgentDefinition(model: modelDeploymentName)
        {
            Instructions = AgentInstructions
        };

        try
        {
            await projectClient.Agents.CreateAgentVersionAsync(
                AgentName,
                new AgentVersionCreationOptions(definition),
                cancellationToken);

            logger.LogInformation("Agent definition registered successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize agent");
            throw;
        }
    }

    /// <summary>
    /// Plans and creates a work order for the given diagnosed fault.
    /// 
    /// Steps:
    /// 1. Get required skills and parts from fault mapping
    /// 2. Query Cosmos DB for available technicians and parts
    /// 3. Build prompt with context data
    /// 4. Invoke agent to generate work order JSON
    /// 5. Parse response with LLM-aware handling
    /// 6. Apply business logic defaults
    /// 7. Save to Cosmos DB
    /// </summary>
    /// <param name="fault">The diagnosed fault from the Fault Diagnosis Agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created work order.</returns>
    /// <exception cref="ArgumentNullException">Thrown if fault is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if agent response cannot be parsed.</exception>
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(DiagnosedFault fault, CancellationToken cancellationToken = default)
    {
        if (fault is null)
            throw new ArgumentNullException(nameof(fault));

        logger.LogInformation(
            "Planning repair for machine {MachineId}, fault={FaultType}, severity={Severity}",
            fault.MachineId,
            fault.FaultType,
            fault.Severity);

        // Step 1: Get required skills and parts from mapping service
        var requiredSkills = faultMapping.GetRequiredSkills(fault.FaultType);
        var requiredParts = faultMapping.GetRequiredParts(fault.FaultType);

        logger.LogInformation(
            "Fault requires {SkillCount} skills [{Skills}] and {PartCount} parts [{Parts}]",
            requiredSkills.Count,
            string.Join(", ", requiredSkills),
            requiredParts.Count,
            string.Join(", ", requiredParts));

        // Step 2: Query Cosmos DB for available resources
        var technicians = await cosmosDb.QueryAvailableTechniciansBySkillsAsync(requiredSkills, cancellationToken: cancellationToken);
        var parts = await cosmosDb.GetPartsByPartNumbersAsync(requiredParts, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Found {TechnicianCount} technicians and {PartCount} available parts",
            technicians.Count,
            parts.Count);

        // Step 3: Build prompt with context
        var prompt = BuildPrompt(fault, technicians, parts);

        // Step 4: Invoke agent
        var responseText = await InvokeAgentAsync(prompt, cancellationToken);

        // Step 5: Parse response
        var workOrder = ParseWorkOrder(responseText);

        // Step 6: Apply defaults and validation
        ApplyDefaults(workOrder, fault, technicians, requiredSkills);

        // Step 7: Save to Cosmos DB
        var savedWorkOrder = await cosmosDb.CreateWorkOrderAsync(workOrder, cancellationToken);
        logger.LogInformation("Work order saved: {WorkOrderNumber}", savedWorkOrder.WorkOrderNumber);

        return savedWorkOrder;
    }

    /// <summary>
    /// Invokes the Foundry Prompt Agent with the given input.
    /// </summary>
    /// <param name="input">The prompt/input for the agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response text.</returns>
    private async Task<string> InvokeAgentAsync(string input, CancellationToken cancellationToken)
    {
        logger.LogDebug("Invoking agent with prompt length={Length}", input.Length);

        try
        {
            var agent = projectClient.GetAIAgent(name: AgentName, cancellationToken: cancellationToken);
            var response = await agent.RunAsync(input, thread: null, options: null, cancellationToken: cancellationToken);
            
            var result = response.Text ?? string.Empty;
            logger.LogDebug("Agent response length={Length}", result.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent invocation failed");
            throw;
        }
    }

    /// <summary>
    /// Parses the agent's response into a WorkOrder object.
    /// Handles JSON embedded in markdown (e.g., ```json { ... } ```).
    /// Uses LLM-aware deserialization to handle string numbers.
    /// </summary>
    /// <param name="content">The agent's response content.</param>
    /// <returns>Parsed WorkOrder object.</returns>
    /// <exception cref="InvalidOperationException">Thrown if JSON cannot be extracted or parsed.</exception>
    private WorkOrder ParseWorkOrder(string content)
    {
        logger.LogDebug("Parsing work order from response");

        // Extract JSON from response (may be wrapped in markdown code blocks)
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException(
                $"No JSON object found in agent response. Content: {content[..Math.Min(200, content.Length)]}");
        }

        var json = content[start..(end + 1)];

        try
        {
            var workOrder = JsonSerializer.Deserialize<WorkOrder>(json, JsonOptions)
                ?? throw new InvalidOperationException("Deserialization returned null");

            logger.LogInformation("Successfully parsed work order: {WorkOrderNumber}", workOrder.WorkOrderNumber);
            return workOrder;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing failed. JSON content: {Json}", json[..Math.Min(500, json.Length)]);
            throw new InvalidOperationException("Failed to parse work order JSON from agent response", ex);
        }
    }

    /// <summary>
    /// Applies business logic defaults and validation to the work order.
    /// </summary>
    /// <param name="workOrder">The work order to update.</param>
    /// <param name="fault">The original diagnosed fault.</param>
    /// <param name="technicians">Available technicians.</param>
    /// <param name="requiredSkills">Required skills for this fault.</param>
    private void ApplyDefaults(WorkOrder workOrder, DiagnosedFault fault, IReadOnlyList<Technician> technicians, IReadOnlyList<string> requiredSkills)
    {
        // Set machine ID if not provided
        workOrder.MachineId ??= fault.MachineId;

        // Set work order type based on fault or recommendation
        if (string.IsNullOrWhiteSpace(workOrder.Type))
        {
            workOrder.Type = fault.RecommendedRepairType ?? "corrective";
        }

        // Map severity to priority
        if (string.IsNullOrWhiteSpace(workOrder.Priority))
        {
            workOrder.Priority = fault.Severity?.ToLowerInvariant() switch
            {
                "critical" => "critical",
                "high" => "high",
                "medium" => "medium",
                _ => "low"
            };
        }

        // Set default status
        workOrder.Status ??= "draft";

        // Validate assigned technician exists in available list
        if (!string.IsNullOrWhiteSpace(workOrder.AssignedTo))
        {
            if (!technicians.Any(t => t.Id == workOrder.AssignedTo))
            {
                logger.LogWarning("Agent assigned invalid technician {TechnicianId}, clearing assignment", workOrder.AssignedTo);
                workOrder.AssignedTo = null;
            }
            else
            {
                var assigned = technicians.First(t => t.Id == workOrder.AssignedTo);
                workOrder.AssignedTechnicianName = assigned.Name;
            }
        }

        // If no technician assigned, auto-assign best match
        if (string.IsNullOrWhiteSpace(workOrder.AssignedTo) && technicians.Count > 0)
        {
            var requiredSkillsSet = new HashSet<string>(requiredSkills, StringComparer.OrdinalIgnoreCase);
            var bestTechnician = technicians
                .OrderByDescending(t => t.Skills.Count(s => requiredSkillsSet.Contains(s)))
                .ThenBy(t => t.Name)
                .First();

            workOrder.AssignedTo = bestTechnician.Id;
            workOrder.AssignedTechnicianName = bestTechnician.Name;

            logger.LogInformation(
                "Auto-assigned technician {TechnicianId} ({TechnicianName}) with {SkillMatch} skill matches",
                bestTechnician.Id,
                bestTechnician.Name,
                bestTechnician.Skills.Count(s => requiredSkillsSet.Contains(s)));
        }

        // Link to the fault that triggered this work order
        workOrder.RelatedFaultId = fault.Id;

        logger.LogInformation(
            "Applied defaults to work order: type={Type}, priority={Priority}, assignedTo={AssignedTo}",
            workOrder.Type,
            workOrder.Priority,
            workOrder.AssignedTo ?? "<unassigned>");
    }

    /// <summary>
    /// Builds the prompt for the agent with all context data.
    /// </summary>
    /// <param name="fault">The diagnosed fault.</param>
    /// <param name="technicians">Available technicians.</param>
    /// <param name="parts">Available parts.</param>
    /// <returns>The formatted prompt.</returns>
    private static string BuildPrompt(DiagnosedFault fault, IReadOnlyList<Technician> technicians, IReadOnlyList<Part> parts)
    {
        return $"""
            Generate a repair plan for the following equipment fault:
            
            FAULT DETAILS:
            - Machine ID: {fault.MachineId}
            - Machine Name: {fault.MachineName}
            - Fault Type: {fault.FaultType}
            - Severity: {fault.Severity}
            - Detected: {fault.DetectedAt:O}
            - Confidence: {fault.Confidence:P}
            - Description: {fault.Description}
            
            AVAILABLE TECHNICIANS:
            {JsonSerializer.Serialize(technicians, new JsonSerializerOptions { WriteIndented = true })}
            
            AVAILABLE PARTS:
            {JsonSerializer.Serialize(parts, new JsonSerializerOptions { WriteIndented = true })}
            
            Create a detailed, actionable repair plan. Ensure:
            - Tasks are numbered and sequential
            - All durations are integers (e.g., 90 not "90 minutes")
            - Assign the most qualified technician
            - Include only necessary parts
            - Return ONLY valid JSON, no other text
            """;
    }
}
