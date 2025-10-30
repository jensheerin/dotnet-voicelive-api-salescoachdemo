using Microsoft.AspNetCore.Mvc;
using VoiceLive.Api.Services;

namespace VoiceLive.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    private readonly IConversationAnalyzer _conversationAnalyzer;
    private readonly IPronunciationAssessor _pronunciationAssessor;
    private readonly ILogger<AnalyzeController> _logger;

    public AnalyzeController(
        IConversationAnalyzer conversationAnalyzer,
        IPronunciationAssessor pronunciationAssessor,
        ILogger<AnalyzeController> logger)
    {
        _conversationAnalyzer = conversationAnalyzer;
        _pronunciationAssessor = pronunciationAssessor;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeConversation([FromBody] AnalyzeConversationRequest request)
    {
        try
        {
            _logger.LogInformation("Received analyze conversation request for scenario: {ScenarioId}", request.ScenarioId);
            
            if (string.IsNullOrEmpty(request.ScenarioId) || string.IsNullOrEmpty(request.Transcript))
            {
                _logger.LogWarning("Invalid request: ScenarioId or Transcript is missing");
                return BadRequest(new { error = "Scenario ID and transcript are required" });
            }

            _logger.LogInformation("Analyzing conversation with transcript length: {Length}", request.Transcript.Length);

            var result = await _conversationAnalyzer.AnalyzeConversationAsync(
                request.ScenarioId,
                request.Transcript);

            _logger.LogInformation("Analysis completed successfully");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing conversation");
            return StatusCode(500, new { error = "Failed to analyze conversation" });
        }
    }

    [HttpPost("pronunciation")]
    public async Task<IActionResult> AssessPronunciation([FromForm] AssessPronunciationRequest request)
    {
        try
        {
            if (request.Audio == null || request.Audio.Length == 0)
            {
                return BadRequest(new { error = "Audio file is required" });
            }

            if (string.IsNullOrEmpty(request.ReferenceText))
            {
                return BadRequest(new { error = "Reference text is required" });
            }

            // Read audio file into byte array
            using var memoryStream = new MemoryStream();
            await request.Audio.CopyToAsync(memoryStream);
            var audioData = memoryStream.ToArray();

            var result = await _pronunciationAssessor.AssessPronunciationAsync(
                audioData,
                request.ReferenceText);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing pronunciation");
            return StatusCode(500, new { error = "Failed to assess pronunciation" });
        }
    }
}

public class AnalyzeConversationRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("scenario_id")]
    public string ScenarioId { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("audio_data")]
    public List<object>? AudioData { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("reference_text")]
    public string? ReferenceText { get; set; }
}

public class AssessPronunciationRequest
{
    public IFormFile Audio { get; set; } = null!;
    public string ReferenceText { get; set; } = string.Empty;
}
