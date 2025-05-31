using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Services;
using GenericTestingFramework.Services.LLM;
using GenericTestingFramework.Services.Repository;
using GenericTestingFramework.Services.Executors;

Console.WriteLine("🚀 AI-Powered Generic Testing Framework - Console Application");
Console.WriteLine("==============================================================");

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

    // Register framework services
    services.AddSingleton<ILLMService, MockLLMService>(); // Use mock for demo
    services.AddSingleton<ITestRepository, InMemoryTestRepository>();

    // Register test executors
    services.AddTransient<ITestExecutor, MockUITestExecutor>(); // Use mock for demo
    services.AddTransient<ITestExecutor, MockAPITestExecutor>(); // Use mock for demo

    // Register main service
    services.AddTransient<TestAutomationService>();

    var serviceProvider = services.BuildServiceProvider();
    var testService = serviceProvider.GetRequiredService<TestAutomationService>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting AI Testing Framework Console Demo");

    // Demo 1: Create test from insurance user story
    Console.WriteLine("\n📝 Demo 1: Creating test from Insurance user story...");
    var insuranceUserStory = @"
        As a SafeGuard Insurance customer, 
        I want to get an auto insurance quote online 
        so that I can compare rates and coverage options for my vehicle.
    ";

    var scenarioId1 = await testService.CreateTestFromUserStory(
        insuranceUserStory, 
        "safeguard-insurance",
        "SafeGuard Insurance Platform - Auto insurance quoting system with VIN validation and premium calculation");

    Console.WriteLine($"✅ Created insurance test scenario: {scenarioId1}");

    // Demo 2: Create test from another user story
    Console.WriteLine("\n📝 Demo 2: Creating test from Customer Portal user story...");
    var portalUserStory = @"
        As a registered customer,
        I want to securely log into my account
        so that I can access my policy information and perform account activities.
    ";

    var scenarioId2 = await testService.CreateTestFromUserStory(
        portalUserStory, 
        "safeguard-insurance",
        "Customer portal with multi-factor authentication and session management");

    Console.WriteLine($"✅ Created portal test scenario: {scenarioId2}");

    // Demo 3: Get project tests
    Console.WriteLine("\n📋 Demo 3: Retrieving project test scenarios...");
    var projectTests = await testService.GetProjectTests("safeguard-insurance");
    
    Console.WriteLine($"✅ Found {projectTests.Count} test scenarios for SafeGuard Insurance:");
    foreach (var test in projectTests)
    {
        Console.WriteLine($"   - {test.Title} ({test.Type}, {test.Steps.Count} steps)");
    }

    // Demo 4: Execute tests
    Console.WriteLine("\n🔄 Demo 4: Executing test scenarios...");
    
    var result1 = await testService.ExecuteTest(scenarioId1);
    Console.WriteLine($"✅ Insurance Quote Test: {(result1.Passed ? "PASSED" : "FAILED")}");
    Console.WriteLine($"   Duration: {result1.Duration}");
    Console.WriteLine($"   Steps executed: {result1.StepResults.Count}");
    Console.WriteLine($"   Success rate: {result1.GetSuccessRate():F1}%");

    var result2 = await testService.ExecuteTest(scenarioId2);
    Console.WriteLine($"✅ Customer Login Test: {(result2.Passed ? "PASSED" : "FAILED")}");
    Console.WriteLine($"   Duration: {result2.Duration}");
    Console.WriteLine($"   Steps executed: {result2.StepResults.Count}");
    Console.WriteLine($"   Success rate: {result2.GetSuccessRate():F1}%");

    // Demo 5: Get test statistics
    Console.WriteLine("\n📊 Demo 5: Getting test execution statistics...");
    var fromDate = DateTime.UtcNow.AddDays(-30);
    var toDate = DateTime.UtcNow;
    
    var stats = await testService.GetTestStatistics("safeguard-insurance", fromDate, toDate);
    Console.WriteLine($"✅ Test Statistics (Last 30 days):");
    Console.WriteLine($"   Total Scenarios: {stats.TotalScenarios}");
    Console.WriteLine($"   Total Executions: {stats.TotalExecutions}");
    Console.WriteLine($"   Pass Rate: {stats.PassRate:F1}%");
    Console.WriteLine($"   Average Duration: {stats.AverageDuration}");

    // Demo 6: Health check
    Console.WriteLine("\n🏥 Demo 6: Checking executor health status...");
    var healthStatus = await testService.GetExecutorHealthStatus();
    
    foreach (var executor in healthStatus)
    {
        var status = executor.Value.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
        Console.WriteLine($"   {executor.Key}: {status} ({executor.Value.ResponseTime.TotalMilliseconds:F0}ms)");
    }

    // Demo 7: Interactive mode
    Console.WriteLine("\n🎯 Demo 7: Interactive Test Creation");
    Console.WriteLine("Enter your own user story (or press Enter to skip):");
    
    var userInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(userInput))
    {
        try
        {
            var customScenarioId = await testService.CreateTestFromUserStory(
                userInput, 
                "custom-project",
                "Custom project for user-defined testing scenarios");

            Console.WriteLine($"✅ Created custom test scenario: {customScenarioId}");

            var customResult = await testService.ExecuteTest(customScenarioId);
            Console.WriteLine($"✅ Custom Test Result: {(customResult.Passed ? "PASSED" : "FAILED")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating custom test: {ex.Message}");
        }
    }

    Console.WriteLine("\n🎉 All demos completed successfully!");
    Console.WriteLine("\n💡 Key Features Demonstrated:");
    Console.WriteLine("   ✓ AI-powered test generation from natural language");
    Console.WriteLine("   ✓ Multiple test executors (UI and API)");
    Console.WriteLine("   ✓ Test execution with detailed results");
    Console.WriteLine("   ✓ Project-based test management");
    Console.WriteLine("   ✓ Comprehensive statistics and reporting");
    Console.WriteLine("   ✓ Health monitoring and diagnostics");

    logger.LogInformation("AI Testing Framework Console Demo completed successfully");
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
    public bool Headless { get; set; } = true; // Default to headless for console
    public string WindowSize { get; set; } = "1920,1080";
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int ImplicitWaitSeconds { get; set; } = 10;
    public int MaxParallelSessions { get; set; } = 1; // Single session for console
    public string ScreenshotPath { get; set; } = "screenshots";
    public bool CaptureScreenshotOnFailure { get; set; } = false; // Disable for console
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}

