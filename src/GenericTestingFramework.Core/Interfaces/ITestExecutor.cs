using GenericTestingFramework.Core.Models;

namespace GenericTestingFramework.Core.Interfaces;

/// <summary>
/// Interface for test executors that can run specific types of tests
/// </summary>
public interface ITestExecutor
{
    /// <summary>
    /// Name of the executor
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines if this executor can handle the specified test type
    /// </summary>
    /// <param name="testType">Type of test to execute</param>
    /// <returns>True if this executor can handle the test type</returns>
    bool CanExecute(TestType testType);

    /// <summary>
    /// Executes a test scenario
    /// </summary>
    /// <param name="scenario">Test scenario to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test execution result</returns>
    Task<TestResult> ExecuteTest(TestScenario scenario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the test scenario can be executed by this executor
    /// </summary>
    /// <param name="scenario">Test scenario to validate</param>
    /// <returns>Validation result</returns>
    Task<ExecutorValidationResult> ValidateScenario(TestScenario scenario);

    /// <summary>
    /// Gets the capabilities and limitations of this executor
    /// </summary>
    /// <returns>Executor capabilities</returns>
    ExecutorCapabilities GetCapabilities();

    /// <summary>
    /// Performs health check on the executor
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status</returns>
    Task<HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the executor with configuration
    /// </summary>
    /// <param name="configuration">Executor configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Initialization result</returns>
    Task<bool> Initialize(Dictionary<string, object> configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up resources used by the executor
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the cleanup operation</returns>
    Task Cleanup(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of executor validation
/// </summary>
public class ExecutorValidationResult
{
    /// <summary>
    /// Whether the scenario can be executed
    /// </summary>
    public bool CanExecute { get; set; }

    /// <summary>
    /// Validation messages
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Required configuration for execution
    /// </summary>
    public Dictionary<string, object> RequiredConfiguration { get; set; } = new();
}

/// <summary>
/// Capabilities of a test executor
/// </summary>
public class ExecutorCapabilities
{
    /// <summary>
    /// Supported test types
    /// </summary>
    public List<TestType> SupportedTestTypes { get; set; } = new();

    /// <summary>
    /// Supported actions
    /// </summary>
    public List<string> SupportedActions { get; set; } = new();

    /// <summary>
    /// Maximum parallel executions
    /// </summary>
    public int MaxParallelExecutions { get; set; } = 1;

    /// <summary>
    /// Whether screenshots are supported
    /// </summary>
    public bool SupportsScreenshots { get; set; }

    /// <summary>
    /// Whether video recording is supported
    /// </summary>
    public bool SupportsVideoRecording { get; set; }

    /// <summary>
    /// Supported browsers (for UI executors)
    /// </summary>
    public List<string> SupportedBrowsers { get; set; } = new();

    /// <summary>
    /// Additional capabilities
    /// </summary>
    public Dictionary<string, object> AdditionalCapabilities { get; set; } = new();
}

/// <summary>
/// Health check result for an executor
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Whether the executor is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Health status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Response time for health check
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Additional health metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}