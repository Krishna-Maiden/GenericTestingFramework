using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using GenericTestingFramework.Services;
using GenericTestingFramework.Services.LLM;
using GenericTestingFramework.Services.Repository;
using GenericTestingFramework.Services.Executors;

namespace GenericTestingFramework.Tests.Integration;

public class TestExecutionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TestExecutionIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateAndExecuteTest_EndToEnd_ShouldSucceed()
    {
        // Arrange
        var createRequest = new
        {
            UserStory = "As a user, I want to login to access my account",
            ProjectId = "integration-test-project",
            ProjectContext = "Web application with standard login form"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act - Create test scenario
        var createResponse = await _client.PostAsync("/api/tests/create", content);

        // Assert - Test creation
        createResponse.Should().BeSuccessful();
        var createResponseContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createResponseContent);
        
        createResult.TryGetProperty("scenarioId", out var scenarioIdElement).Should().BeTrue();
        var scenarioId = scenarioIdElement.GetString();
        scenarioId.Should().NotBeNullOrEmpty();

        // Act - Execute test scenario
        var executeResponse = await _client.PostAsync($"/api/tests/{scenarioId}/execute", null);

        // Assert - Test execution
        executeResponse.Should().BeSuccessful();
        var executeResponseContent = await executeResponse.Content.ReadAsStringAsync();
        var executeResult = JsonSerializer.Deserialize<JsonElement>(executeResponseContent);

        executeResult.TryGetProperty("scenarioId", out var resultScenarioId).Should().BeTrue();
        resultScenarioId.GetString().Should().Be(scenarioId);

        executeResult.TryGetProperty("passed", out var passedElement).Should().BeTrue();
        // Note: This might be true or false depending on mock implementation
    }

    [Fact]
    public async Task GetProjectTests_ShouldReturnCreatedTests()
    {
        // Arrange
        var projectId = "test-project-for-listing";
        
        // Create a test scenario first
        var createRequest = new
        {
            UserStory = "As a customer, I want to view my policy details",
            ProjectId = projectId,
            ProjectContext = "Insurance customer portal"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var createResponse = await _client.PostAsync("/api/tests/create", content);
        createResponse.Should().BeSuccessful();

        // Act - Get project tests
        var getResponse = await _client.GetAsync($"/api/projects/{projectId}/tests");

        // Assert
        getResponse.Should().BeSuccessful();
        var responseContent = await getResponse.Content.ReadAsStringAsync();
        var tests = JsonSerializer.Deserialize<JsonElement[]>(responseContent);

        tests.Should().NotBeEmpty();
        tests.Should().Contain(test => 
            test.TryGetProperty("projectId", out var pid) && 
            pid.GetString() == projectId);
    }

    [Fact]
    public async Task GetProjectStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        var projectId = "stats-test-project";
        
        // Create and execute a test to generate statistics
        var createRequest = new
        {
            UserStory = "As a user, I want to submit a claim",
            ProjectId = projectId,
            ProjectContext = "Insurance claims processing system"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var createResponse = await _client.PostAsync("/api/tests/create", content);
        var createResponseContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createResponseContent);
        var scenarioId = createResult.GetProperty("scenarioId").GetString();

        await _client.PostAsync($"/api/tests/{scenarioId}/execute", null);

        // Act - Get statistics
        var fromDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var toDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        
        var statsResponse = await _client.GetAsync($"/api/projects/{projectId}/statistics?fromDate={fromDate}&toDate={toDate}");

        // Assert
        statsResponse.Should().BeSuccessful();
        var statsContent = await statsResponse.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JsonElement>(statsContent);

        stats.TryGetProperty("totalScenarios", out var totalScenarios).Should().BeTrue();
        totalScenarios.GetInt32().Should().BeGreaterOrEqualTo(1);

        stats.TryGetProperty("totalExecutions", out var totalExecutions).Should().BeTrue();
        totalExecutions.GetInt32().Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var healthResult = JsonSerializer.Deserialize<JsonElement>(content);

        healthResult.TryGetProperty("status", out var status).Should().BeTrue();
        // Status could be "Healthy" or "Unhealthy" depending on executor implementations
        status.GetString().Should().BeOneOf("Healthy", "Unhealthy");

        healthResult.TryGetProperty("executors", out var executors).Should().BeTrue();
        executors.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task VersionEndpoint_ShouldReturnVersionInfo()
    {
        // Act
        var response = await _client.GetAsync("/version");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var versionResult = JsonSerializer.Deserialize<JsonElement>(content);

        versionResult.TryGetProperty("version", out var version).Should().BeTrue();
        version.GetString().Should().NotBeNullOrEmpty();

        versionResult.TryGetProperty("framework", out var framework).Should().BeTrue();
        framework.GetString().Should().Contain(".NET");
    }
}

