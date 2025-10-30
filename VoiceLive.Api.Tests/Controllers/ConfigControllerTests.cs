using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoiceLive.Api.Configuration;
using VoiceLive.Api.Controllers;

namespace VoiceLive.Api.Tests.Controllers;

public class ConfigControllerTests
{
    private readonly Mock<IOptions<AzureSettings>> _mockAzureSettings;
    private readonly Mock<ILogger<ConfigController>> _mockLogger;
    private readonly ConfigController _controller;

    public ConfigControllerTests()
    {
        _mockAzureSettings = new Mock<IOptions<AzureSettings>>();
        _mockLogger = new Mock<ILogger<ConfigController>>();

        var azureSettings = new AzureSettings
        {
            OpenAI = new OpenAISettings
            {
                Endpoint = "https://test.openai.azure.com",
                ApiKey = "test-key",
                ModelDeploymentName = "gpt-4o"
            },
            Speech = new SpeechSettings
            {
                Key = "test-speech-key",
                Region = "eastus"
            },
            AI = new AISettings
            {
                ProjectName = "test-project",
                ProjectEndpoint = "https://test.aiprojects.azure.com"
            }
        };
        _mockAzureSettings.Setup(x => x.Value).Returns(azureSettings);

        _controller = new ConfigController(_mockAzureSettings.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetConfig_ShouldReturnOkResult()
    {
        // Act
        var result = _controller.GetConfig();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetConfig_ShouldReturnConfigurationData()
    {
        // Act
        var result = _controller.GetConfig() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetConfig_ShouldNotExposeApiKeys()
    {
        // Act
        var result = _controller.GetConfig() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var configString = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        configString.Should().NotContain("test-key");
        configString.Should().NotContain("test-speech-key");
        configString.Should().NotContain("test-projects-key");
    }
}
