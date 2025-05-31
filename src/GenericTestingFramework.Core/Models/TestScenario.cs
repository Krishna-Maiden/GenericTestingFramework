using System.ComponentModel.DataAnnotations;

namespace GenericTestingFramework.Core.Models;

/// <summary>
/// Represents a complete test scenario with all its steps and metadata
/// </summary>
public class TestScenario
{
    /// <summary>
    /// Unique identifier for the test scenario
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable title for the test scenario
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what the test scenario validates
    /// </summary>
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Original user story or requirement that generated this test
    /// </summary>
    [StringLength(5000)]
    public string OriginalUserStory { get; set; } = string.Empty;

    /// <summary>
    /// Type of testing (UI, API, Mixed, etc.)
    /// </summary>
    public TestType Type { get; set; }

    /// <summary>
    /// Current status of the test scenario
    /// </summary>
    public TestStatus Status { get; set; } = TestStatus.Draft;

    /// <summary>
    /// Priority level for execution
    /// </summary>
    public TestPriority Priority { get; set; } = TestPriority.Medium;

    /// <summary>
    /// Target environment for execution
    /// </summary>
    public TestEnvironment Environment { get; set; } = TestEnvironment.Development;

    /// <summary>
    /// Project identifier this test belongs to
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Collection of test steps to execute
    /// </summary>
    public List<TestStep> Steps { get; set; } = new();

    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Preconditions that must be met before test execution
    /// </summary>
    public List<string> Preconditions { get; set; } = new();

    /// <summary>
    /// Expected outcomes after test execution
    /// </summary>
    public List<string> ExpectedOutcomes { get; set; } = new();

    /// <summary>
    /// When the test scenario was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the test scenario was last modified
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created the test scenario
    /// </summary>
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Maximum time allowed for test execution
    /// </summary>
    public TimeSpan? TimeoutDuration { get; set; }

    /// <summary>
    /// Number of retry attempts if test fails
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Whether this test should run in parallel with others
    /// </summary>
    public bool CanRunInParallel { get; set; } = true;

    /// <summary>
    /// Additional metadata as key-value pairs
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Configuration specific to the test type
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Test data parameters
    /// </summary>
    public Dictionary<string, object> TestData { get; set; } = new();

    /// <summary>
    /// Validates the test scenario for completeness
    /// </summary>
    /// <returns>List of validation errors, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Title is required");

        if (string.IsNullOrWhiteSpace(ProjectId))
            errors.Add("ProjectId is required");

        if (!Steps.Any())
            errors.Add("At least one test step is required");

        foreach (var step in Steps)
        {
            var stepErrors = step.Validate();
            errors.AddRange(stepErrors.Select(e => $"Step '{step.Action}': {e}"));
        }

        if (TimeoutDuration.HasValue && TimeoutDuration.Value <= TimeSpan.Zero)
            errors.Add("TimeoutDuration must be positive");

        if (RetryCount < 0)
            errors.Add("RetryCount cannot be negative");

        return errors;
    }

    /// <summary>
    /// Creates a deep copy of the test scenario
    /// </summary>
    /// <returns>New TestScenario instance with copied values</returns>
    public TestScenario Clone()
    {
        return new TestScenario
        {
            Id = Guid.NewGuid().ToString(),
            Title = Title,
            Description = Description,
            OriginalUserStory = OriginalUserStory,
            Type = Type,
            Status = TestStatus.Draft,
            Priority = Priority,
            Environment = Environment,
            ProjectId = ProjectId,
            Steps = Steps.Select(s => s.Clone()).ToList(),
            Tags = new List<string>(Tags),
            Preconditions = new List<string>(Preconditions),
            ExpectedOutcomes = new List<string>(ExpectedOutcomes),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = CreatedBy,
            TimeoutDuration = TimeoutDuration,
            RetryCount = RetryCount,
            CanRunInParallel = CanRunInParallel,
            Metadata = new Dictionary<string, object>(Metadata),
            Configuration = new Dictionary<string, object>(Configuration),
            TestData = new Dictionary<string, object>(TestData)
        };
    }
}