using GenericTestingFramework.Core.Models;

namespace GenericTestingFramework.Core.Interfaces;

/// <summary>
/// Interface for test data repository operations
/// </summary>
public interface ITestRepository
{
    /// <summary>
    /// Saves a test scenario
    /// </summary>
    /// <param name="scenario">Test scenario to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scenario ID</returns>
    Task<string> SaveScenario(TestScenario scenario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a test scenario by ID
    /// </summary>
    /// <param name="id">Scenario ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test scenario or null if not found</returns>
    Task<TestScenario?> GetScenario(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves test scenarios for a project
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of test scenarios</returns>
    Task<List<TestScenario>> GetScenariosByProject(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for test scenarios based on criteria
    /// </summary>
    /// <param name="searchCriteria">Search criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching test scenarios</returns>
    Task<List<TestScenario>> SearchScenarios(TestSearchCriteria searchCriteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing test scenario
    /// </summary>
    /// <param name="scenario">Updated test scenario</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update successful</returns>
    Task<bool> UpdateScenario(TestScenario scenario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a test scenario
    /// </summary>
    /// <param name="id">Scenario ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deletion successful</returns>
    Task<bool> DeleteScenario(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a test result
    /// </summary>
    /// <param name="result">Test result to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result ID</returns>
    Task<string> SaveResult(TestResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves test results for a scenario
    /// </summary>
    /// <param name="scenarioId">Scenario ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of test results</returns>
    Task<List<TestResult>> GetResults(string scenarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves test results based on criteria
    /// </summary>
    /// <param name="searchCriteria">Search criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching test results</returns>
    Task<List<TestResult>> SearchResults(ResultSearchCriteria searchCriteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets test execution statistics
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test statistics</returns>
    Task<TestStatistics> GetTestStatistics(string projectId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives old test results
    /// </summary>
    /// <param name="olderThan">Archive results older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of archived results</returns>
    Task<int> ArchiveOldResults(DateTime olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search criteria for test scenarios
/// </summary>
public class TestSearchCriteria
{
    /// <summary>
    /// Project ID filter
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Test type filter
    /// </summary>
    public TestType? Type { get; set; }

    /// <summary>
    /// Status filter
    /// </summary>
    public TestStatus? Status { get; set; }

    /// <summary>
    /// Priority filter
    /// </summary>
    public TestPriority? Priority { get; set; }

    /// <summary>
    /// Tags filter
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Created by filter
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Created date range start
    /// </summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>
    /// Created date range end
    /// </summary>
    public DateTime? CreatedTo { get; set; }

    /// <summary>
    /// Text search in title and description
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Page number for pagination
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size for pagination
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Sort direction
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Search criteria for test results
/// </summary>
public class ResultSearchCriteria
{
    /// <summary>
    /// Scenario ID filter
    /// </summary>
    public string? ScenarioId { get; set; }

    /// <summary>
    /// Project ID filter
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Passed/failed filter
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Environment filter
    /// </summary>
    public TestEnvironment? Environment { get; set; }

    /// <summary>
    /// Executed by filter
    /// </summary>
    public string? ExecutedBy { get; set; }

    /// <summary>
    /// Execution date range start
    /// </summary>
    public DateTime? ExecutedFrom { get; set; }

    /// <summary>
    /// Execution date range end
    /// </summary>
    public DateTime? ExecutedTo { get; set; }

    /// <summary>
    /// Minimum duration filter
    /// </summary>
    public TimeSpan? MinDuration { get; set; }

    /// <summary>
    /// Maximum duration filter
    /// </summary>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>
    /// Execution tags filter
    /// </summary>
    public List<string> ExecutionTags { get; set; } = new();

    /// <summary>
    /// Page number for pagination
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size for pagination
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; set; } = "StartedAt";

    /// <summary>
    /// Sort direction
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Test execution statistics
/// </summary>
public class TestStatistics
{
    /// <summary>
    /// Total number of test scenarios
    /// </summary>
    public int TotalScenarios { get; set; }

    /// <summary>
    /// Total number of test executions
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Number of passed executions
    /// </summary>
    public int PassedExecutions { get; set; }

    /// <summary>
    /// Number of failed executions
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Overall pass rate percentage
    /// </summary>
    public double PassRate { get; set; }

    /// <summary>
    /// Average execution duration
    /// </summary>
    public TimeSpan AverageDuration { get; set; }

    /// <summary>
    /// Statistics by test type
    /// </summary>
    public Dictionary<TestType, TypeStatistics> StatsByType { get; set; } = new();

    /// <summary>
    /// Statistics by environment
    /// </summary>
    public Dictionary<TestEnvironment, EnvironmentStatistics> StatsByEnvironment { get; set; } = new();

    /// <summary>
    /// Daily execution trends
    /// </summary>
    public List<DailyStatistics> DailyTrends { get; set; } = new();
}

/// <summary>
/// Statistics for a specific test type
/// </summary>
public class TypeStatistics
{
    /// <summary>
    /// Number of scenarios of this type
    /// </summary>
    public int ScenarioCount { get; set; }

    /// <summary>
    /// Number of executions of this type
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Pass rate for this type
    /// </summary>
    public double PassRate { get; set; }

    /// <summary>
    /// Average duration for this type
    /// </summary>
    public TimeSpan AverageDuration { get; set; }
}

/// <summary>
/// Statistics for a specific environment
/// </summary>
public class EnvironmentStatistics
{
    /// <summary>
    /// Number of executions in this environment
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Pass rate in this environment
    /// </summary>
    public double PassRate { get; set; }

    /// <summary>
    /// Average duration in this environment
    /// </summary>
    public TimeSpan AverageDuration { get; set; }
}

/// <summary>
/// Daily statistics
/// </summary>
public class DailyStatistics
{
    /// <summary>
    /// Date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of executions on this date
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Pass rate for this date
    /// </summary>
    public double PassRate { get; set; }

    /// <summary>
    /// Average duration for this date
    /// </summary>
    public TimeSpan AverageDuration { get; set; }
}