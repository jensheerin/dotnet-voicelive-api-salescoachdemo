using VoiceLive.Api.Models;

namespace VoiceLive.Api.Services;

public interface IPronunciationAssessor
{
    Task<PronunciationAssessment> AssessPronunciationAsync(byte[] audioData, string referenceText);
}
