namespace GenericTestingFramework.Services.LLM;

/// <summary>
/// Configuration options for LLM services
/// </summary>
public class LLMConfiguration
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "LLM";

    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use (gpt-4, gpt-3.5-turbo, etc.)
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// Maximum tokens for response
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Temperature for response creativity (0.0 - 2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Top-p sampling parameter
    /// </summary>
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// System prompt for test generation
    /// </summary>
    public string SystemPrompt { get; set; } = @"
You are an expert test automation engineer specialized in creating comprehensive, reliable automated tests. 
Your role is to analyze user stories, requirements, and feedback to generate high-quality test scenarios 
that thoroughly validate software functionality.

Key principles:
- Create realistic, executable test steps
- Focus on both positive and negative test cases
- Include proper assertions and validations
- Consider edge cases and error scenarios
- Optimize for maintainability and reliability
- Use industry best practices for test design

Always respond with valid JSON in the exact format requested.
";

    /// <summary>
    /// Whether to enable caching of LLM responses
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to log LLM requests and responses
    /// </summary>
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// Custom endpoint URL (for using other OpenAI-compatible APIs)
    /// </summary>
    public string? CustomEndpoint { get; set; }

    /// <summary>
    /// Additional headers for API requests
    /// </summary>
    public Dictionary<string, string> AdditionalHeaders { get; set; } = new();

    /// <summary>
    /// Rate limiting settings
    /// </summary>
    public RateLimitSettings RateLimit { get; set; } = new();

    /// <summary>
    /// Validates the configuration
    /// </summary>
    /// <returns>List of validation errors</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiKey))
            errors.Add("ApiKey is required");

        if (string.IsNullOrWhiteSpace(Model))
            errors.Add("Model is required");

        if (MaxTokens <= 0)
            errors.Add("MaxTokens must be positive");

        if (Temperature < 0.0 || Temperature > 2.0)
            errors.Add("Temperature must be between 0.0 and 2.0");

        if (TopP <= 0.0 || TopP > 1.0)
            errors.Add("TopP must be between 0.0 and 1.0");

        if (TimeoutSeconds <= 0)
            errors.Add("TimeoutSeconds must be positive");

        if (MaxRetries < 0)
            errors.Add("MaxRetries cannot be negative");

        if (RetryDelayMs < 0)
            errors.Add("RetryDelayMs cannot be negative");

        if (CacheDurationMinutes <= 0)
            errors.Add("CacheDurationMinutes must be positive");

        return errors;
    }
}

/// <summary>
/// Rate limiting configuration
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Maximum requests per minute
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Maximum tokens per minute
    /// </summary>
    public int TokensPerMinute { get; set; } = 90000;

    /// <summary>
    /// Whether to enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;
}