public class APITestConfiguration
{
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 3;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}

// Mock implementations for demo purposes
public class MockLLMService : ILLMService
{
    public Task<GenericTestingFramework.Core.Models.TestScenario> GenerateTestFromNaturalLanguage(
        string userStory, string projectContext, CancellationToken cancellationToken = default)
    {
        var scenario = new GenericTestingFramework.Core.Models.TestScenario
        {
            Title = ExtractTitleFromUserStory(userStory),
            Description = $"Generated test for: {userStory.Trim()}",
            OriginalUserStory = userStory,
            Type = DetermineTestType(userStory),
            Status = GenericTestingFramework.Core.Models.TestStatus.Generated,
            Priority = GenericTestingFramework.Core.Models.TestPriority.Medium,
            Steps = GenerateStepsFromUserStory(userStory)
        };

        return Task.FromResult(scenario);
    }

    public Task<List<GenericTestingFramework.Core.Models.TestStep>> RefineTestSteps(
        List<GenericTestingFramework.Core.Models.TestStep> steps, string feedback, CancellationToken cancellationToken = default)
    {
        // Mock refinement - just return original steps
        return Task.FromResult(steps);
    }

    public Task<string> AnalyzeTestFailure(
        GenericTestingFramework.Core.Models.TestResult result, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Mock analysis: Test failed due to simulated conditions. Consider checking test data and environment setup.");
    }

