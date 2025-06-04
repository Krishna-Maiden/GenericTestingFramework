using GenericTestingFramework.Services.LLM;

namespace GenericTestingFramework.Services.LLM;

/// <summary>
/// Enhanced configuration for OpenAI LLM service
/// </summary>
public class LLMConfiguration
{
    public const string SectionName = "LLM";

    /// <summary>
    /// OpenAI API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI Model to use (e.g., gpt-3.5-turbo, gpt-4)
    /// </summary>
    public string Model { get; set; } = "gpt-3.5-turbo";

    /// <summary>
    /// Maximum tokens for the response
    /// </summary>
    public int MaxTokens { get; set; } = 3000;

    /// <summary>
    /// Temperature for response creativity (0.0 to 2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Top-p for nucleus sampling
    /// </summary>
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Timeout for API calls in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum number of retries for failed requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// System prompt for the AI assistant
    /// </summary>
    public string SystemPrompt { get; set; } = "You are an expert test automation engineer specializing in converting user stories into comprehensive, executable test scenarios.";

    /// <summary>
    /// Enable response caching
    /// </summary>
    public bool EnableCaching { get; set; } = false;

    /// <summary>
    /// Cache duration in minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Enable detailed logging
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitConfiguration RateLimit { get; set; } = new();

    /// <summary>
    /// Validate the configuration
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            return false;
        }

        if (MaxTokens <= 0 || MaxTokens > 32000)
        {
            return false;
        }

        if (Temperature < 0.0 || Temperature > 2.0)
        {
            return false;
        }

        if (TimeoutSeconds <= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Get validation errors
    /// </summary>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add("ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            errors.Add("Model is required");
        }

        if (MaxTokens <= 0 || MaxTokens > 32000)
        {
            errors.Add("MaxTokens must be between 1 and 32000");
        }

        if (Temperature < 0.0 || Temperature > 2.0)
        {
            errors.Add("Temperature must be between 0.0 and 2.0");
        }

        if (TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds must be greater than 0");
        }

        return errors;
    }
}

/// <summary>
/// Rate limiting configuration for OpenAI API
/// </summary>
public class RateLimitConfiguration
{
    /// <summary>
    /// Maximum requests per minute
    /// </summary>
    public int RequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Maximum tokens per minute
    /// </summary>
    public int TokensPerMinute { get; set; } = 120000;

    /// <summary>
    /// Enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;
}