using Microsoft.AspNetCore.Mvc;
using VoiceLive.Api.Services;

namespace VoiceLive.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly IScenarioManager _scenarioManager;
    private readonly ILogger<ScenariosController> _logger;

    public ScenariosController(
        IScenarioManager scenarioManager,
        ILogger<ScenariosController> logger)
    {
        _scenarioManager = scenarioManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetScenarios()
    {
        try
        {
            var scenarios = _scenarioManager.GetAllScenarios();
            return Ok(scenarios.Values.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scenarios");
            return StatusCode(500, new { error = "Failed to retrieve scenarios" });
        }
    }

    [HttpGet("{scenarioId}")]
    public IActionResult GetScenario(string scenarioId)
    {
        try
        {
            var scenario = _scenarioManager.GetScenario(scenarioId);
            if (scenario == null)
            {
                return NotFound(new { error = $"Scenario not found: {scenarioId}" });
            }

            return Ok(scenario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving scenario: {scenarioId}");
            return StatusCode(500, new { error = "Failed to retrieve scenario" });
        }
    }
}
