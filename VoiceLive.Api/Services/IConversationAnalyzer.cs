using VoiceLive.Api.Models;

namespace VoiceLive.Api.Services;

public interface IConversationAnalyzer
{
    Task<ConversationAnalysisResult> AnalyzeConversationAsync(string scenarioId, string transcript);
}
