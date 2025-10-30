using VoiceLive.Api.Configuration;
using VoiceLive.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all interfaces
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000);
});

// Add services to the container
builder.Services.AddControllers();

// Configure Azure services settings
builder.Services.Configure<AzureSettings>(builder.Configuration.GetSection("Azure"));

// Register application services
builder.Services.AddSingleton<IScenarioManager, ScenarioManager>();
builder.Services.AddSingleton<IAgentManager, AgentManager>();
builder.Services.AddScoped<IConversationAnalyzer, ConversationAnalyzer>();
builder.Services.AddScoped<IPronunciationAssessor, PronunciationAssessor>();
builder.Services.AddScoped<IGraphScenarioGenerator, GraphScenarioGenerator>();
builder.Services.AddScoped<VoiceProxyWebSocketHandler>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Enable WebSockets
app.UseWebSockets();

app.UseCors();

// Serve static files (React frontend)
app.UseDefaultFiles();
app.UseStaticFiles();

// WebSocket endpoint for voice proxy - handle BEFORE routing
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws/voice")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            Console.WriteLine("[WebSocket] Connection request received");
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var handler = context.RequestServices.GetRequiredService<VoiceProxyWebSocketHandler>();
            await handler.HandleConnectionAsync(webSocket, context.RequestAborted);
        }
        else
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("WebSocket connection required");
        }
    }
    else
    {
        await next(context);
    }
});

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
