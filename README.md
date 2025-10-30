# VoiceLive API - Sales Coach Demo (.NET)

AI-powered voice training platform for sales professionals using Azure Voice Live API for real-time speech-to-speech conversations.

## Architecture

- **Framework**: .NET 9.0, ASP.NET Core
- **WebSocket**: Raw WebSocket proxy to Azure Voice Live API
- **Frontend**: React + Vite with TypeScript
- **Testing**: xUnit with Moq and FluentAssertions
- **Azure Services**:
  - Azure Voice Live API (real-time speech-to-speech)
  - Azure OpenAI (GPT-4o for agent instructions and analysis)
  - Azure AI Projects (agent management)
  - Azure Speech Services (pronunciation assessment)

## Project Structure

```text
voicelive-api-salescoachdemo-dotnet/
├── VoiceLive.Api/                      # Main API project
│   ├── Configuration/                  # Azure settings
│   ├── Controllers/                    # REST API endpoints
│   │   ├── ConfigController.cs         # Configuration
│   │   ├── ScenariosController.cs      # Scenario management
│   │   ├── AgentsController.cs         # Agent lifecycle
│   │   └── AnalyzeController.cs        # Post-conversation analysis
│   ├── Services/                       # Business logic
│   │   ├── VoiceProxyWebSocketHandler.cs  # WebSocket proxy to Azure Voice Live API
│   │   ├── AgentManager.cs             # Agent creation and management
│   │   ├── ScenarioManager.cs          # Scenario loading from YAML
│   │   ├── ConversationAnalyzer.cs     # GPT-4o conversation analysis
│   │   ├── PronunciationAssessor.cs    # Azure Speech pronunciation assessment
│   │   └── GraphScenarioGenerator.cs   # AI scenario generation
│   ├── Models/                         # Data models
│   ├── Data/scenarios/                 # YAML scenario definitions
│   ├── wwwroot/                        # Static frontend files
│   └── Program.cs                      # Application startup
├── VoiceLive.Api.Tests/                # xUnit test project
├── frontend/                           # React + Vite frontend
│   ├── src/
│   │   ├── components/                 # UI components
│   │   ├── hooks/                      # React hooks (useRealtime, useWebRTC)
│   │   └── services/                   # API client
│   └── package.json
└── Dockerfile
```

## Features

### REST API Endpoints

- `GET /api/config` - Get Azure configuration and WebSocket endpoint
- `GET /api/scenarios` - List available training scenarios
- `GET /api/scenarios/{id}` - Get specific scenario details
- `POST /api/agents/create` - Create AI agent for scenario (returns snake_case format)
- `POST /api/analyze/conversation` - Analyze conversation with GPT-4o
- `POST /api/analyze/pronunciation` - Assess pronunciation quality

#### Agent Creation Response Format

The `/api/agents/create` endpoint returns responses in snake_case format for frontend compatibility:

```json
{
  "agent_id": "local-agent-scenario3-role-play-xxxxx",
  "scenario_id": "scenario3-role-play",
  "instructions": "...",
  "model": "gpt-4o",
  "temperature": 0.7,
  "max_tokens": 2000,
  "created_at": "2025-10-30T13:04:19Z",
  "is_azure_agent": false
}

### WebSocket Proxy

- **Endpoint**: `ws://localhost:5000/ws/voice`
- **Protocol**: Transparent bidirectional proxy to Azure Voice Live API
- **Features**:
  - Real-time speech-to-text and text-to-speech
  - Voice activity detection (VAD)
  - Echo cancellation and noise reduction
  - Avatar support (video WebRTC)
  - Session management with agent context

## Prerequisites

- .NET 9.0 SDK
- Azure subscription with:
  - Azure OpenAI service
  - Azure Speech service
  - Azure AI Projects (optional, for agent features)

## Configuration

Create `VoiceLive.Api/appsettings.Development.json`:

```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com",
      "ApiKey": "your-openai-api-key",
      "ModelDeploymentName": "gpt-4o"
    },
    "Speech": {
      "ApiKey": "your-speech-api-key",
      "Region": "eastus2"
    },
    "AI": {
      "ResourceName": "your-resource-name",
      "ProjectName": "your-project-name",
      "ProjectEndpoint": "https://your-resource.cognitiveservices.azure.com",
      "ApiKey": "your-ai-projects-key"
    }
  }
}
```

## Installation

### Prerequisites

- .NET 9.0 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- Node.js 18+ (for frontend)
- Azure subscription with Voice Live API, OpenAI, and Speech Services

### Backend Setup

