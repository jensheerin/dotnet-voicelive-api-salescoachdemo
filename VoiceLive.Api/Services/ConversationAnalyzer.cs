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

public class ConversationAnalyzer : IConversationAnalyzer
{
    private readonly AzureSettings _azureSettings;
    private readonly ILogger<ConversationAnalyzer> _logger;
    private readonly IWebHostEnvironment _environment;

    public ConversationAnalyzer(
        IOptions<AzureSettings> azureSettings,
        ILogger<ConversationAnalyzer> logger,
        IWebHostEnvironment environment)
    {
        _azureSettings = azureSettings.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<ConversationAnalysisResult> AnalyzeConversationAsync(string scenarioId, string transcript)
    {
        var evaluationScenario = await LoadEvaluationScenarioAsync(scenarioId);
        if (evaluationScenario == null)
        {
            throw new ArgumentException($"Evaluation scenario not found for: {scenarioId}");
        }

        var client = new AzureOpenAIClient(
            new Uri(_azureSettings.OpenAI.Endpoint),
            new ApiKeyCredential(_azureSettings.OpenAI.ApiKey));

        var chatClient = client.GetChatClient(_azureSettings.OpenAI.ModelDeploymentName);

        var messages = new List<ChatMessage>();

        // Add evaluation instructions
        foreach (var message in evaluationScenario.Messages)
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

        // Add the transcript
        messages.Add(ChatMessage.CreateUserMessage($"Please evaluate the following conversation:\n\n{transcript}"));

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = (float)evaluationScenario.ModelParameters.Temperature,
            MaxOutputTokenCount = evaluationScenario.ModelParameters.MaxTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "conversation_evaluation",
                jsonSchema: BinaryData.FromString(GetEvaluationJsonSchema()),
                jsonSchemaIsStrict: true)
        };        var response = await chatClient.CompleteChatAsync(messages, chatOptions);
        var content = response.Value.Content[0].Text;

        _logger.LogDebug($"OpenAI Response: {content}");

        var result = JsonSerializer.Deserialize<ConversationAnalysisResult>(content)
            ?? throw new InvalidOperationException("Failed to deserialize analysis result");

        return result;
    }

    private async Task<Scenario?> LoadEvaluationScenarioAsync(string scenarioId)
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

        var evaluationFile = Path.Combine(scenariosPath, $"{scenarioId}-evaluation.prompt.yml");
        if (!File.Exists(evaluationFile))
        {
            _logger.LogWarning($"Evaluation file not found: {evaluationFile}");
            return null;
        }

        var yamlContent = await File.ReadAllTextAsync(evaluationFile);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<Scenario>(yamlContent);
    }

    private static string GetEvaluationJsonSchema()
    {
        return """
        {
            "type": "object",
            "properties": {
                "speaking_tone_style": {
                    "type": "object",
                    "properties": {
                        "professional_tone": { "type": "integer", "minimum": 0, "maximum": 10 },
                        "active_listening": { "type": "integer", "minimum": 0, "maximum": 10 },
                        "engagement_quality": { "type": "integer", "minimum": 0, "maximum": 10 },
                        "total": { "type": "integer", "minimum": 0, "maximum": 30 }
                    },
                    "required": ["professional_tone", "active_listening", "engagement_quality", "total"],
                    "additionalProperties": false
                },
                "conversation_content": {
                    "type": "object",
                    "properties": {
                        "needs_assessment": { "type": "integer", "minimum": 0, "maximum": 25 },
                        "value_proposition": { "type": "integer", "minimum": 0, "maximum": 25 },
                        "objection_handling": { "type": "integer", "minimum": 0, "maximum": 20 },
                        "total": { "type": "integer", "minimum": 0, "maximum": 70 }
                    },
                    "required": ["needs_assessment", "value_proposition", "objection_handling", "total"],
                    "additionalProperties": false
                },
                "overall_score": { "type": "integer", "minimum": 0, "maximum": 100 },
                "strengths": {
                    "type": "array",
                    "items": { "type": "string" },
                    "minItems": 1,
                    "maxItems": 5
                },
                "improvements": {
                    "type": "array",
                    "items": { "type": "string" },
                    "minItems": 1,
                    "maxItems": 5
                },
                "specific_feedback": { "type": "string" }
            },
            "required": ["speaking_tone_style", "conversation_content", "overall_score", "strengths", "improvements", "specific_feedback"],
            "additionalProperties": false
        }
        """;
    }
}