// Integration test with custom service setup
public class ServiceIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Configure logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Configure test services with mocks for integration testing
        services.Configure<LLMConfiguration>(config =>
        {
            config.ApiKey = "test-key";
            config.Model = "test-model";
            config.MaxTokens = 1000;
        });

        services.Configure<UITestConfiguration>(config =>
        {
            config.BaseUrl = "https://test.example.com";
            config.Headless = true;
            config.DefaultTimeoutSeconds = 10;
        });

        services.Configure<APITestConfiguration>(config =>
        {
            config.DefaultTimeoutSeconds = 10;
            config.MaxConcurrentRequests = 2;
        });

        // Add HTTP clients
        services.AddHttpClient<OpenAILLMService>();
        services.AddHttpClient<APITestExecutor>();

        // Register services with mock implementations for testing
        services.AddSingleton<ILLMService, MockLLMServiceForIntegration>();
        services.AddSingleton<ITestRepository, InMemoryTestRepository>();
        services.AddTransient<ITestExecutor, MockUITestExecutorForIntegration>();
        services.AddTransient<ITestExecutor, MockAPITestExecutorForIntegration>();
        services.AddTransient<TestAutomationService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task TestAutomationService_CompleteWorkflow_ShouldWork()
    {
        // Arrange
        var testService = _serviceProvider.GetRequiredService<TestAutomationService>();
        var userStory = "As a customer, I want to purchase auto insurance online";
        var projectId = "integration-test";

        // Act & Assert - Create test
        var scenarioId = await testService.CreateTestFromUserStory(userStory, projectId);
        scenarioId.Should().NotBeNullOrEmpty();

        // Act & Assert - Get project tests
        var projectTests = await testService.GetProjectTests(projectId);
        projectTests.Should().NotBeEmpty();
        projectTests.Should().Contain(t => t.Id == scenarioId);

        // Act & Assert - Execute test
        var result = await testService.ExecuteTest(scenarioId);
        result.Should().NotBeNull();
        result.ScenarioId.Should().Be(scenarioId);

        // Act & Assert - Get test history
        var history = await testService.GetTestHistory(scenarioId);
        history.Should().NotBeEmpty();
        history.Should().Contain(r => r.ScenarioId == scenarioId);

        // Act & Assert - Get executor health status
        var healthStatus = await testService.GetExecutorHealthStatus();
        healthStatus.Should().NotBeEmpty();
        healthStatus.Values.Should().OnlyContain(h => h.IsHealthy);
    }

    [Fact]
    public async Task TestAutomationService_ParallelExecution_ShouldWork()
    {
        // Arrange
        var testService = _serviceProvider.GetRequiredService<TestAutomationService>();
        var projectId = "parallel-test";

        var userStories = new[]
        {
            "As a user, I want to login to the system",
            "As a customer, I want to view my account balance",
            "As a user, I want to update my profile information"
        };

        // Act - Create multiple scenarios
        var scenarioIds = new List<string>();
        foreach (var story in userStories)
        {
            var id = await testService.CreateTestFromUserStory(story, projectId);
            scenarioIds.Add(id);
        }

        // Act - Execute in parallel
        var results = await testService.ExecuteTestsParallel(scenarioIds, maxConcurrency: 2);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Passed); // Mock implementations return success
        results.Select(r => r.ScenarioId).Should().BeEquivalentTo(scenarioIds);
    }

    [Fact]
    public async Task TestAutomationService_CloneScenario_ShouldCreateCopy()
    {
        // Arrange
        var testService = _serviceProvider.GetRequiredService<TestAutomationService>();
        var userStory = "As a user, I want to reset my password";
        var projectId = "clone-test";

        // Act - Create original scenario
        var originalId = await testService.CreateTestFromUserStory(userStory, projectId);

        // Act - Clone scenario
        var clonedId = await testService.CloneTestScenario(originalId, "Cloned Password Reset Test");

        // Assert
        clonedId.Should().NotBeNullOrEmpty();
        clonedId.Should().NotBe(originalId);

        var projectTests = await testService.GetProjectTests(projectId);
        projectTests.Should().HaveCount(2);
        projectTests.Should().Contain(t => t.Id == originalId);
        projectTests.Should().Contain(t => t.Id == clonedId && t.Title == "Cloned Password Reset Test");
    }
}

