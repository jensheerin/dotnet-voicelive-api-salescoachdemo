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
        try
        {
            _logger.LogInformation("Starting conversation analysis for scenario: {ScenarioId}", scenarioId);
            
            var evaluationScenario = await LoadEvaluationScenarioAsync(scenarioId);
            if (evaluationScenario == null)
            {
                _logger.LogError("Evaluation scenario not found for: {ScenarioId}", scenarioId);
                throw new ArgumentException($"Evaluation scenario not found for: {scenarioId}");
            }

            _logger.LogInformation("Loaded evaluation scenario with {MessageCount} messages", evaluationScenario.Messages?.Count ?? 0);

            if (string.IsNullOrEmpty(_azureSettings.OpenAI.Endpoint))
            {
                _logger.LogError("Azure OpenAI Endpoint is not configured");
                throw new InvalidOperationException("Azure OpenAI Endpoint is not configured. Please check appsettings.json");
            }

            if (string.IsNullOrEmpty(_azureSettings.OpenAI.ApiKey))
            {
                _logger.LogError("Azure OpenAI ApiKey is not configured");
                throw new InvalidOperationException("Azure OpenAI ApiKey is not configured. Please check appsettings.json");
            }

            _logger.LogInformation("Connecting to Azure OpenAI at: {Endpoint}", _azureSettings.OpenAI.Endpoint);
            _logger.LogInformation("Using model deployment: {ModelDeployment}", _azureSettings.OpenAI.ModelDeploymentName);

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

            _logger.LogInformation("Prepared {MessageCount} messages for OpenAI", messages.Count);

            var chatOptions = new ChatCompletionOptions
            {
                Temperature = (float)evaluationScenario.ModelParameters.Temperature,
                MaxOutputTokenCount = evaluationScenario.ModelParameters.MaxTokens,
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "conversation_evaluation",
                    jsonSchema: BinaryData.FromString(GetEvaluationJsonSchema()),
                    jsonSchemaIsStrict: true)
            };

            _logger.LogInformation("Calling Azure OpenAI ChatClient...");
            var response = await chatClient.CompleteChatAsync(messages, chatOptions);
            
            _logger.LogInformation("Received response from Azure OpenAI");
            var content = response.Value.Content[0].Text;

            _logger.LogDebug("OpenAI Response: {Content}", content);

            _logger.LogInformation("Deserializing response to ConversationAnalysisResult");
            var result = JsonSerializer.Deserialize<ConversationAnalysisResult>(content)
                ?? throw new InvalidOperationException("Failed to deserialize analysis result");

            _logger.LogInformation("Successfully completed conversation analysis");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzeConversationAsync for scenario {ScenarioId}: {Message}", scenarioId, ex.Message);
            throw;
        }
    }

    private async Task<Scenario?> LoadEvaluationScenarioAsync(string scenarioId)
    {
        _logger.LogInformation("Loading evaluation scenario: {ScenarioId}", scenarioId);
        
        // Extract base scenario ID (e.g., "scenario1" from "scenario1-role-play")
        var baseScenarioId = scenarioId.Replace("-role-play", "");
        _logger.LogInformation("Base scenario ID: {BaseScenarioId}", baseScenarioId);
        
        var possiblePaths = new[]
        {
            Path.Combine(_environment.ContentRootPath, "Data", "scenarios"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "scenarios"),
            Path.Combine(_environment.ContentRootPath, "..", "..", "data", "scenarios")
        };

        _logger.LogDebug("Searching for scenarios in paths: {Paths}", string.Join(", ", possiblePaths));

        var scenariosPath = possiblePaths.FirstOrDefault(Directory.Exists);
        if (scenariosPath == null)
        {
            _logger.LogWarning("Scenarios directory not found in any of the expected locations");
            return null;
        }

        _logger.LogInformation("Found scenarios directory: {Path}", scenariosPath);

        var evaluationFile = Path.Combine(scenariosPath, $"{baseScenarioId}-evaluation.prompt.yml");
        _logger.LogInformation("Looking for evaluation file: {FilePath}", evaluationFile);
        
        if (!File.Exists(evaluationFile))
        {
            _logger.LogWarning("Evaluation file not found: {FilePath}", evaluationFile);
            
            // List available files for debugging
            var availableFiles = Directory.GetFiles(scenariosPath, "*.yml");
            _logger.LogInformation("Available scenario files: {Files}", string.Join(", ", availableFiles.Select(Path.GetFileName)));
            
            return null;
        }

        _logger.LogInformation("Reading evaluation file...");
        var yamlContent = await File.ReadAllTextAsync(evaluationFile);
        
        _logger.LogDebug("YAML content length: {Length} characters", yamlContent.Length);
        
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var scenario = deserializer.Deserialize<Scenario>(yamlContent);
        _logger.LogInformation("Successfully loaded and deserialized scenario");
        
        return scenario;
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
