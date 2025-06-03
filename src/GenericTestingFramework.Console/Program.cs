using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Services;
using GenericTestingFramework.Services.LLM;
using GenericTestingFramework.Services.Repository;
using GenericTestingFramework.Services.Executors;
using GenericTestingFramework.Services.Documents;
using GenericTestingFramework.Services.TestGeneration;

Console.WriteLine("🚀 Dynamic Test Generation Framework - Console Application");
Console.WriteLine("========================================================");

try
{
    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    // Setup DI container
    var services = new ServiceCollection();
    
    // Add logging
    services.AddLogging(builder => 
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // Add configuration
    services.AddSingleton<IConfiguration>(configuration);

    // Configure LLM service
    services.Configure<LLMConfiguration>(
        configuration.GetSection(LLMConfiguration.SectionName));

    // Configure UI Test settings
    services.Configure<UITestConfiguration>(
        configuration.GetSection("UITestConfiguration"));

    // Configure API Test settings
    services.Configure<APITestConfiguration>(
        configuration.GetSection("APITestConfiguration"));

    // Add HTTP clients
    services.AddHttpClient<OpenAILLMService>();
    services.AddHttpClient<APITestExecutor>();

    // Register document manager
    services.AddSingleton<IDocumentManager, DocumentManager>();

    // Register framework services with dynamic test generator
    services.AddSingleton<ILLMService, DynamicTestGenerator>();
    services.AddSingleton<ITestRepository, InMemoryTestRepository>();

    // Register test executors
    services.AddTransient<ITestExecutor, UITestExecutor>();
    services.AddTransient<ITestExecutor, MockAPITestExecutor>();

    // Register main service
    services.AddTransient<TestAutomationService>();

    var serviceProvider = services.BuildServiceProvider();
    var testService = serviceProvider.GetRequiredService<TestAutomationService>();
    var documentManager = serviceProvider.GetRequiredService<IDocumentManager>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting Dynamic Test Generation Framework");

    // Demo: Create user story from text input
    Console.WriteLine("\n📝 Dynamic Test Generation Demo");
    Console.WriteLine("===============================");

    // Option 1: Interactive user story input
    Console.WriteLine("\nEnter your user story (or press Enter for demo story):");
    var userInput = Console.ReadLine();

    string userStory;
    string projectContext;

    if (string.IsNullOrWhiteSpace(userInput))
    {
        // Default demo story for Confessions Portal
        userStory = @"Test Authentication with credentials admin@confess.com, Admin@123 
                     for Confessions tracking Admin Portal at https://maidencube.com/cube-admin-prod/";
        projectContext = "Confessions tracking Admin Portal - Authentication and access control system";
        Console.WriteLine("Using demo story: Confessions Portal Authentication Test");
    }
    else
    {
        userStory = userInput;
        Console.WriteLine("\nEnter project context (optional):");
        projectContext = Console.ReadLine() ?? "";
    }

    // Create document from user story
    Console.WriteLine("\n📄 Creating document from user story...");
    var document = await documentManager.CreateUserStoryFromText(userStory, projectContext);
    Console.WriteLine($"✅ Created document: {document.FileName}");
    Console.WriteLine($"   Content length: {document.Content.Length} characters");
    Console.WriteLine($"   Project context: {document.ProjectContext}");

    // Generate test scenario dynamically
    Console.WriteLine("\n🔄 Generating test scenario dynamically...");
    var scenarioId = await testService.CreateTestFromUserStory(
        userStory, 
        "dynamic-testing",
        projectContext);

    Console.WriteLine($"✅ Generated test scenario: {scenarioId}");

    // Get the generated scenario details
    var projectTests = await testService.GetProjectTests("dynamic-testing");
    var generatedScenario = projectTests.FirstOrDefault();

    if (generatedScenario != null)
    {
        Console.WriteLine($"\n📋 Generated Test Scenario Details:");
        Console.WriteLine($"   Title: {generatedScenario.Title}");
        Console.WriteLine($"   Type: {generatedScenario.Type}");
        Console.WriteLine($"   Priority: {generatedScenario.Priority}");
        Console.WriteLine($"   Steps: {generatedScenario.Steps.Count}");
        Console.WriteLine($"   Tags: {string.Join(", ", generatedScenario.Tags)}");

        Console.WriteLine($"\n📋 Generated Test Steps:");
        foreach (var step in generatedScenario.Steps.OrderBy(s => s.Order))
        {
            Console.WriteLine($"   {step.Order}. {step.Action} - {step.Description}");
            Console.WriteLine($"      Target: {step.Target}");
            if (step.Parameters.Any())
            {
                Console.WriteLine($"      Parameters: {string.Join(", ", step.Parameters.Select(p => $"{p.Key}={p.Value}"))}");
            }
        }

        Console.WriteLine($"\n📋 Preconditions:");
        foreach (var precondition in generatedScenario.Preconditions)
        {
            Console.WriteLine($"   • {precondition}");
        }

        Console.WriteLine($"\n📋 Expected Outcomes:");
        foreach (var outcome in generatedScenario.ExpectedOutcomes)
        {
            Console.WriteLine($"   • {outcome}");
        }
    }

    // Ask user if they want to execute the test
    Console.WriteLine("\n🚀 Execute the generated test? (y/n):");
    var executeChoice = Console.ReadLine()?.ToLowerInvariant();

    if (executeChoice == "y" || executeChoice == "yes")
    {
        Console.WriteLine("\n🔄 Executing dynamically generated test...");
        
        var result = await testService.ExecuteTest(scenarioId);
        Console.WriteLine($"\n✅ Test Execution Result: {(result.Passed ? "PASSED" : "FAILED")}");
        Console.WriteLine($"   Duration: {result.Duration}");
        Console.WriteLine($"   Steps executed: {result.StepResults.Count}");
        Console.WriteLine($"   Success rate: {result.GetSuccessRate():F1}%");

        if (!result.Passed)
        {
            Console.WriteLine($"\n❌ Test Failed: {result.Message}");
            var firstFailure = result.GetFirstFailure();
            if (firstFailure != null)
            {
                Console.WriteLine($"   First Failed Step: {firstFailure.StepName}");
                Console.WriteLine($"   Error: {firstFailure.Message}");
            }
        }

        // Show detailed step results
        Console.WriteLine("\n📋 Detailed Step Results:");
        foreach (var stepResult in result.StepResults)
        {
            var status = stepResult.Passed ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"   {status} - {stepResult.StepName}");
            Console.WriteLine($"          Action: {stepResult.Action} on {stepResult.Target}");
            Console.WriteLine($"          Duration: {stepResult.Duration.TotalMilliseconds:F0}ms");
            if (!stepResult.Passed)
            {
                Console.WriteLine($"          Error: {stepResult.Message}");
            }
        }
    }

    // Show document management capabilities
    Console.WriteLine("\n📚 Document Management:");
    var allDocuments = await documentManager.GetUserStories("");
    Console.WriteLine($"   Total user story documents: {allDocuments.Count}");
    
    foreach (var doc in allDocuments)
    {
        Console.WriteLine($"   • {doc.FileName} (uploaded: {doc.UploadedAt:yyyy-MM-dd HH:mm})");
        Console.WriteLine($"     Context: {doc.ProjectContext}");
    }

    // Option to upload a file
    Console.WriteLine("\n📁 Upload a user story file? (Enter file path or press Enter to skip):");
    var filePath = Console.ReadLine();
    
    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
    {
        try
        {
            var uploadedDoc = await documentManager.UploadUserStory(filePath);
            Console.WriteLine($"✅ Uploaded file: {uploadedDoc.FileName}");
            
            // Generate test from uploaded file
            var fileScenarioId = await testService.CreateTestFromUserStory(
                uploadedDoc.Content,
                "uploaded-tests",
                uploadedDoc.ProjectContext);
            
            Console.WriteLine($"✅ Generated test from uploaded file: {fileScenarioId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to upload file: {ex.Message}");
        }
    }

    // Get test statistics
    Console.WriteLine("\n📊 Test Execution Statistics:");
    var fromDate = DateTime.UtcNow.AddDays(-1);
    var toDate = DateTime.UtcNow;
    
    var stats = await testService.GetTestStatistics("dynamic-testing", fromDate, toDate);
    Console.WriteLine($"   Total Scenarios: {stats.TotalScenarios}");
    Console.WriteLine($"   Total Executions: {stats.TotalExecutions}");
    Console.WriteLine($"   Pass Rate: {stats.PassRate:F1}%");
    Console.WriteLine($"   Average Duration: {stats.AverageDuration}");

    // Health check
    Console.WriteLine("\n🏥 Executor Health Status:");
    var healthStatus = await testService.GetExecutorHealthStatus();
    
    foreach (var executor in healthStatus)
    {
        var status = executor.Value.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
        Console.WriteLine($"   {executor.Key}: {status} ({executor.Value.ResponseTime.TotalMilliseconds:F0}ms)");
    }

    Console.WriteLine("\n🎉 Dynamic Test Generation Completed!");
    Console.WriteLine("\n💡 Framework Features Demonstrated:");
    Console.WriteLine("   ✓ Dynamic test generation from user stories");
    Console.WriteLine("   ✓ User story document management");
    Console.WriteLine("   ✓ Automatic analysis of user story content");
    Console.WriteLine("   ✓ Smart extraction of URLs, credentials, and actions");
    Console.WriteLine("   ✓ Context-aware test step generation");
    Console.WriteLine("   ✓ File upload support for user stories");
    Console.WriteLine("   ✓ Real-time test execution with detailed reporting");

    logger.LogInformation("Dynamic Test Generation Framework demo completed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

// Configuration classes for Console app
public class UITestConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public bool Headless { get; set; } = false; // Set to false to see the browser during testing
    public string WindowSize { get; set; } = "1920,1080";
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int ImplicitWaitSeconds { get; set; } = 10;
    public int MaxParallelSessions { get; set; } = 1;
    public string ScreenshotPath { get; set; } = "screenshots";
    public bool CaptureScreenshotOnFailure { get; set; } = true; // Enable screenshots for debugging
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}

public class APITestConfiguration
{
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 3;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}

// Keep the mock API executor for now since we're focusing on UI testing
public class MockAPITestExecutor : ITestExecutor
{
    public string Name => "Mock API Test Executor";

    public bool CanExecute(GenericTestingFramework.Core.Models.TestType testType) => 
        testType == GenericTestingFramework.Core.Models.TestType.API || 
        testType == GenericTestingFramework.Core.Models.TestType.Mixed;

    public async Task<GenericTestingFramework.Core.Models.TestResult> ExecuteTest(
        GenericTestingFramework.Core.Models.TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var result = new GenericTestingFramework.Core.Models.TestResult
        {
            ScenarioId = scenario.Id,
            StartedAt = DateTime.UtcNow,
            Environment = GenericTestingFramework.Core.Models.TestEnvironment.Testing
        };

        await Task.Delay(100, cancellationToken);
        result.Complete();
        return result;
    }

    public Task<GenericTestingFramework.Core.Interfaces.ExecutorValidationResult> ValidateScenario(
        GenericTestingFramework.Core.Models.TestScenario scenario)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.ExecutorValidationResult
        {
            CanExecute = true,
            Messages = new List<string> { "Mock API validation passed" }
        });
    }

    public GenericTestingFramework.Core.Interfaces.ExecutorCapabilities GetCapabilities()
    {
        return new GenericTestingFramework.Core.Interfaces.ExecutorCapabilities
        {
            SupportedTestTypes = new List<GenericTestingFramework.Core.Models.TestType> 
            { 
                GenericTestingFramework.Core.Models.TestType.API, 
                GenericTestingFramework.Core.Models.TestType.Mixed 
            },
            SupportedActions = new List<string> { "api_get", "api_post", "verify_status", "verify_body" },
            MaxParallelExecutions = 3,
            SupportsScreenshots = false
        };
    }

    public Task<GenericTestingFramework.Core.Interfaces.HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.ExecutorHealthCheckResult
        {
            IsHealthy = true,
            Message = "Mock API executor is healthy",
            ResponseTime = TimeSpan.FromMilliseconds(25)
        });
    }

    public Task<bool> Initialize(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task Cleanup(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}