    public Task<Dictionary<string, object>> GenerateTestData(
        GenericTestingFramework.Core.Models.TestScenario testScenario, string dataRequirements, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["username"] = "testuser@example.com",
            ["password"] = "TestPassword123!",
            ["vin"] = "1HGBH41JXMN109186",
            ["zipcode"] = "90210"
        });
    }

    public Task<List<GenericTestingFramework.Core.Models.TestScenario>> OptimizeTestScenarios(
        List<GenericTestingFramework.Core.Models.TestScenario> scenarios, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(scenarios);
    }

    public Task<List<GenericTestingFramework.Core.Models.TestScenario>> SuggestAdditionalTests(
        List<GenericTestingFramework.Core.Models.TestScenario> existingScenarios, string projectContext, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<GenericTestingFramework.Core.Models.TestScenario>());
    }

    public Task<GenericTestingFramework.Core.Interfaces.TestValidationResult> ValidateTestScenario(
        GenericTestingFramework.Core.Models.TestScenario scenario, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.TestValidationResult
        {
            IsValid = true,
            QualityScore = 85,
            Issues = new List<string>(),
            Suggestions = new List<string> { "Consider adding more edge case testing" }
        });
    }

    private string ExtractTitleFromUserStory(string userStory)
    {
        if (userStory.Contains("quote", StringComparison.OrdinalIgnoreCase))
            return "Auto Insurance Quote Test";
        if (userStory.Contains("login", StringComparison.OrdinalIgnoreCase))
            return "Customer Login Test";
        if (userStory.Contains("claim", StringComparison.OrdinalIgnoreCase))
            return "Claims Processing Test";
        if (userStory.Contains("payment", StringComparison.OrdinalIgnoreCase))
            return "Payment Processing Test";
        
        return "Generated Test Scenario";
    }

    private GenericTestingFramework.Core.Models.TestType DetermineTestType(string userStory)
    {
        if (userStory.Contains("api", StringComparison.OrdinalIgnoreCase) || 
            userStory.Contains("service", StringComparison.OrdinalIgnoreCase))
            return GenericTestingFramework.Core.Models.TestType.API;
        
        return GenericTestingFramework.Core.Models.TestType.UI;
    }

    private List<GenericTestingFramework.Core.Models.TestStep> GenerateStepsFromUserStory(string userStory)
    {
        var steps = new List<GenericTestingFramework.Core.Models.TestStep>();

        if (userStory.Contains("quote", StringComparison.OrdinalIgnoreCase))
        {
            steps.AddRange(new[]
            {
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 1,
                    Action = "navigate",
                    Target = "/quote",
                    Description = "Navigate to quote page",
                    ExpectedResult = "Quote page loads successfully"
                },
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 2,
                    Action = "enter_text",
                    Target = "#zipcode",
                    Description = "Enter ZIP code",
                    ExpectedResult = "ZIP code entered",
                    Parameters = new Dictionary<string, object> { ["value"] = "90210" }
                },
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 3,
                    Action = "click",
                    Target = "#get-quote-btn",
                    Description = "Click get quote button",
                    ExpectedResult = "Quote form appears"
                }
            });
        }
        else if (userStory.Contains("login", StringComparison.OrdinalIgnoreCase))
        {
            steps.AddRange(new[]
            {
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 1,
                    Action = "navigate",
                    Target = "/login",
                    Description = "Navigate to login page",
                    ExpectedResult = "Login page loads"
                },
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 2,
                    Action = "enter_text",
                    Target = "#username",
                    Description = "Enter username",
                    ExpectedResult = "Username entered",
                    Parameters = new Dictionary<string, object> { ["value"] = "testuser@example.com" }
                },
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 3,
                    Action = "enter_text",
                    Target = "#password",
                    Description = "Enter password",
                    ExpectedResult = "Password entered",
                    Parameters = new Dictionary<string, object> { ["value"] = "TestPassword123!" }
                },
                new GenericTestingFramework.Core.Models.TestStep
                {
                    Order = 4,
                    Action = "click",
                    Target = "#login-button",
                    Description = "Click login button",
                    ExpectedResult = "User logged in successfully"
                }
            });
        }
        else
        {
            steps.Add(new GenericTestingFramework.Core.Models.TestStep
            {
                Order = 1,
                Action = "verify",
                Target = "body",
                Description = "Verify page loads",
                ExpectedResult = "Page content is visible"
            });
        }

        return steps;
    }
}

