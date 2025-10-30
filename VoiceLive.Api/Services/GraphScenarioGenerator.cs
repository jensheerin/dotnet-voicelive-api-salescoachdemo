using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using System.ClientModel;
using System.Text.Json;
using VoiceLive.Api.Configuration;
using VoiceLive.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using OpenAI.Chat;

namespace VoiceLive.Api.Services;

public class GraphScenarioGenerator : IGraphScenarioGenerator
{
    private readonly AzureSettings _azureSettings;
    private readonly ILogger<GraphScenarioGenerator> _logger;
    private readonly IWebHostEnvironment _environment;

    public GraphScenarioGenerator(
        IOptions<AzureSettings> azureSettings,
        ILogger<GraphScenarioGenerator> logger,
        IWebHostEnvironment environment)
    {
        _azureSettings = azureSettings.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<string> GenerateScenarioAsync(string description)
    {
        var template = await LoadScenarioTemplateAsync();
        if (template == null)
        {
            throw new InvalidOperationException("Scenario generation template not found");
        }

        var client = new AzureOpenAIClient(
            new Uri(_azureSettings.OpenAI.Endpoint),
            new ApiKeyCredential(_azureSettings.OpenAI.ApiKey));

        var chatClient = client.GetChatClient(_azureSettings.OpenAI.ModelDeploymentName);

        var messages = new List<ChatMessage>();

        // Add template instructions
        foreach (var message in template.Messages)
        {
            if (message.Role.ToLower() == "system")
            {
                messages.Add(ChatMessage.CreateSystemMessage(message.Content));
            }
            else if (message.Role.ToLower() == "user")
            {
                messages.Add(ChatMessage.CreateUserMessage(message.Content));
            }
        }

        // Add the user's scenario description
        messages.Add(ChatMessage.CreateUserMessage($"Generate a scenario based on this description: {description}"));

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = (float)template.ModelParameters.Temperature,
            MaxOutputTokenCount = template.ModelParameters.MaxTokens
        };        var response = await chatClient.CompleteChatAsync(messages, chatOptions);
        var generatedYaml = response.Value.Content[0].Text;

        _logger.LogDebug($"Generated scenario YAML: {generatedYaml}");

        // Validate the generated YAML by trying to parse it
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            deserializer.Deserialize<Scenario>(generatedYaml);
            _logger.LogInformation("Generated scenario YAML is valid");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Generated scenario YAML may be invalid");
        }

        return generatedYaml;
    }

    private async Task<Scenario?> LoadScenarioTemplateAsync()
    {
        var possiblePaths = new[]
        {
            Path.Combine(_environment.ContentRootPath, "Data", "scenarios"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "scenarios"),
            Path.Combine(_environment.ContentRootPath, "..", "..", "data", "scenarios")
        };

        var scenariosPath = possiblePaths.FirstOrDefault(Directory.Exists);
        if (scenariosPath == null)
        {
            _logger.LogWarning("Scenarios directory not found");
            return null;
        }

        var templateFile = Path.Combine(scenariosPath, "graph-generated-evaluation.prompt.yml");
        if (!File.Exists(templateFile))
        {
            _logger.LogWarning($"Template file not found: {templateFile}");
            return null;
        }

        var yamlContent = await File.ReadAllTextAsync(templateFile);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<Scenario>(yamlContent);
    }
}
