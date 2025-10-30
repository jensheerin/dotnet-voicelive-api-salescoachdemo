using VoiceLive.Api.Models;

namespace VoiceLive.Api.Services;

public interface IAgentManager
{
    Task<Agent> CreateAgentAsync(string scenarioId);
    Agent? GetAgent(string agentId);
    Task DeleteAgentAsync(string agentId);
}
