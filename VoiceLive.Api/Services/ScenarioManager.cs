using VoiceLive.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VoiceLive.Api.Services;

public class ScenarioManager : IScenarioManager
{
    private Dictionary<string, Scenario> _scenarios = new();
    private readonly ILogger<ScenarioManager> _logger;
    private readonly string _scenariosPath;
    private static readonly string BaseInstructions = @"
**IMPORTANT CONVERSATION GUIDELINES:**
- Keep ALL responses SHORT - maximum 3 sentences
- Stay completely in character
- Use natural, human-like speech patterns
- Show genuine emotions appropriate to your character
- Never use robotic or AI-like language
- Never break character or mention you're an AI
- Respond naturally to the conversation flow
";

    public ScenarioManager(ILogger<ScenarioManager> logger, IWebHostEnvironment environment)
    {
        _logger = logger;

        // Try to find scenarios in multiple locations
        var possiblePaths = new[]
        {
            Path.Combine(environment.ContentRootPath, "Data", "scenarios"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "scenarios"),
            Path.Combine(environment.ContentRootPath, "..", "..", "data", "scenarios")
        };

        _scenariosPath = possiblePaths.FirstOrDefault(Directory.Exists)
            ?? possiblePaths[0];

        _logger.LogInformation($"Scenario path: {_scenariosPath}");
        LoadScenarios();
    }

    public Dictionary<string, Scenario> GetAllScenarios()
    {
        return new Dictionary<string, Scenario>(_scenarios);
    }

    public Scenario? GetScenario(string scenarioId)
    {
        return _scenarios.TryGetValue(scenarioId, out var scenario) ? scenario : null;
    }

    public void ReloadScenarios()
    {
        LoadScenarios();
    }

    private void LoadScenarios()
    {
        _scenarios.Clear();

        if (!Directory.Exists(_scenariosPath))
        {
            _logger.LogWarning($"Scenarios directory not found: {_scenariosPath}");
            return;
        }

        var rolePlayFiles = Directory.GetFiles(_scenariosPath, "*-role-play.prompt.yml");
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();        foreach (var filePath in rolePlayFiles)
        {
            try
            {
                var yamlContent = File.ReadAllText(filePath);
                var scenario = deserializer.Deserialize<Scenario>(yamlContent);

                if (scenario != null)
                {
                    // Extract scenario ID from filename
                    var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath));
                    scenario.Id = fileName.Replace("-role-play.prompt", "");

                    // Add base instructions to all messages
                    foreach (var message in scenario.Messages)
                    {
                        if (message.Role.ToLower() == "system")
                        {
                            message.Content += "\n\n" + BaseInstructions;
                        }
                    }

                    _scenarios[scenario.Id] = scenario;
                    _logger.LogInformation($"Loaded scenario: {scenario.Id} - {scenario.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading scenario from {filePath}");
            }
        }

        _logger.LogInformation($"Loaded {_scenarios.Count} scenarios");
    }
}
