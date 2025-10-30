namespace VoiceLive.Api.Models;

public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public bool IsAzureAgent { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = "gpt-4o";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public string? AzureAgentId { get; set; }
}

public class CreateAgentRequest
{
    public string ScenarioId { get; set; } = string.Empty;
}

public class CreateAgentResponse
{
    public string AgentId { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}
