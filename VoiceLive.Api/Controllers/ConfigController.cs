using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VoiceLive.Api.Configuration;

namespace VoiceLive.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly AzureSettings _azureSettings;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IOptions<AzureSettings> azureSettings,
        ILogger<ConfigController> logger)
    {
        _azureSettings = azureSettings.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        try
        {
            var config = new
            {
                proxy_enabled = true,
                ws_endpoint = "/ws/voice",
                azure = new
                {
                    openai = new
                    {
                        endpoint = _azureSettings.OpenAI.Endpoint,
                        modelDeploymentName = _azureSettings.OpenAI.ModelDeploymentName
                    },
                    speech = new
                    {
                        region = _azureSettings.Speech.Region
                    },
                    ai = new
                    {
                        projectName = _azureSettings.AI.ProjectName,
                        projectEndpoint = _azureSettings.AI.ProjectEndpoint
                    }
                }
            };

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration");
            return StatusCode(500, new { error = "Failed to retrieve configuration" });
        }
    }
}
