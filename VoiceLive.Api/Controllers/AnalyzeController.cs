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
    [HttpPost("conversation")]
    public async Task<IActionResult> AnalyzeConversation([FromBody] AnalyzeConversationRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ScenarioId) || string.IsNullOrEmpty(request.Transcript))
            {
                return BadRequest(new { error = "Scenario ID and transcript are required" });
            }

            var result = await _conversationAnalyzer.AnalyzeConversationAsync(
                request.ScenarioId,
                request.Transcript);

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
    public string ScenarioId { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public List<byte[]>? AudioData { get; set; }
    public string? ReferenceText { get; set; }

    // Support snake_case from frontend
    public string? scenario_id
    {
        get => ScenarioId;
        set => ScenarioId = value ?? string.Empty;
    }
    public List<byte[]>? audio_data
    {
        get => AudioData;
        set => AudioData = value;
    }
    public string? reference_text
    {
        get => ReferenceText;
        set => ReferenceText = value;
    }
}

public class AssessPronunciationRequest
{
    public IFormFile Audio { get; set; } = null!;
    public string ReferenceText { get; set; } = string.Empty;
}
