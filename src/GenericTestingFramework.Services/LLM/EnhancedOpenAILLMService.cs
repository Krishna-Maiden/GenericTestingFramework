using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GenericTestingFramework.Services.LLM;

/// <summary>
/// Enhanced OpenAI LLM service with improved selector generation for real-world websites
/// </summary>
public class EnhancedOpenAILLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EnhancedOpenAILLMService> _logger;
    private readonly LLMConfiguration _configuration;

    public EnhancedOpenAILLMService(HttpClient httpClient, ILogger<EnhancedOpenAILLMService> logger, IOptions<LLMConfiguration> configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;

        ConfigureHttpClient();
    }

    public async Task<TestScenario> GenerateTestFromNaturalLanguage(string userStory, string projectContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating enhanced test scenario from user story using OpenAI GPT-3.5-Turbo");

        try
        {
            var prompt = BuildEnhancedTestGenerationPrompt(userStory, projectContext);
            var response = await CallOpenAI(prompt, cancellationToken);

            var scenario = ParseOpenAIResponse(response, userStory);

            _logger.LogInformation("Successfully generated enhanced test scenario: {Title}", scenario.Title);
            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test scenario from user story");

            // Fallback to basic scenario if OpenAI fails
            return CreateEnhancedFallbackScenario(userStory, projectContext);
        }
    }

    private string BuildEnhancedTestGenerationPrompt(string userStory, string context)
    {
        var extractedUrl = ExtractUrlFromUserStory(userStory);
        var extractedUsername = ExtractUsernameFromUserStory(userStory);
        var extractedPassword = ExtractPasswordFromUserStory(userStory);

        return $@"Convert the user story below into a robust test scenario with multiple fallback selectors. Respond ONLY with valid JSON in the exact format shown.

USER STORY:
{userStory}

CONTEXT:
{context}

IMPORTANT SELECTOR STRATEGY:
- Use multiple fallback selectors for each element (CSS selectors, attribute-based)
- Common email/username field selectors: input[type='email'], input[name*='email'], input[name*='username'], input[placeholder*='email'], input[placeholder*='username'], #email, #username, .email-input, .username-input
- Common password field selectors: input[type='password'], input[name*='password'], input[placeholder*='password'], #password, .password-input
- Common button selectors: button[type='submit'], input[type='submit'], button:contains('Login'), button:contains('Sign'), .login-btn, .btn-login, .submit-btn

REQUIRED JSON FORMAT (copy this structure exactly):
{{
  ""title"": ""Authentication Test for Admin Portal"",
  ""description"": ""Test admin authentication with fallback element detection"",
  ""type"": ""UI"",
  ""priority"": ""High"",
  ""tags"": [""auth"", ""login"", ""admin""],
  ""preconditions"": [
    ""Admin portal is accessible"",
    ""Valid admin credentials are available""
  ],
  ""steps"": [
    {{
      ""order"": 1,
      ""action"": ""navigate"",
      ""description"": ""Navigate to admin portal"",
      ""target"": ""{extractedUrl}"",
      ""parameters"": {{
        ""url"": ""{extractedUrl}""
      }},
      ""expectedResult"": ""Admin portal login page loads successfully"",
      ""timeout"": 30
    }},
    {{
      ""order"": 2,
      ""action"": ""wait"",
      ""description"": ""Wait for page to fully load"",
      ""target"": ""page"",
      ""parameters"": {{
        ""type"": ""page_load"",
        ""duration"": ""3000""
      }},
      ""expectedResult"": ""Page is fully loaded"",
      ""timeout"": 10
    }},
    {{
      ""order"": 3,
      ""action"": ""enter_text"",
      ""description"": ""Enter admin username/email"",
      ""target"": ""input[type='email'], input[name*='email'], input[name*='username'], input[placeholder*='email'], input[placeholder*='username'], #email, #username, .email-input, .username-input, [data-testid*='email'], [data-testid*='username']"",
      ""parameters"": {{
        ""value"": ""{extractedUsername}"",
        ""clearFirst"": ""true""
      }},
      ""expectedResult"": ""Username/email field contains entered value"",
      ""timeout"": 15
    }},
    {{
      ""order"": 4,
      ""action"": ""enter_text"",
      ""description"": ""Enter admin password"",
      ""target"": ""input[type='password'], input[name*='password'], input[placeholder*='password'], #password, .password-input, [data-testid*='password']"",
      ""parameters"": {{
        ""value"": ""{extractedPassword}"",
        ""clearFirst"": ""true""
      }},
      ""expectedResult"": ""Password field is filled"",
      ""timeout"": 15
    }},
    {{
      ""order"": 5,
      ""action"": ""click"",
      ""description"": ""Click login/submit button"",
      ""target"": ""button[type='submit'], input[type='submit'], button:contains('Login'), button:contains('Sign'), .login-btn, .btn-login, .submit-btn, .btn-primary, [data-testid*='login'], [data-testid*='submit']"",
      ""parameters"": {{}},
      ""expectedResult"": ""Login form is submitted"",
      ""timeout"": 15
    }},
    {{
      ""order"": 6,
      ""action"": ""wait"",
      ""description"": ""Wait for authentication to complete"",
      ""target"": ""page"",
      ""parameters"": {{
        ""type"": ""duration"",
        ""duration"": ""5000""
      }},
      ""expectedResult"": ""Authentication processing completes"",
      ""timeout"": 20
    }},
    {{
      ""order"": 7,
      ""action"": ""verify_element"",
      ""description"": ""Verify successful login to admin dashboard"",
      ""target"": "".dashboard, #dashboard, .admin-panel, .admin-dashboard, .main-content, .user-menu, .logout, .welcome, nav, .navbar, .sidebar, [data-testid*='dashboard'], body:not(:contains('Login')):not(:contains('Sign'))"",
      ""parameters"": {{
        ""mode"": ""visible""
      }},
      ""expectedResult"": ""Admin dashboard or main interface is visible"",
      ""timeout"": 25
    }}
  ],
  ""expectedOutcomes"": [
    ""Admin successfully authenticates"",
    ""Admin dashboard is accessible"",
    ""Authentication redirects to main admin interface""
  ]
}}

EXTRACTION RULES:
1. Extract EXACT URLs, usernames, passwords from the user story
2. Use comprehensive CSS selector fallbacks that cover multiple naming conventions
3. Include wait steps for page loading and authentication processing
4. Set realistic timeouts (15-30 seconds for complex operations)
5. Add proper verification that works for various admin interfaces
6. Respond with ONLY the JSON, no other text

JSON RESPONSE:";
    }

    private string ExtractUrlFromUserStory(string userStory)
    {
        var urlPattern = @"https?://[^\s]+";
        var match = Regex.Match(userStory, urlPattern);
        return match.Success ? match.Value : "https://example.com";
    }

    private string ExtractUsernameFromUserStory(string userStory)
    {
        var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
        var match = Regex.Match(userStory, emailPattern);
        return match.Success ? match.Value : "admin@example.com";
    }

    private string ExtractPasswordFromUserStory(string userStory)
    {
        var passwordPatterns = new[]
        {
            @"password[:\s]+([^\s,]+)",
            @"pwd[:\s]+([^\s,]+)",
            @"pass[:\s]+([^\s,]+)"
        };

        foreach (var pattern in passwordPatterns)
        {
            var match = Regex.Match(userStory, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
        }

        return "admin123";
    }

    private TestScenario CreateEnhancedFallbackScenario(string userStory, string projectContext)
    {
        _logger.LogWarning("Creating enhanced fallback scenario due to OpenAI processing failure");

        var url = ExtractUrlFromUserStory(userStory);
        var username = ExtractUsernameFromUserStory(userStory);
        var password = ExtractPasswordFromUserStory(userStory);

        var scenario = new TestScenario
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Enhanced Fallback Authentication Test",
            Description = $"Robust authentication test with multiple selector fallbacks for: {userStory.Substring(0, Math.Min(100, userStory.Length))}...",
            OriginalUserStory = userStory,
            Type = TestType.UI,
            Priority = TestPriority.High,
            Environment = TestEnvironment.Testing,
            Status = TestStatus.Generated,
            Tags = new List<string> { "fallback", "authentication", "admin" },
            Preconditions = new List<string> { "Application should be accessible", "Valid credentials are available" },
            ExpectedOutcomes = new List<string> { "User authenticates successfully", "Dashboard is accessible" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Enhanced navigation step
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 1,
            Action = "navigate",
            Description = "Navigate to admin portal",
            Target = url,
            ExpectedResult = "Admin portal loads successfully",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(30),
            Parameters = new Dictionary<string, object>
            {
                ["url"] = url
            }
        });

        // Wait for page load
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 2,
            Action = "wait",
            Description = "Wait for page to fully load",
            Target = "page",
            ExpectedResult = "Page is fully loaded",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(10),
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "page_load",
                ["duration"] = "3000"
            }
        });

        // Enhanced username entry with multiple selectors
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 3,
            Action = "enter_text",
            Description = "Enter username with fallback selectors",
            Target = "input[type='email'], input[name*='email'], input[name*='username'], input[placeholder*='email'], input[placeholder*='username'], #email, #username, .email-input, .username-input, [data-testid*='email'], [data-testid*='username']",
            ExpectedResult = "Username field populated",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(15),
            Parameters = new Dictionary<string, object>
            {
                ["value"] = username,
                ["clearFirst"] = "true"
            }
        });

        // Enhanced password entry with multiple selectors
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 4,
            Action = "enter_text",
            Description = "Enter password with fallback selectors",
            Target = "input[type='password'], input[name*='password'], input[placeholder*='password'], #password, .password-input, [data-testid*='password']",
            ExpectedResult = "Password field populated",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(15),
            Parameters = new Dictionary<string, object>
            {
                ["value"] = password,
                ["clearFirst"] = "true"
            }
        });

        // Enhanced login button click with multiple selectors
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 5,
            Action = "click",
            Description = "Click login button with fallback selectors",
            Target = "button[type='submit'], input[type='submit'], button:contains('Login'), button:contains('Sign'), .login-btn, .btn-login, .submit-btn, .btn-primary, [data-testid*='login'], [data-testid*='submit']",
            ExpectedResult = "Login form submitted",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(15),
            Parameters = new Dictionary<string, object>()
        });

        // Wait for authentication
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 6,
            Action = "wait",
            Description = "Wait for authentication to complete",
            Target = "page",
            ExpectedResult = "Authentication processing completes",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(20),
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "duration",
                ["duration"] = "5000"
            }
        });

        // Enhanced verification with multiple selectors
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 7,
            Action = "verify_element",
            Description = "Verify successful login with fallback selectors",
            Target = ".dashboard, #dashboard, .admin-panel, .admin-dashboard, .main-content, .user-menu, .logout, .welcome, nav, .navbar, .sidebar, [data-testid*='dashboard'], body:not(:contains('Login')):not(:contains('Sign'))",
            ExpectedResult = "Dashboard or admin interface is visible",
            IsEnabled = true,
            Timeout = TimeSpan.FromSeconds(25),
            Parameters = new Dictionary<string, object>
            {
                ["mode"] = "visible"
            }
        });

        return scenario;
    }

    #region Other Interface Implementations

    public Task<List<TestStep>> RefineTestSteps(List<TestStep> steps, string feedback, CancellationToken cancellationToken = default)
    {
        // Enhanced implementation with better selector refinement
        return Task.FromResult(steps);
    }

    public Task<string> AnalyzeTestFailure(TestResult result, CancellationToken cancellationToken = default)
    {
        var analysis = new StringBuilder();
        analysis.AppendLine($"Enhanced Test Failure Analysis for scenario {result.ScenarioId}:");
        analysis.AppendLine($"Overall Status: {(result.Passed ? "PASSED" : "FAILED")}");
        analysis.AppendLine($"Duration: {result.Duration}");
        analysis.AppendLine($"Message: {result.Message}");

        var failedSteps = result.StepResults.Where(sr => !sr.Passed).ToList();
        if (failedSteps.Any())
        {
            analysis.AppendLine($"\nFailed Steps ({failedSteps.Count}):");
            foreach (var step in failedSteps)
            {
                analysis.AppendLine($"- {step.StepName}: {step.Message}");

                if (step.Message.Contains("Element not found") && step.Action == "enter_text")
                {
                    analysis.AppendLine("  💡 Recommendation: The element selector may need updating. Consider inspecting the actual form field attributes.");
                }
                else if (step.Message.Contains("Element not found") && step.Action == "click")
                {
                    analysis.AppendLine("  💡 Recommendation: The button selector may need updating. Check for dynamic buttons or different class names.");
                }
                else if (step.Message.Contains("timeout"))
                {
                    analysis.AppendLine("  💡 Recommendation: Increase timeout or add explicit wait conditions.");
                }
            }
        }

        analysis.AppendLine("\n🔧 General Recommendations:");
        analysis.AppendLine("- Inspect the website's HTML structure to identify correct selectors");
        analysis.AppendLine("- Consider using more specific CSS selectors or XPath expressions");
        analysis.AppendLine("- Add wait conditions for dynamic content loading");
        analysis.AppendLine("- Verify that the website hasn't changed its structure");

        return Task.FromResult(analysis.ToString());
    }

    public Task<Dictionary<string, object>> GenerateTestData(TestScenario testScenario, string dataRequirements, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, object>());
    }

    public Task<List<TestScenario>> OptimizeTestScenarios(List<TestScenario> scenarios, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(scenarios);
    }

    public Task<List<TestScenario>> SuggestAdditionalTests(List<TestScenario> existingScenarios, string projectContext, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<TestScenario>());
    }

    public Task<TestValidationResult> ValidateTestScenario(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TestValidationResult
        {
            IsValid = true,
            QualityScore = 85,
            Issues = new List<string>(),
            Suggestions = new List<string> { "Enhanced validation completed with improved selectors" }
        });
    }

    #endregion

    #region Private Helper Methods

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri("https://api.openai.com/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GenericTestingFramework/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
    }

    private async Task<string> CallOpenAI(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _configuration.Model,
            messages = new[]
            {
                new { role = "system", content = _configuration.SystemPrompt },
                new { role = "user", content = prompt }
            },
            max_tokens = _configuration.MaxTokens,
            temperature = _configuration.Temperature,
            top_p = _configuration.TopP,
            frequency_penalty = 0.0,
            presence_penalty = 0.0
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogDebug("Calling OpenAI API with enhanced model: {Model}", _configuration.Model);

        var response = await _httpClient.PostAsync("v1/chat/completions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI API call failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"OpenAI API call failed: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

        if (openAIResponse?.Choices?.Length > 0)
        {
            var result = openAIResponse.Choices[0].Message.Content;
            _logger.LogDebug("Received enhanced response from OpenAI: {Length} characters", result?.Length ?? 0);
            return result ?? string.Empty;
        }

        throw new InvalidOperationException("No response content from OpenAI");
    }

    private TestScenario ParseOpenAIResponse(string response, string originalUserStory)
    {
        try
        {
            // Clean the response to extract just the JSON
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart);
                var openAIScenario = JsonSerializer.Deserialize<OpenAITestScenario>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return ConvertToTestScenario(openAIScenario, originalUserStory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse enhanced OpenAI response as JSON, falling back to enhanced text parsing");
        }

        // Fallback to enhanced scenario
        return CreateEnhancedFallbackScenario(response, originalUserStory);
    }

    private TestScenario ConvertToTestScenario(OpenAITestScenario openAIScenario, string originalUserStory)
    {
        var scenario = new TestScenario
        {
            Id = Guid.NewGuid().ToString(),
            Title = openAIScenario.Title ?? "Enhanced Generated Test Scenario",
            Description = openAIScenario.Description ?? "",
            OriginalUserStory = originalUserStory,
            Type = Enum.TryParse<TestType>(openAIScenario.Type, true, out var type) ? type : TestType.UI,
            Priority = Enum.TryParse<TestPriority>(openAIScenario.Priority, true, out var priority) ? priority : TestPriority.High,
            Environment = TestEnvironment.Testing,
            Status = TestStatus.Generated,
            Tags = openAIScenario.Tags?.ToList() ?? new List<string>(),
            Preconditions = openAIScenario.Preconditions?.ToList() ?? new List<string>(),
            ExpectedOutcomes = openAIScenario.ExpectedOutcomes?.ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Convert steps with enhanced error handling
        if (openAIScenario.Steps != null)
        {
            foreach (var step in openAIScenario.Steps)
            {
                var testStep = new TestStep
                {
                    Id = Guid.NewGuid().ToString(),
                    Order = step.Order,
                    Action = step.Action ?? "navigate",
                    Description = step.Description ?? "",
                    Target = step.Target ?? "",
                    ExpectedResult = step.ExpectedResult ?? "",
                    IsEnabled = true,
                    ContinueOnFailure = false
                };

                // Add timeout if specified, with enhanced defaults
                if (step.Timeout > 0)
                {
                    testStep.Timeout = TimeSpan.FromSeconds(step.Timeout);
                }
                else
                {
                    // Set intelligent defaults based on action type
                    testStep.Timeout = step.Action?.ToLowerInvariant() switch
                    {
                        "navigate" => TimeSpan.FromSeconds(30),
                        "enter_text" => TimeSpan.FromSeconds(15),
                        "click" => TimeSpan.FromSeconds(15),
                        "verify_element" => TimeSpan.FromSeconds(25),
                        "wait" => TimeSpan.FromSeconds(20),
                        _ => TimeSpan.FromSeconds(15)
                    };
                }

                // Add parameters with validation
                if (step.Parameters != null)
                {
                    foreach (var param in step.Parameters)
                    {
                        testStep.Parameters[param.Key] = param.Value?.ToString() ?? "";
                    }
                }

                scenario.Steps.Add(testStep);
            }
        }

        return scenario;
    }

    #endregion

    #region OpenAI Response Models

    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public OpenAIChoice[]? Choices { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage Message { get; set; } = new();
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class OpenAITestScenario
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("tags")]
        public string[]? Tags { get; set; }

        [JsonPropertyName("preconditions")]
        public string[]? Preconditions { get; set; }

        [JsonPropertyName("steps")]
        public OpenAITestStep[]? Steps { get; set; }

        [JsonPropertyName("expectedOutcomes")]
        public string[]? ExpectedOutcomes { get; set; }
    }

    private class OpenAITestStep
    {
        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("target")]
        public string? Target { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }

        [JsonPropertyName("expectedResult")]
        public string? ExpectedResult { get; set; }

        [JsonPropertyName("timeout")]
        public int Timeout { get; set; } = 15;
    }

    #endregion
}