// Mock executors for demo
public class MockUITestExecutor : BaseTestExecutor, ITestExecutor
{
    public string Name => "Mock UI Test Executor";

    public bool CanExecute(GenericTestingFramework.Core.Models.TestType testType) => 
        testType == GenericTestingFramework.Core.Models.TestType.UI || 
        testType == GenericTestingFramework.Core.Models.TestType.Mixed;

    public async Task<GenericTestingFramework.Core.Models.TestResult> ExecuteTest(
        GenericTestingFramework.Core.Models.TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var result = new GenericTestingFramework.Core.Models.TestResult
        {
            ScenarioId = scenario.Id,
            StartedAt = DateTime.UtcNow
        };

        foreach (var step in scenario.Steps.Where(s => IsUIAction(s.Action)))
        {
            await Task.Delay(500, cancellationToken); // Simulate execution time
            
            var stepResult = new GenericTestingFramework.Core.Models.StepResult
            {
                StepId = step.Id,
                StepName = step.Description,
                Action = step.Action,
                Target = step.Target,
                Passed = true,
                Message = $"Mock execution successful for {step.Action}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(500),
                Duration = TimeSpan.FromMilliseconds(500)
            };

            result.AddStepResult(stepResult);
        }

        result.Complete();
        return result;
    }

    public Task<GenericTestingFramework.Core.Interfaces.ExecutorValidationResult> ValidateScenario(
        GenericTestingFramework.Core.Models.TestScenario scenario)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.ExecutorValidationResult
        {
            CanExecute = true,
            Messages = new List<string> { "Mock validation successful" }
        });
    }

    public GenericTestingFramework.Core.Interfaces.ExecutorCapabilities GetCapabilities()
    {
        return new GenericTestingFramework.Core.Interfaces.ExecutorCapabilities
        {
            SupportedTestTypes = new List<GenericTestingFramework.Core.Models.TestType> 
            { 
                GenericTestingFramework.Core.Models.TestType.UI, 
                GenericTestingFramework.Core.Models.TestType.Mixed 
            },
            SupportedActions = new List<string> { "navigate", "click", "enter_text", "verify" },
            MaxParallelExecutions = 1,
            SupportsScreenshots = false
        };
    }

    public Task<GenericTestingFramework.Core.Interfaces.HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.HealthCheckResult
        {
            IsHealthy = true,
            Message = "Mock UI executor is healthy",
            ResponseTime = TimeSpan.FromMilliseconds(100)
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

public class MockAPITestExecutor : BaseTestExecutor, ITestExecutor
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
            StartedAt = DateTime.UtcNow
        };

        foreach (var step in scenario.Steps.Where(s => IsAPIAction(s.Action)))
        {
            await Task.Delay(300, cancellationToken); // Simulate API call time
            
            var stepResult = new GenericTestingFramework.Core.Models.StepResult
            {
                StepId = step.Id,
                StepName = step.Description,
                Action = step.Action,
                Target = step.Target,
                Passed = true,
                Message = $"Mock API call successful for {step.Action}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(300),
                Duration = TimeSpan.FromMilliseconds(300)
            };

            result.AddStepResult(stepResult);
        }

        result.Complete();
        return result;
    }

    public Task<GenericTestingFramework.Core.Interfaces.ExecutorValidationResult> ValidateScenario(
        GenericTestingFramework.Core.Models.TestScenario scenario)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.ExecutorValidationResult
        {
            CanExecute = true,
            Messages = new List<string> { "Mock validation successful" }
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
            MaxParallelExecutions = 5,
            SupportsScreenshots = false
        };
    }

    public Task<GenericTestingFramework.Core.Interfaces.HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.HealthCheckResult
        {
            IsHealthy = true,
            Message = "Mock API executor is healthy",
            ResponseTime = TimeSpan.FromMilliseconds(50)
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