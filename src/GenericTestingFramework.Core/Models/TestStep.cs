using System.ComponentModel.DataAnnotations;

namespace GenericTestingFramework.Core.Models;

/// <summary>
/// Represents a single step in a test scenario
/// </summary>
public class TestStep
{
    /// <summary>
    /// Unique identifier for the test step
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Order of execution within the scenario
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Action to be performed (click, navigate, api_call, verify, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Target element, URL, or endpoint for the action
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Expected result or outcome of this step
    /// </summary>
    [StringLength(1000)]
    public string ExpectedResult { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what this step does
    /// </summary>
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional parameters for the action
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Conditions that must be met before this step executes
    /// </summary>
    public List<string> Prerequisites { get; set; } = new();

    /// <summary>
    /// Maximum time to wait for this step to complete
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Whether to continue test execution if this step fails
    /// </summary>
    public bool ContinueOnFailure { get; set; } = false;

    /// <summary>
    /// Whether to take a screenshot after this step (for UI tests)
    /// </summary>
    public bool TakeScreenshot { get; set; } = false;

    /// <summary>
    /// Wait time before executing this step
    /// </summary>
    public TimeSpan? WaitBefore { get; set; }

    /// <summary>
    /// Wait time after executing this step
    /// </summary>
    public TimeSpan? WaitAfter { get; set; }

    /// <summary>
    /// Data to be used in this step (input values, etc.)
    /// </summary>
    public Dictionary<string, object> StepData { get; set; } = new();

    /// <summary>
    /// Validation rules for this step
    /// </summary>
    public List<ValidationRule> ValidationRules { get; set; } = new();

    /// <summary>
    /// Whether this step is enabled for execution
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Validates the test step for completeness and correctness
    /// </summary>
    /// <returns>List of validation errors, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Action))
            errors.Add("Action is required");

        if (string.IsNullOrWhiteSpace(Target))
            errors.Add("Target is required");

        if (Timeout.HasValue && Timeout.Value <= TimeSpan.Zero)
            errors.Add("Timeout must be positive");

        if (WaitBefore.HasValue && WaitBefore.Value < TimeSpan.Zero)
            errors.Add("WaitBefore cannot be negative");

        if (WaitAfter.HasValue && WaitAfter.Value < TimeSpan.Zero)
            errors.Add("WaitAfter cannot be negative");

        // Validate action-specific requirements
        switch (Action.ToLowerInvariant())
        {
            case "enter_text":
            case "type":
                if (!Parameters.ContainsKey("value") && !StepData.ContainsKey("value"))
                    errors.Add("Text input actions require a 'value' parameter");
                break;

            case "api_post":
            case "api_put":
            case "api_patch":
                if (!Parameters.ContainsKey("body") && !StepData.ContainsKey("body"))
                    errors.Add("HTTP POST/PUT/PATCH actions require a 'body' parameter");
                break;

            case "wait":
                if (!Parameters.ContainsKey("duration") && !StepData.ContainsKey("duration"))
                    errors.Add("Wait actions require a 'duration' parameter");
                break;

            case "verify":
            case "assert":
                if (!Parameters.ContainsKey("expected") && !StepData.ContainsKey("expected"))
                    errors.Add("Verification actions require an 'expected' parameter");
                break;
        }

        return errors;
    }

    /// <summary>
    /// Creates a deep copy of the test step
    /// </summary>
    /// <returns>New TestStep instance with copied values</returns>
    public TestStep Clone()
    {
        return new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = Order,
            Action = Action,
            Target = Target,
            ExpectedResult = ExpectedResult,
            Description = Description,
            Parameters = new Dictionary<string, object>(Parameters),
            Prerequisites = new List<string>(Prerequisites),
            Timeout = Timeout,
            ContinueOnFailure = ContinueOnFailure,
            TakeScreenshot = TakeScreenshot,
            WaitBefore = WaitBefore,
            WaitAfter = WaitAfter,
            StepData = new Dictionary<string, object>(StepData),
            ValidationRules = ValidationRules.Select(vr => vr.Clone()).ToList(),
            IsEnabled = IsEnabled,
            Tags = new List<string>(Tags)
        };
    }

    /// <summary>
    /// Gets the effective value for a parameter from either Parameters or StepData
    /// </summary>
    /// <param name="key">Parameter key</param>
    /// <returns>Parameter value or null if not found</returns>
    public object? GetParameterValue(string key)
    {
        return Parameters.TryGetValue(key, out var paramValue) ? paramValue :
               StepData.TryGetValue(key, out var stepValue) ? stepValue : null;
    }

    /// <summary>
    /// Sets a parameter value, preferring Parameters over StepData
    /// </summary>
    /// <param name="key">Parameter key</param>
    /// <param name="value">Parameter value</param>
    public void SetParameterValue(string key, object value)
    {
        Parameters[key] = value;
    }
}

/// <summary>
/// Represents a validation rule for a test step
/// </summary>
public class ValidationRule
{
    /// <summary>
    /// Type of validation (equals, contains, regex, etc.)
    /// </summary>
    public string ValidationType { get; set; } = string.Empty;

    /// <summary>
    /// Expected value or pattern
    /// </summary>
    public object ExpectedValue { get; set; } = string.Empty;

    /// <summary>
    /// Property or element to validate
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Error message if validation fails
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether this validation is mandatory for step success
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Creates a deep copy of the validation rule
    /// </summary>
    /// <returns>New ValidationRule instance with copied values</returns>
    public ValidationRule Clone()
    {
        return new ValidationRule
        {
            ValidationType = ValidationType,
            ExpectedValue = ExpectedValue,
            Target = Target,
            ErrorMessage = ErrorMessage,
            IsRequired = IsRequired
        };
    }
}