// Mock implementations for integration testing
public class MockLLMServiceForIntegration : ILLMService
{
    public Task<TestScenario> GenerateTestFromNaturalLanguage(
        string userStory, string projectContext, CancellationToken cancellationToken = default)
    {
        var scenario = new TestScenario
        {
            Title = ExtractTitleFromUserStory(userStory),
            Description = $"Generated test for: {userStory}",
            OriginalUserStory = userStory,
            Type = DetermineTestType(userStory),
            Status = TestStatus.Generated,
            Priority = TestPriority.Medium,
            Steps = GenerateStepsFromUserStory(userStory)
        };

        return Task.FromResult(scenario);
    }

    public Task<List<TestStep>> RefineTestSteps(
        List<TestStep> steps, string feedback, CancellationToken cancellationToken = default)
    {
        // For integration testing, just return the original steps
        return Task.FromResult(steps);
    }

    public Task<string> AnalyzeTestFailure(
        TestResult result, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Integration test analysis for scenario {result.ScenarioId}: Mock failure analysis completed.");
    }

    public Task<Dictionary<string, object>> GenerateTestData(
        TestScenario testScenario, string dataRequirements, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["username"] = "integration.test@example.com",
            ["password"] = "IntegrationTest123!",
            ["zipCode"] = "12345"
        });
    }

    public Task<List<TestScenario>> OptimizeTestScenarios(
        List<TestScenario> scenarios, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(scenarios);
    }

    public Task<List<TestScenario>> SuggestAdditionalTests(
        List<TestScenario> existingScenarios, string projectContext, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<TestScenario>());
    }

    public Task<TestValidationResult> ValidateTestScenario(
        TestScenario scenario, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TestValidationResult
        {
            IsValid = true,
            QualityScore = 90,
            Issues = new List<string>(),
            Suggestions = new List<string> { "Integration test validation completed" }
        });
    }

    private string ExtractTitleFromUserStory(string userStory)
    {
        if (userStory.Contains("login", StringComparison.OrdinalIgnoreCase))
            return "Integration Login Test";
        if (userStory.Contains("purchase", StringComparison.OrdinalIgnoreCase))
            return "Integration Purchase Test";
        if (userStory.Contains("claim", StringComparison.OrdinalIgnoreCase))
            return "Integration Claims Test";
        if (userStory.Contains("view", StringComparison.OrdinalIgnoreCase))
            return "Integration View Test";
        if (userStory.Contains("update", StringComparison.OrdinalIgnoreCase))
            return "Integration Update Test";
        if (userStory.Contains("reset", StringComparison.OrdinalIgnoreCase))
            return "Integration Password Reset Test";
        
        return "Integration Test Scenario";
    }

    private TestType DetermineTestType(string userStory)
    {
        if (userStory.Contains("api", StringComparison.OrdinalIgnoreCase))
            return TestType.API;
        
        return TestType.UI;
    }

    private List<TestStep> GenerateStepsFromUserStory(string userStory)
    {
        var steps = new List<TestStep>();

        if (userStory.Contains("login", StringComparison.OrdinalIgnoreCase))
        {
            steps.AddRange(new[]
            {
                new TestStep
                {
                    Order = 1,
                    Action = "navigate",
                    Target = "/login",
                    Description = "Navigate to login page",
                    ExpectedResult = "Login page loads"
                },
                new TestStep
                {
                    Order = 2,
                    Action = "enter_text",
                    Target = "#username",
                    Description = "Enter username",
                    ExpectedResult = "Username entered",
                    Parameters = new Dictionary<string, object> { ["value"] = "test@example.com" }
                },
                new TestStep
                {
                    Order = 3,
                    Action = "enter_text",
                    Target = "#password",
                    Description = "Enter password",
                    ExpectedResult = "Password entered",
                    Parameters = new Dictionary<string, object> { ["value"] = "password123" }
                },
                new TestStep
                {
                    Order = 4,
                    Action = "click",
                    Target = "#login-button",
                    Description = "Click login button",
                    ExpectedResult = "User logged in successfully"
                }
            });
        }
        else if (userStory.Contains("purchase", StringComparison.OrdinalIgnoreCase))
        {
            steps.AddRange(new[]
            {
                new TestStep
                {
                    Order = 1,
                    Action = "navigate",
                    Target = "/products",
                    Description = "Navigate to products page",
                    ExpectedResult = "Products page loads"
                },
                new TestStep
                {
                    Order = 2,
                    Action = "click",
                    Target = ".product-item:first-child .buy-button",
                    Description = "Click buy button for first product",
                    ExpectedResult = "Product added to cart"
                },
                new TestStep
                {
                    Order = 3,
                    Action = "navigate",
                    Target = "/checkout",
                    Description = "Navigate to checkout",
                    ExpectedResult = "Checkout page loads"
                }
            });
        }
        else
        {
            steps.Add(new TestStep
            {
                Order = 1,
                Action = "navigate",
                Target = "/",
                Description = "Navigate to home page",
                ExpectedResult = "Home page loads"
            });
        }

        return steps;
    }
}

