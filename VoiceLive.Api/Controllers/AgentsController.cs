using Microsoft.AspNetCore.Mvc;
using VoiceLive.Api.Services;

namespace VoiceLive.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentManager _agentManager;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IAgentManager agentManager,
        ILogger<AgentsController> logger)
    {
        _agentManager = agentManager;
        _logger = logger;
    }

    [HttpPost]
    [HttpPost("create")]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ScenarioId))
            {
                return BadRequest(new { error = "Scenario ID is required" });
            }

            var agent = await _agentManager.CreateAgentAsync(request.ScenarioId);
            
            // Return response in snake_case format expected by frontend
            return Ok(new 
            { 
                agent_id = agent.Id,
                scenario_id = agent.ScenarioId,
                instructions = agent.Instructions,
                model = agent.Model,
                temperature = agent.Temperature,
                max_tokens = agent.MaxTokens,
                created_at = agent.CreatedAt,
                is_azure_agent = agent.IsAzureAgent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent");
            return StatusCode(500, new { error = "Failed to create agent" });
        }
    }

    [HttpGet("{agentId}")]
    public IActionResult GetAgent(string agentId)
    {
        try
        {
            var agent = _agentManager.GetAgent(agentId);
            if (agent == null)
            {
                return NotFound(new { error = $"Agent not found: {agentId}" });
            }

            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving agent: {agentId}");
            return StatusCode(500, new { error = "Failed to retrieve agent" });
        }
    }

    [HttpDelete("{agentId}")]
    public async Task<IActionResult> DeleteAgent(string agentId)
    {
        try
        {
            await _agentManager.DeleteAgentAsync(agentId);
            return Ok(new { message = "Agent deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting agent: {agentId}");
            return StatusCode(500, new { error = "Failed to delete agent" });
        }
    }
}

public class CreateAgentRequest
{
    public string ScenarioId { get; set; } = string.Empty;
    public string? BaseInstructions { get; set; }

    // Support snake_case from frontend
    public string? scenario_id
    {
        get => ScenarioId;
        set => ScenarioId = value ?? string.Empty;
    }
}
