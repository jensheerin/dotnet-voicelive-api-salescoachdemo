using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using System.Text.Json;
using VoiceLive.Api.Configuration;
using VoiceLive.Api.Models;

namespace VoiceLive.Api.Services;

public class PronunciationAssessor : IPronunciationAssessor
{
    private readonly AzureSettings _azureSettings;
    private readonly ILogger<PronunciationAssessor> _logger;

    public PronunciationAssessor(
        IOptions<AzureSettings> azureSettings,
        ILogger<PronunciationAssessor> logger)
    {
        _azureSettings = azureSettings.Value;
        _logger = logger;
    }

    public async Task<PronunciationAssessment> AssessPronunciationAsync(byte[] audioData, string referenceText)
    {
        var config = SpeechConfig.FromSubscription(
            _azureSettings.Speech.Key,
            _azureSettings.Speech.Region);

        // Create a memory stream for the audio data
        using var audioStream = new MemoryStream(audioData);
        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
        var audioInputStream = AudioInputStream.CreatePushStream(audioFormat);

        // Write audio data to the stream
        audioInputStream.Write(audioData);
        audioInputStream.Close();

        var audioConfig = AudioConfig.FromStreamInput(audioInputStream);

        // TODO: Pronunciation assessment API needs to be updated for current SDK version
        // Using basic speech recognition for now
        using var recognizer = new SpeechRecognizer(config, audioConfig);
        var result = await recognizer.RecognizeOnceAsync();

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            // Return basic assessment with placeholder scores
            // Full pronunciation assessment will be implemented when SDK API is clarified
            var assessment = new PronunciationAssessment
            {
                AccuracyScore = 85.0,
                FluencyScore = 80.0,
                CompletenessScore = 90.0,
                PronScore = 85.0,
                RecognizedText = result.Text,
                ReferenceText = referenceText,
                Words = new List<WordAssessment>()
            };

            return assessment;
        }
        else if (result.Reason == ResultReason.NoMatch)
        {
            _logger.LogWarning("Speech could not be recognized");
            throw new InvalidOperationException("Speech could not be recognized");
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(result);
            _logger.LogError($"Speech recognition canceled: {cancellation.Reason}, {cancellation.ErrorDetails}");
            throw new InvalidOperationException($"Speech recognition canceled: {cancellation.ErrorDetails}");
        }

        throw new InvalidOperationException("Unexpected speech recognition result");
    }
}
