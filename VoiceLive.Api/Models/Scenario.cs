using YamlDotNet.Serialization;

namespace VoiceLive.Api.Models;

public class Scenario
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";

    public ModelParameters ModelParameters { get; set; } = new();

    public List<Message> Messages { get; set; } = new();
}

public class ModelParameters
{
    public double Temperature { get; set; } = 0.7;

    [YamlMember(Alias = "maxTokens", ApplyNamingConventions = false)]
    [System.Text.Json.Serialization.JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2000;
}

public class Message
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