```bash
cd VoiceLive.Api
dotnet restore
dotnet build
```

### Frontend Setup

```bash
cd frontend
npm install
```

## Running the Application

### Start Backend

```powershell
cd VoiceLive.Api
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run
```

Backend will be available at `http://localhost:5000`

### Start Frontend

```bash
cd frontend
npm run dev
```

Frontend will be available at `http://localhost:5173`

### Development Mode

```bash
# Backend with hot reload
cd VoiceLive.Api
dotnet watch run

# Frontend automatically hot reloads with Vite
```

## Testing

### Run All Tests

```bash
cd VoiceLive.Api.Tests
dotnet test
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## How It Works

1. **User selects training scenario** (e.g., handling objections, closing techniques)
2. **Backend creates AI agent** using Azure AI Projects with GPT-4o and scenario-specific instructions
3. **Frontend connects WebSocket** to `/ws/voice` endpoint
4. **WebSocket proxy forwards** to Azure Voice Live API with agent configuration
5. **Real-time voice conversation** with speech-to-text, AI response generation, and text-to-speech
6. **Post-conversation analysis** using GPT-4o for feedback and pronunciation assessment

## Architecture Flow

```text
React Frontend (port 5173)
    ↓ WebSocket connection
.NET Proxy (/ws/voice on port 5000)
    ↓ WebSocket forwarding with agent context
Azure Voice Live API (wss://{resource}.cognitiveservices.azure.com/voice-agent/realtime)
    ↓ Connected to
GPT-4o Model + Voice (en-US-Ava:DragonHDLatestNeural)
```

## Docker

### Build Image

```bash
docker build -t voicelive-api:latest .
```

### Run Container

```bash
docker run -p 5000:5000 \
  -e Azure__OpenAI__Endpoint="https://your-resource.openai.azure.com" \
  -e Azure__OpenAI__ApiKey="your-api-key" \
  -e Azure__OpenAI__ModelDeploymentName="gpt-4o" \
  -e Azure__Speech__ApiKey="your-speech-key" \
  -e Azure__Speech__Region="eastus2" \
  voicelive-api:latest
```

## Troubleshooting

### WebSocket Connection Fails

- Verify backend is running on port 5000
- Check `appsettings.Development.json` has correct Azure credentials
- Ensure CORS settings in `Program.cs` include `http://localhost:5173`
- Check browser console for `[WebSocket]` logs to see connection attempts
- Verify Vite proxy is forwarding `/ws` requests to backend

### Voice Not Working

- Confirm Azure Voice Live API access is enabled for your resource
- Check browser console for WebSocket errors
- Verify agent is created successfully before connecting
- Ensure agent_id is not `undefined` in console logs (should be `local-agent-*`)
- Check microphone permissions are granted in browser

### Agent Creation Issues

- If you see `agent_id: undefined` in console, verify the backend response format
- The API must return `agent_id` in snake_case format, not `Id` or `AgentId`
- Test agent creation: `curl -X POST http://localhost:5000/api/agents/create -H "Content-Type: application/json" -d '{"scenario_id":"scenario3-role-play"}'`
- Expected response should include `"agent_id": "local-agent-..."` field

### Recording Button Not Responding

- Check browser console for `[App] Toggle recording called` log
- Verify `[App] Connected: true` when clicking Start Recording
- Ensure WebSocket connection is established before recording
- Check if microphone permission prompt appears and grant access
- Look for `[Recorder]` logs indicating audio capture status

### Azure Authentication Errors

- Verify API keys in `appsettings.Development.json`
- Check Azure resource endpoints are correct
- Ensure Speech Services region matches configuration
- Test Speech API: `curl http://localhost:5000/api/test/speech`

## Development Tools

### Recommended VS Code Extensions

- C# Dev Kit
- .NET Extension Pack
- REST Client (for testing endpoints)

### Debugging Tips

**Browser Console Logs:**
- `[App]` - Application state changes (agent creation, recording)
- `[WebSocket]` - Connection status and message flow
- `[Recorder]` - Audio capture status

**Backend Logs:**
- Check for `[WebSocket] Connection request received` when frontend connects
- Look for agent creation: `Created agent: local-agent-*`
- Monitor Azure Voice Live API connection status

**Testing Endpoints:**
```bash
# Test configuration
curl http://localhost:5000/api/config

# Create agent
curl -X POST http://localhost:5000/api/agents/create \
  -H "Content-Type: application/json" \
  -d '{"scenario_id":"scenario3-role-play"}'

# List scenarios
curl http://localhost:5000/api/scenarios
```

## License

See LICENSE file in the root directory.
