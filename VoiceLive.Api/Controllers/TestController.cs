using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VoiceLive.Api.Configuration;
using Microsoft.CognitiveServices.Speech;

namespace VoiceLive.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly AzureSettings _azureSettings;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IOptions<AzureSettings> azureSettings,
        ILogger<TestController> logger)
    {
        _azureSettings = azureSettings.Value;
        _logger = logger;
    }

    [HttpGet("speech-config")]
    public IActionResult TestSpeechConfig()
    {
        try
        {
            _logger.LogInformation($"Testing Speech Config - Key length: {_azureSettings.Speech.Key?.Length ?? 0}, Region: {_azureSettings.Speech.Region}");

            if (string.IsNullOrEmpty(_azureSettings.Speech.Key))
            {
                return Ok(new { success = false, error = "Speech key is null or empty" });
            }

            if (string.IsNullOrEmpty(_azureSettings.Speech.Region))
            {
                return Ok(new { success = false, error = "Speech region is null or empty" });
            }

            var speechConfig = SpeechConfig.FromSubscription(
                _azureSettings.Speech.Key,
                _azureSettings.Speech.Region);

            speechConfig.SpeechSynthesisVoiceName = "en-US-AvaMultilingualNeural";

            _logger.LogInformation("SpeechConfig created successfully");

            // Try to create a synthesizer
            using var synthesizer = new SpeechSynthesizer(speechConfig, null);

            _logger.LogInformation("SpeechSynthesizer created successfully");

            return Ok(new
            {
                success = true,
                keyLength = _azureSettings.Speech.Key.Length,
                region = _azureSettings.Speech.Region,
                voice = speechConfig.SpeechSynthesisVoiceName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error testing speech config: {ex.Message}");
            return Ok(new
            {
                success = false,
                error = ex.Message,
                details = ex.ToString(),
                innerException = ex.InnerException?.Message
            });
        }
    }

    [HttpPost("synthesize-simple")]
    public async Task<IActionResult> TestSynthesis([FromBody] TestSynthesisRequest request)
    {
        try
        {
            _logger.LogInformation($"Testing synthesis with text: {request.Text}");

            var speechConfig = SpeechConfig.FromSubscription(
                _azureSettings.Speech.Key,
                _azureSettings.Speech.Region);

            speechConfig.SpeechSynthesisVoiceName = request.Voice ?? "en-US-AvaMultilingualNeural";

            using var synthesizer = new SpeechSynthesizer(speechConfig, null);

            var result = await synthesizer.SpeakTextAsync(request.Text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                return Ok(new
                {
                    success = true,
                    audioLength = result.AudioData.Length,
                    duration = result.AudioDuration.TotalSeconds
                });
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                return Ok(new
                {
                    success = false,
                    reason = cancellation.Reason.ToString(),
                    errorCode = cancellation.ErrorCode.ToString(),
                    errorDetails = cancellation.ErrorDetails
                });
            }

            return Ok(new { success = false, reason = result.Reason.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error testing synthesis: {ex.Message}");
            return Ok(new
            {
                success = false,
                error = ex.Message,
                details = ex.ToString()
            });
        }
    }
}

public class TestSynthesisRequest
{
    public string Text { get; set; } = "Hello, this is a test.";
    public string? Voice { get; set; }
}
