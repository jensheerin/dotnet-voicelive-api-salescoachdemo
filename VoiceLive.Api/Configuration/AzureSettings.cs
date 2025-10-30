namespace VoiceLive.Api.Configuration;

public class AzureSettings
{
    public AISettings AI { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public SpeechSettings Speech { get; set; } = new();
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
}

public class AISettings
{
    public string ResourceName { get; set; } = string.Empty;
    public string Region { get; set; } = "swedencentral";
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectEndpoint { get; set; } = string.Empty;
    public bool UseAzureAIAgents { get; set; } = false;
    public string AgentId { get; set; } = string.Empty;
}

public class OpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelDeploymentName { get; set; } = "gpt-4o";
    public string ApiVersion { get; set; } = "2024-12-01-preview";
}

public class SpeechSettings
{
    public string Key { get; set; } = string.Empty;
    public string Region { get; set; } = "swedencentral";
    public string Language { get; set; } = "en-US";
    public string VoiceName { get; set; } = "en-US-Ava:DragonHDLatestNeural";
    public string VoiceType { get; set; } = "azure-standard";
    public string AvatarCharacter { get; set; } = "lisa";
    public string AvatarStyle { get; set; } = "casual-sitting";
    public string InputTranscriptionModel { get; set; } = "azure-speech";
    public string InputTranscriptionLanguage { get; set; } = "en-US";
    public string InputNoiseReductionType { get; set; } = "azure_deep_noise_suppression";
}
