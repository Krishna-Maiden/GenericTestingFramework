using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using GenericTestingFramework.Services;

namespace GenericTestingFramework.Tests.Unit;

public class TestAutomationServiceTests
{
    private readonly Mock<ILLMService> _mockLLMService;
    private readonly Mock<ITestRepository> _mockRepository;
    private readonly Mock<ITestExecutor> _mockUIExecutor;
    private readonly Mock<ITestExecutor> _mockAPIExecutor;
    private readonly Mock<ILogger<TestAutomationService>> _mockLogger;
    private readonly TestAutomationService _service;

    public TestAutomationServiceTests()
    {
        _mockLLMService = new Mock<ILLMService>();
        _mockRepository = new Mock<ITestRepository>();
        _mockUIExecutor = new Mock<ITestExecutor>();
        _mockAPIExecutor = new Mock<ITestExecutor>();
        _mockLogger = new Mock<ILogger<TestAutomationService>>();

        _mockUIExecutor.Setup(x => x.Name).Returns("UI Test Executor");
        _mockUIExecutor.Setup(x => x.CanExecute(TestType.UI)).Returns(true);
        _mockUIExecutor.Setup(x => x.CanExecute(TestType.Mixed)).Returns(true);
        
        _mockAPIExecutor.Setup(x => x.Name).Returns("API Test Executor");
        _mockAPIExecutor.Setup(x => x.CanExecute(TestType.API)).Returns(true);
        _mockAPIExecutor.Setup(x => x.CanExecute(TestType.Mixed)).Returns(true);

        _service = new TestAutomationService(
            _mockLLMService.Object,
            _mockRepository.Object,
            new[] { _mockUIExecutor.Object, _mockAPIExecutor.Object },
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateTestFromUserStory_ShouldReturnScenarioId()
    {
        // Arrange
        var userStory = "As a user, I want to login to access my dashboard";
        var projectId = "test-project";
        var projectContext = "Web application with authentication";
        
        var expectedScenario = new TestScenario
        {
            Id = "test-scenario-1",
            Title = "User Login Test",
            ProjectId = projectId,
            Type = TestType.UI,
            Status = TestStatus.Generated,
            Steps = new List<TestStep>
            {
                new TestStep
                {
                    Order = 1,
                    Action = "navigate",
                    Target = "/login",
                    Description = "Navigate to login page"
                }
            }
        };

        _mockLLMService
            .Setup(x => x.GenerateTestFromNaturalLanguage(userStory, projectContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedScenario);

        _mockRepository
            .Setup(x => x.SaveScenario(It.IsAny<TestScenario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedScenario.Id);

        // Act
        var result = await _service.CreateTestFromUserStory(userStory, projectId, projectContext);

        // Assert
        result.Should().Be(expectedScenario.Id);
        _mockLLMService.Verify(x => x.GenerateTestFromNaturalLanguage(userStory, projectContext, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.SaveScenario(It.Is<TestScenario>(s => 
            s.ProjectId == projectId && 
            s.Status == TestStatus.Ready), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTest_WithValidUIScenario_ShouldReturnSuccessfulResult()
    {
        // Arrange
        var scenarioId = "test-scenario-1";
        var scenario = new TestScenario
        {
            Id = scenarioId,
            Type = TestType.UI,
            Status = TestStatus.Ready,
            Steps = new List<TestStep>
            {
                new TestStep 
                { 
                    Order = 1, 
                    Action = "navigate", 
                    Target = "https://example.com",
                    IsEnabled = true
                }
            }
        };

        var expectedResult = new TestResult
        {
            ScenarioId = scenarioId,
            Passed = true,
            Message = "Test completed successfully",
            StepResults = new List<StepResult>
            {
                new StepResult
                {
                    Passed = true,
                    Action = "navigate",
                    Message = "Navigation successful"
                }
            }
        };

        _mockRepository
            .Setup(x => x.GetScenario(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario);

        _mockUIExecutor
            .Setup(x => x.ExecuteTest(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        _mockRepository
            .Setup(x => x.SaveResult(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult.Id);

        _mockRepository
            .Setup(x => x.UpdateScenario(It.IsAny<TestScenario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ExecuteTest(scenarioId);

        // Assert
        result.Should().NotBeNull();
        result.Passed.Should().BeTrue();
        result.ScenarioId.Should().Be(scenarioId);
        result.Message.Should().Be("Test completed successfully");

        _mockRepository.Verify(x => x.GetScenario(scenarioId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUIExecutor.Verify(x => x.ExecuteTest(scenario, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.SaveResult(expectedResult, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.UpdateScenario(It.Is<TestScenario>(s => 
            s.Status == TestStatus.Completed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTest_WithValidAPIScenario_ShouldReturnSuccessfulResult()
    {
        // Arrange
        var scenarioId = "api-test-scenario";
        var scenario = new TestScenario
        {
            Id = scenarioId,
            Type = TestType.API,
            Status = TestStatus.Ready,
            Steps = new List<TestStep>
            {
                new TestStep 
                { 
                    Order = 1, 
                    Action = "api_get", 
                    Target = "https://api.example.com/users",
                    IsEnabled = true
                }
            }
        };

        var expectedResult = new TestResult
        {
            ScenarioId = scenarioId,
            Passed = true,
            Message = "API test completed successfully"
        };

        _mockRepository
            .Setup(x => x.GetScenario(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario);

        _mockAPIExecutor
            .Setup(x => x.ExecuteTest(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        _mockRepository
            .Setup(x => x.SaveResult(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult.Id);

        _mockRepository
            .Setup(x => x.UpdateScenario(It.IsAny<TestScenario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ExecuteTest(scenarioId);

        // Assert
        result.Should().NotBeNull();
        result.Passed.Should().BeTrue();
        result.ScenarioId.Should().Be(scenarioId);

        _mockAPIExecutor.Verify(x => x.ExecuteTest(scenario, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTest_WithNonExistentScenario_ShouldThrowArgumentException()
    {
        // Arrange
        var scenarioId = "non-existent-scenario";

        _mockRepository
            .Setup(x => x.GetScenario(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestScenario?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.ExecuteTest(scenarioId));
        exception.Message.Should().Contain($"Scenario {scenarioId} not found");
    }

    [Fact]
    public async Task ExecuteTest_WithUnsupportedTestType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var scenarioId = "unsupported-scenario";
        var scenario = new TestScenario
        {
            Id = scenarioId,
            Type = TestType.Database, // Unsupported type
            Status = TestStatus.Ready
        };

        _mockRepository
            .Setup(x => x.GetScenario(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => _service.ExecuteTest(scenarioId));
        exception.Message.Should().Contain($"No executor available for test type {TestType.Database}");
    }

    [Fact]
    public async Task GetProjectTests_ShouldReturnTestsForProject()
    {
        // Arrange
        var projectId = "test-project";
        var expectedTests = new List<TestScenario>
        {
            new TestScenario { Id = "test1", ProjectId = projectId, Title = "Test 1" },
            new TestScenario { Id = "test2", ProjectId = projectId, Title = "Test 2" }
        };

        _mockRepository
            .Setup(x => x.GetScenariosByProject(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTests);

        // Act
        var result = await _service.GetProjectTests(projectId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(expectedTests);
        _mockRepository.Verify(x => x.GetScenariosByProject(projectId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTestsParallel_ShouldExecuteMultipleTestsConcurrently()
    {
        // Arrange
        var scenarioIds = new List<string> { "test1", "test2", "test3" };
        var scenarios = scenarioIds.Select(id => new TestScenario
        {
            Id = id,
            Type = TestType.UI,
            Status = TestStatus.Ready,
            Steps = new List<TestStep> { new TestStep { Order = 1, Action = "navigate", Target = "/", IsEnabled = true } }
        }).ToList();

        var expectedResults = scenarioIds.Select(id => new TestResult
        {
            ScenarioId = id,
            Passed = true,
            Message = "Test completed"
        }).ToList();

        for (int i = 0; i < scenarioIds.Count; i++)
        {
            _mockRepository
                .Setup(x => x.GetScenario(scenarioIds[i], It.IsAny<CancellationToken>()))
                .ReturnsAsync(scenarios[i]);

            _mockUIExecutor
                .Setup(x => x.ExecuteTest(scenarios[i], It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults[i]);

            _mockRepository
                .Setup(x => x.SaveResult(expectedResults[i], It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults[i].Id);

            _mockRepository
                .Setup(x => x.UpdateScenario(scenarios[i], It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        // Act
        var results = await _service.ExecuteTestsParallel(scenarioIds, maxConcurrency: 2);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Passed);
        _mockUIExecutor.Verify(x => x.ExecuteTest(It.IsAny<TestScenario>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetExecutorHealthStatus_ShouldReturnHealthForAllExecutors()
    {
        // Arrange
        var uiHealth = new HealthCheckResult { IsHealthy = true, Message = "UI Executor healthy" };
        var apiHealth = new HealthCheckResult { IsHealthy = true, Message = "API Executor healthy" };

        _mockUIExecutor
            .Setup(x => x.PerformHealthCheck(It.IsAny<CancellationToken>()))
            .ReturnsAsync(uiHealth);

        _mockAPIExecutor
            .Setup(x => x.PerformHealthCheck(It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiHealth);

        // Act
        var result = await _service.GetExecutorHealthStatus();

        // Assert
        result.Should().HaveCount(2);
        result["UI Test Executor"].Should().Be(uiHealth);
        result["API Test Executor"].Should().Be(apiHealth);
        result.Values.Should().OnlyContain(h => h.IsHealthy);
    }

    [Fact]
    public async Task AnalyzeFailure_WithFailedTest_ShouldReturnAnalysis()
    {
        // Arrange
        var scenarioId = "failed-scenario";
        var failedResult = new TestResult
        {
            ScenarioId = scenarioId,
            Passed = false,
            Message = "Test failed",
            StartedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var expectedAnalysis = "The test failed due to element not found. Consider updating the selector.";

        _mockRepository
            .Setup(x => x.GetResults(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestResult> { failedResult });

        _mockLLMService
            .Setup(x => x.AnalyzeTestFailure(failedResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAnalysis);

        // Act
        var result = await _service.AnalyzeFailure(scenarioId);

        // Assert
        result.Should().Be(expectedAnalysis);
        _mockLLMService.Verify(x => x.AnalyzeTestFailure(failedResult, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloneTestScenario_ShouldCreateCopyWithNewId()
    {
        // Arrange
        var originalId = "original-scenario";
        var newTitle = "Cloned Test Scenario";
        
        var originalScenario = new TestScenario
        {
            Id = originalId,
            Title = "Original Test",
            ProjectId = "test-project",
            Type = TestType.UI,
            Steps = new List<TestStep>
            {
                new TestStep { Order = 1, Action = "navigate", Target = "/" }
            }
        };

        var clonedId = "cloned-scenario-id";

        _mockRepository
            .Setup(x => x.GetScenario(originalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalScenario);

        _mockRepository
            .Setup(x => x.SaveScenario(It.IsAny<TestScenario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clonedId);

        // Act
        var result = await _service.CloneTestScenario(originalId, newTitle);

        // Assert
        result.Should().Be(clonedId);
        _mockRepository.Verify(x => x.SaveScenario(It.Is<TestScenario>(s => 
            s.Title == newTitle && 
            s.Id != originalId && 
            s.Status == TestStatus.Draft &&
            s.Steps.Count == originalScenario.Steps.Count), It.IsAny<CancellationToken>()), Times.Once);
    }
}