public class MockUITestExecutorForIntegration : ITestExecutor
{
    public string Name => "Integration UI Test Executor";

    public bool CanExecute(TestType testType) => 
        testType == TestType.UI || testType == TestType.Mixed;

    public async Task<TestResult> ExecuteTest(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var result = new TestResult
        {
            ScenarioId = scenario.Id,
            StartedAt = DateTime.UtcNow,
            Environment = TestEnvironment.Testing
        };

        // Simulate test execution
        await Task.Delay(500, cancellationToken);

        foreach (var step in scenario.Steps.Where(s => s.IsEnabled))
        {
            var stepResult = new StepResult
            {
                StepId = step.Id,
                StepName = step.Description,
                Action = step.Action,
                Target = step.Target,
                Passed = true,
                Message = $"Integration test step completed: {step.Action}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(100)
            };

            result.AddStepResult(stepResult);
        }

        result.Complete();
        return result;
    }

    public Task<ExecutorValidationResult> ValidateScenario(TestScenario scenario)
    {
        return Task.FromResult(new ExecutorValidationResult
        {
            CanExecute = true,
            Messages = new List<string> { "Integration UI validation passed" }
        });
    }

    public ExecutorCapabilities GetCapabilities()
    {
        return new ExecutorCapabilities
        {
            SupportedTestTypes = new List<TestType> { TestType.UI, TestType.Mixed },
            SupportedActions = new List<string> { "navigate", "click", "enter_text", "verify" },
            MaxParallelExecutions = 1,
            SupportsScreenshots = false
        };
    }

    public Task<HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthCheckResult
        {
            IsHealthy = true,
            Message = "Integration UI executor is healthy",
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

public class MockAPITestExecutorForIntegration : ITestExecutor
{
    public string Name => "Integration API Test Executor";

    public bool CanExecute(TestType testType) => 
        testType == TestType.API || testType == TestType.Mixed;

    public async Task<TestResult> ExecuteTest(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var result = new TestResult
        {
            ScenarioId = scenario.Id,
            StartedAt = DateTime.UtcNow,
            Environment = TestEnvironment.Testing
        };

        // Simulate API test execution
        await Task.Delay(300, cancellationToken);

        foreach (var step in scenario.Steps.Where(s => s.Action.StartsWith("api_")))
        {
            var stepResult = new StepResult
            {
                StepId = step.Id,
                StepName = step.Description,
                Action = step.Action,
                Target = step.Target,
                Passed = true,
                Message = $"Integration API test completed: {step.Action}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(50),
                Duration = TimeSpan.FromMilliseconds(50)
            };

            result.AddStepResult(stepResult);
        }

        result.Complete();
        return result;
    }

    public Task<ExecutorValidationResult> ValidateScenario(TestScenario scenario)
    {
        return Task.FromResult(new ExecutorValidationResult
        {
            CanExecute = true,
            Messages = new List<string> { "Integration API validation passed" }
        });
    }

    public ExecutorCapabilities GetCapabilities()
    {
        return new ExecutorCapabilities
        {
            SupportedTestTypes = new List<TestType> { TestType.API, TestType.Mixed },
            SupportedActions = new List<string> { "api_get", "api_post", "verify_status", "verify_body" },
            MaxParallelExecutions = 3,
            SupportsScreenshots = false
        };
    }

    public Task<HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthCheckResult
        {
            IsHealthy = true,
            Message = "Integration API executor is healthy",
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