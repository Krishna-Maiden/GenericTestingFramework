using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Services;
using GenericTestingFramework.Services.LLM;
using GenericTestingFramework.Services.Repository;
using GenericTestingFramework.Services.Executors;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "AI-Powered Generic Testing Framework API", 
        Version = "v1",
        Description = "AI-powered generic testing framework for UI and API automation with natural language test generation"
    });
    c.EnableAnnotations();
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure LLM service
builder.Services.Configure<LLMConfiguration>(
    builder.Configuration.GetSection(LLMConfiguration.SectionName));

// Configure UI Test settings
builder.Services.Configure<UITestConfiguration>(
    builder.Configuration.GetSection("UITestConfiguration"));

// Configure API Test settings  
builder.Services.Configure<APITestConfiguration>(
    builder.Configuration.GetSection("APITestConfiguration"));

// Add HTTP client for LLM service
builder.Services.AddHttpClient<OpenAILLMService>();

// Add HTTP client for API testing
builder.Services.AddHttpClient<APITestExecutor>();

// Register framework services
builder.Services.AddSingleton<ILLMService, OpenAILLMService>();
builder.Services.AddSingleton<ITestRepository, InMemoryTestRepository>();

// Register test executors
builder.Services.AddTransient<ITestExecutor, UITestExecutor>();
builder.Services.AddTransient<ITestExecutor, APITestExecutor>();

// Register main service
builder.Services.AddTransient<TestAutomationService>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    if (builder.Environment.IsDevelopment())
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Testing Framework API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
        c.DocumentTitle = "AI Testing Framework API";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Add health check endpoint
app.MapGet("/health", async (TestAutomationService testService) =>
{
    try
    {
        var healthStatus = await testService.GetExecutorHealthStatus();
        var overallHealth = healthStatus.Values.All(h => h.IsHealthy);
        
        return Results.Ok(new
        {
            Status = overallHealth ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = app.Environment.EnvironmentName,
            Executors = healthStatus
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Health check failed: {ex.Message}");
    }
})
.WithName("HealthCheck")
.WithTags("Health");

// Add version endpoint
app.MapGet("/version", () => Results.Ok(new
{
    Version = "1.0.0",
    BuildDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
    Framework = ".NET 8.0",
    Description = "AI-Powered Generic Testing Framework"
}))
.WithName("GetVersion")
.WithTags("Info");

// Add test creation endpoint
app.MapPost("/api/tests/create", async (
    CreateTestRequest request,
    TestAutomationService testService) =>
{
    try
    {
        var scenarioId = await testService.CreateTestFromUserStory(
            request.UserStory,
            request.ProjectId,
            request.ProjectContext ?? "");

        return Results.Ok(new { ScenarioId = scenarioId, Message = "Test scenario created successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to create test: {ex.Message}");
    }
})
.WithName("CreateTest")
.WithTags("Tests");

// Add test execution endpoint
app.MapPost("/api/tests/{scenarioId}/execute", async (
    string scenarioId,
    TestAutomationService testService) =>
{
    try
    {
        var result = await testService.ExecuteTest(scenarioId);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to execute test: {ex.Message}");
    }
})
.WithName("ExecuteTest")
.WithTags("Tests");

// Add get project tests endpoint
app.MapGet("/api/projects/{projectId}/tests", async (
    string projectId,
    TestAutomationService testService) =>
{
    try
    {
        var tests = await testService.GetProjectTests(projectId);
        return Results.Ok(tests);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get project tests: {ex.Message}");
    }
})
.WithName("GetProjectTests")
.WithTags("Projects");

// Add test statistics endpoint
app.MapGet("/api/projects/{projectId}/statistics", async (
    string projectId,
    DateTime? fromDate,
    DateTime? toDate,
    TestAutomationService testService) =>
{
    try
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;
        
        var stats = await testService.GetTestStatistics(projectId, from, to);
        return Results.Ok(stats);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get statistics: {ex.Message}");
    }
})
.WithName("GetProjectStatistics")
.WithTags("Projects");

app.Run();

// DTOs for API endpoints
public record CreateTestRequest(string UserStory, string ProjectId, string? ProjectContext = null);

// Configuration classes
public class UITestConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public bool Headless { get; set; } = false;
    public string WindowSize { get; set; } = "1920,1080";
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int ImplicitWaitSeconds { get; set; } = 10;
    public int MaxParallelSessions { get; set; } = 3;
    public string ScreenshotPath { get; set; } = "screenshots";
    public bool CaptureScreenshotOnFailure { get; set; } = true;
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}

public class APITestConfiguration
{
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 5;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}