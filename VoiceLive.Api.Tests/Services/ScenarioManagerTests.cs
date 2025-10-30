using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceLive.Api.Configuration;
using VoiceLive.Api.Services;

namespace VoiceLive.Api.Tests.Services;

public class ScenarioManagerTests
{
    private readonly Mock<IOptions<AzureSettings>> _mockAzureSettings;
    private readonly Mock<ILogger<ScenarioManager>> _mockLogger;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly ScenarioManager _scenarioManager;

    public ScenarioManagerTests()
    {
        _mockAzureSettings = new Mock<IOptions<AzureSettings>>();
        _mockLogger = new Mock<ILogger<ScenarioManager>>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();

        var azureSettings = new AzureSettings
        {
            OpenAI = new OpenAISettings
            {
                Endpoint = "https://test.openai.azure.com",
                ApiKey = "test-key",
                ModelDeploymentName = "gpt-4o"
            }
        };
        _mockAzureSettings.Setup(x => x.Value).Returns(azureSettings);

        _mockEnvironment.Setup(x => x.ContentRootPath).Returns("/test/path");

        _scenarioManager = new ScenarioManager(
            _mockLogger.Object,
            _mockEnvironment.Object);
    }

    [Fact]
    public void GetAllScenarios_ShouldReturnEmptyDictionary_WhenNoScenariosExist()
    {
        // Act
        var scenarios = _scenarioManager.GetAllScenarios();

        // Assert
        scenarios.Should().NotBeNull();
        scenarios.Should().BeEmpty();
    }

    [Fact]
    public void GetScenario_ShouldReturnNull_WhenScenarioNotFound()
    {
        // Act
        var scenario = _scenarioManager.GetScenario("non-existent");

        // Assert
        scenario.Should().BeNull();
    }
}
