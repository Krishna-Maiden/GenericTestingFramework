using System.ComponentModel.DataAnnotations;

namespace GenericTestingFramework.Core.Models;

/// <summary>
/// Represents the result of a test scenario execution
/// </summary>
public class TestResult
{
    /// <summary>
    /// Unique identifier for this test result
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the test scenario that was executed
    /// </summary>
    [Required]
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the overall test passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Overall test execution message
    /// </summary>
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the test execution started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the test execution completed
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total duration of test execution
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Environment where the test was executed
    /// </summary>
    public TestEnvironment Environment { get; set; }

    /// <summary>
    /// Machine or agent that executed the test
    /// </summary>
    [StringLength(100)]
    public string ExecutedBy { get; set; } = Environment.MachineName;

    /// <summary>
    /// Results of individual test steps
    /// </summary>
    public List<StepResult> StepResults { get; set; } = new();

    /// <summary>
    /// Paths to screenshots taken during execution
    /// </summary>
    public List<string> Screenshots { get; set; } = new();

    /// <summary>
    /// Complete log output from test execution
    /// </summary>
    public string LogOutput { get; set; } = string.Empty;

    /// <summary>
    /// Error details if test failed
    /// </summary>
    public TestError? Error { get; set; }

    /// <summary>
    /// Performance metrics collected during execution
    /// </summary>
    public TestPerformanceMetrics? PerformanceMetrics { get; set; }

    /// <summary>
    /// Additional metadata collected during execution
    /// </summary>
    public Dictionary<string, object> ExecutionMetadata { get; set; } = new();

    /// <summary>
    /// Version of the test framework used
    /// </summary>
    [StringLength(50)]
    public string FrameworkVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Browser information for UI tests
    /// </summary>
    public BrowserInfo? BrowserInfo { get; set; }

    /// <summary>
    /// Number of retry attempts made
    /// </summary>
    public int RetryAttempts { get; set; } = 0;

    /// <summary>
    /// Tags associated with this execution
    /// </summary>
    public List<string> ExecutionTags { get; set; } = new();

    /// <summary>
    /// Calculates success rate based on step results
    /// </summary>
    /// <returns>Percentage of successful steps</returns>
    public double GetSuccessRate()
    {
        if (!StepResults.Any()) return 0.0;
        return (double)StepResults.Count(sr => sr.Passed) / StepResults.Count * 100;
    }

    /// <summary>
    /// Gets total number of assertions made during test
    /// </summary>
    /// <returns>Total assertion count</returns>
    public int GetTotalAssertions()
    {
        return StepResults.Sum(sr => sr.AssertionCount);
    }

    /// <summary>
    /// Gets the first failed step result
    /// </summary>
    /// <returns>First failed step or null if all passed</returns>
    public StepResult? GetFirstFailure()
    {
        return StepResults.FirstOrDefault(sr => !sr.Passed);
    }

    /// <summary>
    /// Adds a step result to the collection
    /// </summary>
    /// <param name="stepResult">Step result to add</param>
    public void AddStepResult(StepResult stepResult)
    {
        StepResults.Add(stepResult);
        
        // Update overall test status
        if (!stepResult.Passed && stepResult.IsRequired)
        {
            Passed = false;
            if (string.IsNullOrEmpty(Message))
                Message = $"Test failed at step: {stepResult.StepName}";
        }
    }

    /// <summary>
    /// Marks the test as completed and calculates final status
    /// </summary>
    public void Complete()
    {
        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - StartedAt;
        
        // If no explicit failure was set, check if all required steps passed
        if (Passed && StepResults.Any(sr => !sr.Passed && sr.IsRequired))
        {
            Passed = false;
            Message = "One or more required steps failed";
        }
        
        // If all steps passed and no explicit failure, mark as passed
        if (!StepResults.Any() || StepResults.Where(sr => sr.IsRequired).All(sr => sr.Passed))
        {
            if (string.IsNullOrEmpty(Message))
            {
                Passed = true;
                Message = "All test steps completed successfully";
            }
        }
    }
}

/// <summary>
/// Represents the result of a single test step execution
/// </summary>
public class StepResult
{
    /// <summary>
    /// Unique identifier for this step result
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the test step that was executed
    /// </summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>
    /// Name or description of the step
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Action that was performed
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Target that the action was performed on
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Whether the step passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Result message or error description
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Expected result for this step
    /// </summary>
    public string ExpectedResult { get; set; } = string.Empty;

    /// <summary>
    /// Actual result obtained
    /// </summary>
    public string ActualResult { get; set; } = string.Empty;

    /// <summary>
    /// When the step started executing
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the step completed
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of step execution
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Screenshot path if captured
    /// </summary>
    public string? ScreenshotPath { get; set; }

    /// <summary>
    /// Number of assertions made in this step
    /// </summary>
    public int AssertionCount { get; set; } = 0;

    /// <summary>
    /// Whether this step is required for overall test success
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Exception details if step failed
    /// </summary>
    public StepException? Exception { get; set; }

    /// <summary>
    /// Additional data collected during step execution
    /// </summary>
    public Dictionary<string, object> StepData { get; set; } = new();

    /// <summary>
    /// Validation results for this step
    /// </summary>
    public List<ValidationResult> ValidationResults { get; set; } = new();

    /// <summary>
    /// Marks the step as completed
    /// </summary>
    /// <param name="passed">Whether the step passed</param>
    /// <param name="message">Result message</param>
    public void Complete(bool passed, string message = "")
    {
        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - StartedAt;
        Passed = passed;
        if (!string.IsNullOrEmpty(message))
            Message = message;
    }
}

/// <summary>
/// Contains error information for failed tests
/// </summary>
public class TestError
{
    /// <summary>
    /// Type of error that occurred
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace if available
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Inner exception details
    /// </summary>
    public TestError? InnerError { get; set; }

    /// <summary>
    /// Step where the error occurred
    /// </summary>
    public string? FailedStep { get; set; }

    /// <summary>
    /// Additional error context
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Performance metrics collected during test execution
/// </summary>
public class TestPerformanceMetrics
{
    /// <summary>
    /// Total memory used during test execution
    /// </summary>
    public long MemoryUsedBytes { get; set; }

    /// <summary>
    /// Peak memory usage
    /// </summary>
    public long PeakMemoryBytes { get; set; }

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Network requests made during test
    /// </summary>
    public int NetworkRequestCount { get; set; }

    /// <summary>
    /// Total bytes transferred
    /// </summary>
    public long NetworkBytesTransferred { get; set; }

    /// <summary>
    /// Average response time for network requests
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Custom performance counters
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Browser information for UI tests
/// </summary>
public class BrowserInfo
{
    /// <summary>
    /// Browser name (Chrome, Firefox, Safari, etc.)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Browser version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Operating system
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Screen resolution used for testing
    /// </summary>
    public string Resolution { get; set; } = string.Empty;

    /// <summary>
    /// User agent string
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Whether the test ran in headless mode
    /// </summary>
    public bool IsHeadless { get; set; }
}

/// <summary>
/// Exception information for failed steps
/// </summary>
public class StepException
{
    /// <summary>
    /// Exception type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Exception message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Additional exception data
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Result of a validation operation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Name of the validation
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Validation message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Expected value
    /// </summary>
    public object? Expected { get; set; }

    /// <summary>
    /// Actual value
    /// </summary>
    public object? Actual { get; set; }
}