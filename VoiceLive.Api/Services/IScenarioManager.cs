using VoiceLive.Api.Models;

namespace VoiceLive.Api.Services;

public interface IScenarioManager
{
    Dictionary<string, Scenario> GetAllScenarios();
    Scenario? GetScenario(string scenarioId);
    void ReloadScenarios();
}
