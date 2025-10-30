/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VoiceLive.Api.Configuration;

namespace VoiceLive.Api.Services;

public class VoiceProxyWebSocketHandler
{
    private readonly AzureSettings _azureSettings;
    private readonly IAgentManager _agentManager;
    private readonly ILogger<VoiceProxyWebSocketHandler> _logger;
    
    private const string AzureVoiceApiVersion = "2025-05-01-preview";
    private const string AzureCognitiveServicesDomain = "cognitiveservices.azure.com";
    private const string VoiceAgentEndpoint = "voice-agent/realtime";
    private const int MaxMessageSize = 1024 * 1024; // 1MB
    private const int LogMessageMaxLength = 200;

    public VoiceProxyWebSocketHandler(
        IOptions<AzureSettings> azureSettings,
        IAgentManager agentManager,
        ILogger<VoiceProxyWebSocketHandler> logger)
    {
        _azureSettings = azureSettings.Value;
        _agentManager = agentManager;
        _logger = logger;
    }

    public async Task HandleConnectionAsync(WebSocket clientWebSocket, CancellationToken cancellationToken)
    {
        _logger.LogInformation("New WebSocket connection established");
        ClientWebSocket? azureWebSocket = null;
        string? currentAgentId = null;

        try
        {
            // Get agent ID from initial client message
            currentAgentId = await GetAgentIdFromClientAsync(clientWebSocket, cancellationToken);
            
            // Connect to Azure Voice Live API
            azureWebSocket = await ConnectToAzureAsync(currentAgentId, cancellationToken);
            
            if (azureWebSocket == null)
            {
                await SendErrorAsync(clientWebSocket, "Failed to connect to Azure Voice API", cancellationToken);
                return;
            }

            // Send connection confirmation
            await SendMessageAsync(clientWebSocket, new
            {
                type = "proxy.connected",
                message = "Connected to Azure Voice API"
            }, cancellationToken);

            // Handle bidirectional message forwarding
            await HandleMessageForwardingAsync(clientWebSocket, azureWebSocket, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proxy error: {Message}", ex.Message);
            if (clientWebSocket.State == WebSocketState.Open)
            {
                await SendErrorAsync(clientWebSocket, ex.Message, cancellationToken);
            }
        }
        finally
        {
            if (azureWebSocket?.State == WebSocketState.Open)
            {
                await azureWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
            azureWebSocket?.Dispose();
        }
    }

    private async Task<string?> GetAgentIdFromClientAsync(WebSocket clientWebSocket, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[MaxMessageSize];
            var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var messageObj = JsonSerializer.Deserialize<JsonElement>(message);
                
                if (messageObj.TryGetProperty("type", out var typeProperty) && 
                    typeProperty.GetString() == "session.update")
                {
                    if (messageObj.TryGetProperty("session", out var session) &&
                        session.TryGetProperty("agent_id", out var agentId))
                    {
                        return agentId.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent ID: {Message}", ex.Message);
        }
        
        return null;
    }

    private async Task<ClientWebSocket?> ConnectToAzureAsync(string? agentId, CancellationToken cancellationToken)
    {
        try
        {
            var agent = agentId != null ? _agentManager.GetAgent(agentId) : null;
            var azureUrl = BuildAzureUrl(agentId, agent);
            var apiKey = _azureSettings.OpenAI.ApiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("No API key found in configuration (Azure.OpenAI.ApiKey)");
                return null;
            }

            var azureWebSocket = new ClientWebSocket();
            azureWebSocket.Options.SetRequestHeader("api-key", apiKey);

            _logger.LogInformation("Connecting to Azure Voice API: {Url}", azureUrl.Split('?')[0]);
            await azureWebSocket.ConnectAsync(new Uri(azureUrl), cancellationToken);
            
            _logger.LogInformation("Connected to Azure Voice API with agent: {AgentId}", agentId ?? "default");

            // Send initial configuration
            await SendInitialConfigAsync(azureWebSocket, agent, cancellationToken);

            return azureWebSocket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Azure: {Message}", ex.Message);
            return null;
        }
    }

    private string BuildAzureUrl(string? agentId, Models.Agent? agent)
    {
        var baseUrl = BuildBaseAzureUrl();

        if (agent != null)
        {
            return BuildAgentSpecificUrl(baseUrl, agentId, agent);
        }

        if (!string.IsNullOrEmpty(_azureSettings.AI.AgentId))
        {
            return $"{baseUrl}&agent-id={_azureSettings.AI.AgentId}";
        }

        var modelName = _azureSettings.OpenAI.ModelDeploymentName;
        return $"{baseUrl}&model={modelName}";
    }

    private string BuildBaseAzureUrl()
    {
        var resourceName = _azureSettings.AI.ResourceName;
        var clientRequestId = Guid.NewGuid();
        
        return $"wss://{resourceName}.{AzureCognitiveServicesDomain}/" +
               $"{VoiceAgentEndpoint}?api-version={AzureVoiceApiVersion}" +
               $"&x-ms-client-request-id={clientRequestId}";
    }

    private string BuildAgentSpecificUrl(string baseUrl, string? agentId, Models.Agent agent)
    {
        var projectName = _azureSettings.AI.ProjectName;

        if (agent.IsAzureAgent)
        {
            return $"{baseUrl}&agent-id={agentId}&agent-project-name={projectName}";
        }

        var modelName = agent.Model ?? _azureSettings.OpenAI.ModelDeploymentName;
        return $"{baseUrl}&model={modelName}";
    }

    private async Task SendInitialConfigAsync(ClientWebSocket azureWebSocket, Models.Agent? agent, CancellationToken cancellationToken)
    {
        var configMessage = BuildSessionConfig();

        if (agent != null && !agent.IsAzureAgent)
        {
            AddLocalAgentConfig(configMessage, agent);
        }

        var json = JsonSerializer.Serialize(configMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        await azureWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private Dictionary<string, object> BuildSessionConfig()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "session.update",
            ["session"] = new Dictionary<string, object>
            {
                ["modalities"] = new[] { "text", "audio" },
                ["turn_detection"] = new Dictionary<string, string>
                {
                    ["type"] = "azure_semantic_vad"
                },
                ["input_audio_noise_reduction"] = new Dictionary<string, string>
                {
                    ["type"] = _azureSettings.Speech.InputNoiseReductionType
                },
                ["input_audio_echo_cancellation"] = new Dictionary<string, string>
                {
                    ["type"] = "server_echo_cancellation"
                },
                ["avatar"] = new Dictionary<string, string>
                {
                    ["character"] = _azureSettings.Speech.AvatarCharacter,
                    ["style"] = _azureSettings.Speech.AvatarStyle
                },
                ["voice"] = new Dictionary<string, string>
                {
                    ["name"] = _azureSettings.Speech.VoiceName,
                    ["type"] = _azureSettings.Speech.VoiceType
                }
            }
        };
    }

    private void AddLocalAgentConfig(Dictionary<string, object> configMessage, Models.Agent agent)
    {
        var session = (Dictionary<string, object>)configMessage["session"];
        session["model"] = agent.Model ?? _azureSettings.OpenAI.ModelDeploymentName;
        session["instructions"] = agent.Instructions;
        session["temperature"] = agent.Temperature;
        session["max_response_output_tokens"] = agent.MaxTokens;
    }

    private async Task HandleMessageForwardingAsync(
        WebSocket clientWebSocket,
        ClientWebSocket azureWebSocket,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var clientToAzureTask = ForwardClientToAzureAsync(clientWebSocket, azureWebSocket, cts.Token);
        var azureToClientTask = ForwardAzureToClientAsync(azureWebSocket, clientWebSocket, cts.Token);

        var completedTask = await Task.WhenAny(clientToAzureTask, azureToClientTask);
        
        // Cancel the other task
        cts.Cancel();

        try
        {
            await Task.WhenAll(clientToAzureTask, azureToClientTask);
        }
        catch (OperationCanceledException)
        {
            // Expected when we cancel
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message forwarding: {Message}", ex.Message);
        }
    }

    private async Task ForwardClientToAzureAsync(
        WebSocket clientWebSocket,
        ClientWebSocket azureWebSocket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxMessageSize];

        try
        {
            while (clientWebSocket.State == WebSocketState.Open && 
                   azureWebSocket.State == WebSocketState.Open &&
                   !cancellationToken.IsCancellationRequested)
            {
                var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Client initiated close");
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var logMessage = message.Length > LogMessageMaxLength 
                    ? message.Substring(0, LogMessageMaxLength) + "..." 
                    : message;
                _logger.LogDebug("Client->Azure: {Message}", logMessage);

                await azureWebSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug("Client connection closed during forwarding: {Message}", ex.Message);
        }
    }

    private async Task ForwardAzureToClientAsync(
        ClientWebSocket azureWebSocket,
        WebSocket clientWebSocket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxMessageSize];

        try
        {
            while (azureWebSocket.State == WebSocketState.Open && 
                   clientWebSocket.State == WebSocketState.Open &&
                   !cancellationToken.IsCancellationRequested)
            {
                var result = await azureWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Azure initiated close");
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var logMessage = message.Length > LogMessageMaxLength 
                    ? message.Substring(0, LogMessageMaxLength) + "..." 
                    : message;
                _logger.LogDebug("Azure->Client: {Message}", logMessage);

                await clientWebSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug("Azure connection closed during forwarding: {Message}", ex.Message);
        }
    }

    private async Task SendMessageAsync(WebSocket webSocket, object message, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", ex.Message);
        }
    }

    private async Task SendErrorAsync(WebSocket webSocket, string errorMessage, CancellationToken cancellationToken)
    {
        await SendMessageAsync(webSocket, new
        {
            type = "error",
            error = new { message = errorMessage }
        }, cancellationToken);
    }
}
