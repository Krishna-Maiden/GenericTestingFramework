using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;

namespace GenericTestingFramework.Services;

/// <summary>
/// Main orchestration service for test automation
/// </summary>
public class TestAutomationService
{
    private readonly ILLMService _llmService;
    private readonly ITestRepository _repository;
    private readonly List<ITestExecutor> _executors;
    private readonly ILogger<TestAutomationService> _logger;

    public TestAutomationService(
        ILLMService llmService,
        ITestRepository repository,
        IEnumerable<ITestExecutor> executors,
        ILogger<TestAutomationService> logger)
    {
        _llmService = llmService;
        _repository = repository;
        _executors = executors.ToList();
        _logger = logger;
    }

    /// <summary>
    /// Creates a test scenario from a natural language user story
    /// </summary>
    public async Task<string> CreateTestFromUserStory(
        string userStory, 
        string projectId, 
        string projectContext = "",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating test scenario from user story for project {ProjectId}", projectId);

            // Generate test scenario using LLM
            var scenario = await _llmService.GenerateTestFromNaturalLanguage(userStory, projectContext, cancellationToken);
            
            // Set project and metadata
            scenario.ProjectId = projectId;
            scenario.Status = TestStatus.Ready;
            scenario.CreatedAt = DateTime.UtcNow;
            scenario.UpdatedAt = DateTime.UtcNow;

            // Validate the generated scenario
            var validationErrors = scenario.Validate();
            if (validationErrors.Any())
            {
                _logger.LogWarning("Generated scenario has validation errors: {Errors}", string.Join(", ", validationErrors));
                scenario.Status = TestStatus.Draft;
            }

            // Save the scenario
            var scenarioId = await _repository.SaveScenario(scenario, cancellationToken);
            
            _logger.LogInformation("Test scenario {ScenarioId} created successfully", scenarioId);
            return scenarioId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create test from user story");
            throw;
        }
    }

    /// <summary>
    /// Executes a test scenario
    /// </summary>
    public async Task<TestResult> ExecuteTest(string scenarioId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the scenario
            var scenario = await _repository.GetScenario(scenarioId, cancellationToken);
            if (scenario == null)
                throw new ArgumentException($"Scenario {scenarioId} not found");

            _logger.LogInformation("Executing test scenario {ScenarioId}", scenarioId);

            // Update scenario status
            scenario.Status = TestStatus.Running;
            await _repository.UpdateScenario(scenario, cancellationToken);

            // Find appropriate executor
            var executor = _executors.FirstOrDefault(e => e.CanExecute(scenario.Type));
            if (executor == null)
                throw new NotSupportedException($"No executor available for test type {scenario.Type}");

            // Execute the test
            var result = await executor.ExecuteTest(scenario, cancellationToken);
            
            // Save the result
            await _repository.SaveResult(result, cancellationToken);

            // Update scenario status
            scenario.Status = result.Passed ? TestStatus.Completed : TestStatus.Failed;
            await _repository.UpdateScenario(scenario, cancellationToken);

            _logger.LogInformation("Test scenario {ScenarioId} execution completed. Passed: {Passed}", 
                scenarioId, result.Passed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute test scenario {ScenarioId}", scenarioId);
            throw;
        }
    }

    /// <summary>
    /// Executes multiple test scenarios in parallel
    /// </summary>
    public async Task<List<TestResult>> ExecuteTestsParallel(
        List<string> scenarioIds, 
        int maxConcurrency = 3,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = scenarioIds.Select(async scenarioId =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteTest(scenarioId, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Gets test execution history for a scenario
    /// </summary>
    public async Task<List<TestResult>> GetTestHistory(string scenarioId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetResults(scenarioId, cancellationToken);
    }

    /// <summary>
    /// Gets all test scenarios for a project
    /// </summary>
    public async Task<List<TestScenario>> GetProjectTests(string projectId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetScenariosByProject(projectId, cancellationToken);
    }

    /// <summary>
    /// Searches for test scenarios based on criteria
    /// </summary>
    public async Task<List<TestScenario>> SearchTests(TestSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        return await _repository.SearchScenarios(criteria, cancellationToken);
    }

    /// <summary>
    /// Gets test execution statistics
    /// </summary>
    public async Task<TestStatistics> GetTestStatistics(
        string projectId, 
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetTestStatistics(projectId, fromDate, toDate, cancellationToken);
    }

    /// <summary>
    /// Analyzes test failure using LLM
    /// </summary>
    public async Task<string> AnalyzeFailure(string scenarioId, CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetResults(scenarioId, cancellationToken);
        var latestResult = results.OrderByDescending(r => r.StartedAt).FirstOrDefault();

        if (latestResult == null || latestResult.Passed)
            return "No failures to analyze";

        return await _llmService.AnalyzeTestFailure(latestResult, cancellationToken);
    }

    /// <summary>
    /// Refines test steps based on feedback
    /// </summary>
    public async Task<TestScenario> RefineTestScenario(
        string scenarioId, 
        string feedback, 
        CancellationToken cancellationToken = default)
    {
        var scenario = await _repository.GetScenario(scenarioId, cancellationToken);
        if (scenario == null)
            throw new ArgumentException($"Scenario {scenarioId} not found");

        var refinedSteps = await _llmService.RefineTestSteps(scenario.Steps, feedback, cancellationToken);
        
        scenario.Steps = refinedSteps;
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.Status = TestStatus.Ready;

        await _repository.UpdateScenario(scenario, cancellationToken);
        return scenario;
    }

    /// <summary>
    /// Generates test data for a scenario
    /// </summary>
    public async Task<Dictionary<string, object>> GenerateTestData(
        string scenarioId, 
        string dataRequirements, 
        CancellationToken cancellationToken = default)
    {
        var scenario = await _repository.GetScenario(scenarioId, cancellationToken);
        if (scenario == null)
            throw new ArgumentException($"Scenario {scenarioId} not found");

        return await _llmService.GenerateTestData(scenario, dataRequirements, cancellationToken);
    }

    /// <summary>
    /// Validates a test scenario
    /// </summary>
    public async Task<TestValidationResult> ValidateTestScenario(
        string scenarioId, 
        CancellationToken cancellationToken = default)
    {
        var scenario = await _repository.GetScenario(scenarioId, cancellationToken);
        if (scenario == null)
            throw new ArgumentException($"Scenario {scenarioId} not found");

        return await _llmService.ValidateTestScenario(scenario, cancellationToken);
    }

    /// <summary>
    /// Suggests additional test scenarios based on existing ones
    /// </summary>
    public async Task<List<TestScenario>> SuggestAdditionalTests(
        string projectId, 
        string projectContext, 
        CancellationToken cancellationToken = default)
    {
        var existingScenarios = await _repository.GetScenariosByProject(projectId, cancellationToken);
        var suggestedScenarios = await _llmService.SuggestAdditionalTests(existingScenarios, projectContext, cancellationToken);

        // Set project ID for suggested scenarios
        foreach (var scenario in suggestedScenarios)
        {
            scenario.ProjectId = projectId;
            scenario.Status = TestStatus.Draft;
        }

        return suggestedScenarios;
    }

    /// <summary>
    /// Optimizes test scenarios for better performance
    /// </summary>
    public async Task<List<TestScenario>> OptimizeTestScenarios(
        string projectId, 
        CancellationToken cancellationToken = default)
    {
        var scenarios = await _repository.GetScenariosByProject(projectId, cancellationToken);
        var optimizedScenarios = await _llmService.OptimizeTestScenarios(scenarios, cancellationToken);

        // Update scenarios with optimizations
        foreach (var optimizedScenario in optimizedScenarios)
        {
            optimizedScenario.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateScenario(optimizedScenario, cancellationToken);
        }

        return optimizedScenarios;
    }

    /// <summary>
    /// Gets health status of all executors
    /// </summary>
    public async Task<Dictionary<string, HealthCheckResult>> GetExecutorHealthStatus(CancellationToken cancellationToken = default)
    {
        var healthChecks = new Dictionary<string, HealthCheckResult>();

        foreach (var executor in _executors)
        {
            try
            {
                var health = await executor.PerformHealthCheck(cancellationToken);
                healthChecks[executor.Name] = health;
            }
            catch (Exception ex)
            {
                healthChecks[executor.Name] = new HealthCheckResult
                {
                    IsHealthy = false,
                    Message = $"Health check failed: {ex.Message}"
                };
            }
        }

        return healthChecks;
    }

    /// <summary>
    /// Deletes a test scenario and its results
    /// </summary>
    public async Task<bool> DeleteTestScenario(string scenarioId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting test scenario {ScenarioId}", scenarioId);
        return await _repository.DeleteScenario(scenarioId, cancellationToken);
    }

    /// <summary>
    /// Clones a test scenario
    /// </summary>
    public async Task<string> CloneTestScenario(string scenarioId, string newTitle = "", CancellationToken cancellationToken = default)
    {
        var originalScenario = await _repository.GetScenario(scenarioId, cancellationToken);
        if (originalScenario == null)
            throw new ArgumentException($"Scenario {scenarioId} not found");

        var clonedScenario = originalScenario.Clone();
        clonedScenario.Title = !string.IsNullOrEmpty(newTitle) ? newTitle : $"{originalScenario.Title} (Copy)";
        clonedScenario.Status = TestStatus.Draft;

        var clonedId = await _repository.SaveScenario(clonedScenario, cancellationToken);
        _logger.LogInformation("Cloned test scenario {OriginalId} to {ClonedId}", scenarioId, clonedId);

        return clonedId;
    }
}