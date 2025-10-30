namespace VoiceLive.Api.Services;

public interface IGraphScenarioGenerator
{
    Task<string> GenerateScenarioAsync(string description);
}
