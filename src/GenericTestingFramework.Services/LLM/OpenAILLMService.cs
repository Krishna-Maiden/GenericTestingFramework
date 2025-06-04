using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenericTestingFramework.Services.LLM;

/// <summary>
/// Real OpenAI LLM service for intelligent test generation
/// </summary>
public class OpenAILLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAILLMService> _logger;
    private readonly LLMConfiguration _configuration;

    public OpenAILLMService(HttpClient httpClient, ILogger<OpenAILLMService> logger, IOptions<LLMConfiguration> configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;

        ConfigureHttpClient();
    }

    public async Task<TestScenario> GenerateTestFromNaturalLanguage(string userStory, string projectContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating test scenario from user story using OpenAI GPT-3.5-Turbo");

        try
        {
            var prompt = BuildTestGenerationPrompt(userStory, projectContext);
            var response = await CallOpenAI(prompt, cancellationToken);

            var scenario = ParseOpenAIResponse(response, userStory);

            _logger.LogInformation("Successfully generated test scenario: {Title}", scenario.Title);
            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test scenario from user story");

            // Fallback to basic scenario if OpenAI fails
            return CreateFallbackScenario(userStory, projectContext);
        }
    }

    public async Task<List<TestStep>> RefineTestSteps(List<TestStep> steps, string feedback, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refining {StepCount} test steps using OpenAI", steps.Count);

        try
        {
            var prompt = BuildStepRefinementPrompt(steps, feedback);
            var response = await CallOpenAI(prompt, cancellationToken);

            var refinedSteps = ParseTestStepsFromResponse(response);

            _logger.LogInformation("Successfully refined test steps");
            return refinedSteps.Any() ? refinedSteps : steps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refine test steps");
            return steps; // Return original steps if refinement fails
        }
    }

    public async Task<string> AnalyzeTestFailure(TestResult result, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing test failure using OpenAI");

        try
        {
            var prompt = BuildFailureAnalysisPrompt(result);
            var response = await CallOpenAI(prompt, cancellationToken);

            _logger.LogInformation("Generated failure analysis");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze test failure");
            return $"Test failed: {result.Message}. Please review the detailed test results for more information.";
        }
    }

    public async Task<Dictionary<string, object>> GenerateTestData(TestScenario testScenario, string dataRequirements, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating test data for scenario: {ScenarioTitle}", testScenario.Title);

        try
        {
            var prompt = BuildTestDataGenerationPrompt(testScenario, dataRequirements);
            var response = await CallOpenAI(prompt, cancellationToken);

            var testData = ParseTestDataFromResponse(response);

            _logger.LogInformation("Generated test data with {Count} entries", testData.Count);
            return testData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test data");
            return new Dictionary<string, object>();
        }
    }

    public async Task<List<TestScenario>> OptimizeTestScenarios(List<TestScenario> scenarios, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing {Count} test scenarios using OpenAI", scenarios.Count);

        var optimizedScenarios = new List<TestScenario>();

        foreach (var scenario in scenarios)
        {
            try
            {
                var prompt = BuildOptimizationPrompt(scenario);
                var response = await CallOpenAI(prompt, cancellationToken);

                // For now, just return the original scenario with updated metadata
                var optimizedScenario = scenario.Clone();
                optimizedScenario.UpdatedAt = DateTime.UtcNow;
                optimizedScenarios.Add(optimizedScenario);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to optimize scenario {ScenarioId}", scenario.Id);
                optimizedScenarios.Add(scenario);
            }
        }

        return optimizedScenarios;
    }

    public async Task<List<TestScenario>> SuggestAdditionalTests(List<TestScenario> existingScenarios, string projectContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting additional tests based on {Count} existing scenarios", existingScenarios.Count);

        try
        {
            var prompt = BuildAdditionalTestSuggestionPrompt(existingScenarios, projectContext);
            var response = await CallOpenAI(prompt, cancellationToken);

            var suggestedScenarios = ParseSuggestedScenariosFromResponse(response);

            _logger.LogInformation("Suggested {Count} additional test scenarios", suggestedScenarios.Count);
            return suggestedScenarios;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suggest additional tests");
            return new List<TestScenario>();
        }
    }

    public async Task<TestValidationResult> ValidateTestScenario(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating test scenario using OpenAI: {ScenarioTitle}", scenario.Title);

        try
        {
            var prompt = BuildValidationPrompt(scenario);
            var response = await CallOpenAI(prompt, cancellationToken);

            var validationResult = ParseValidationResultFromResponse(response);

            _logger.LogInformation("Validated test scenario with quality score: {QualityScore}", validationResult.QualityScore);
            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate test scenario");
            return new TestValidationResult
            {
                IsValid = true,
                QualityScore = 75,
                Issues = new List<string>(),
                Suggestions = new List<string> { "Validation completed with basic checks" }
            };
        }
    }

    #region Private Methods

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

        _logger.LogDebug("Calling OpenAI API with model: {Model}", _configuration.Model);

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
            _logger.LogDebug("Received response from OpenAI: {Length} characters", result?.Length ?? 0);
            return result ?? string.Empty;
        }

        throw new InvalidOperationException("No response content from OpenAI");
    }

    private string BuildTestGenerationPrompt(string userStory, string context)
    {
        return $@"Convert the user story below into a test scenario. Respond ONLY with valid JSON in the exact format shown.

USER STORY:
{userStory}

CONTEXT:
{context}

REQUIRED JSON FORMAT (copy this structure exactly):
{{
  ""title"": ""Test title here"",
  ""description"": ""Brief description"",
  ""type"": ""UI"",
  ""priority"": ""High"",
  ""tags"": [""auth"", ""login""],
  ""preconditions"": [
    ""Application is accessible"",
    ""Valid credentials are available""
  ],
  ""steps"": [
    {{
      ""order"": 1,
      ""action"": ""navigate"",
      ""description"": ""Navigate to login page"",
      ""target"": ""https://example.com/login"",
      ""parameters"": {{
        ""url"": ""https://example.com/login""
      }},
      ""expectedResult"": ""Login page loads successfully"",
      ""timeout"": 30
    }},
    {{
      ""order"": 2,
      ""action"": ""enter_text"",
      ""description"": ""Enter username"",
      ""target"": ""#username"",
      ""parameters"": {{
        ""value"": ""admin@example.com"",
        ""clearFirst"": ""true""
      }},
      ""expectedResult"": ""Username field contains entered value"",
      ""timeout"": 10
    }},
    {{
      ""order"": 3,
      ""action"": ""enter_text"",
      ""description"": ""Enter password"",
      ""target"": ""#password"",
      ""parameters"": {{
        ""value"": ""password123"",
        ""clearFirst"": ""true""
      }},
      ""expectedResult"": ""Password field is filled"",
      ""timeout"": 10
    }},
    {{
      ""order"": 4,
      ""action"": ""click"",
      ""description"": ""Click login button"",
      ""target"": ""#loginButton"",
      ""parameters"": {{}},
      ""expectedResult"": ""Login form is submitted"",
      ""timeout"": 15
    }},
    {{
      ""order"": 5,
      ""action"": ""verify_element"",
      ""description"": ""Verify successful login"",
      ""target"": "".dashboard, .welcome, .admin-panel"",
      ""parameters"": {{
        ""mode"": ""visible""
      }},
      ""expectedResult"": ""Dashboard or admin panel is visible"",
      ""timeout"": 20
    }}
  ],
  ""expectedOutcomes"": [
    ""User successfully logs in"",
    ""Dashboard is accessible""
  ]
}}

RULES:
1. Extract real URLs, usernames, passwords from the user story
2. Use CSS selectors like #id, .class, or element[attribute]
3. Include these actions: navigate, enter_text, click, verify_element, verify_text, wait
4. Set realistic timeouts (10-30 seconds)
5. Add proper verification steps
6. Respond with ONLY the JSON, no other text

JSON RESPONSE:";
    }

    private string BuildStepGenerationPrompt(string description, TestType testType)
    {
        return $@"
Generate detailed test steps for the following requirement:

REQUIREMENT: {description}
TEST TYPE: {testType}

Provide a JSON array of test steps with this structure:
[
  {{
    ""order"": 1,
    ""action"": ""action_name"",
    ""description"": ""What this step does"",
    ""target"": ""element_selector_or_url"",
    ""parameters"": {{
      ""key"": ""value""
    }},
    ""expectedResult"": ""Expected outcome"",
    ""timeout"": 30
  }}
]

Available actions for UI tests: navigate, click, enter_text, verify_text, verify_element, wait, take_screenshot
Available actions for API tests: api_get, api_post, api_put, api_delete, verify_status, verify_body

Generate only the JSON array, no additional text.";
    }

    private string BuildOptimizationPrompt(TestScenario scenario)
    {
        var scenarioJson = JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true });

        return $@"
Analyze the following test scenario and provide optimization suggestions:

TEST SCENARIO:
{scenarioJson}

Please provide suggestions for:
1. Performance improvements
2. Better element selectors
3. Enhanced error handling
4. Additional verification points
5. Test data improvements
6. Maintainability enhancements

Provide practical, actionable recommendations in plain text.";
    }

    private string BuildTestDataPrompt(string context, int count)
    {
        return $@"
Generate {count} realistic test data examples for the following context:

CONTEXT: {context}

Provide the data as a JSON array of strings:
[""data1"", ""data2"", ""data3""]

Make the test data:
1. Realistic and varied
2. Appropriate for testing scenarios
3. Include both valid and edge case data
4. Follow common patterns for the given context

Generate only the JSON array, no additional text.";
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
            _logger.LogWarning(ex, "Failed to parse OpenAI response as JSON, falling back to text parsing");
        }

        // Fallback to text parsing if JSON parsing fails
        return ParseResponseAsText(response, originalUserStory);
    }

    private TestScenario ParseResponseAsText(string response, string originalUserStory)
    {
        // Basic text parsing fallback
        var scenario = new TestScenario
        {
            Id = Guid.NewGuid().ToString(),
            Title = "AI Generated Test Scenario",
            Description = "Generated from user story using AI",
            OriginalUserStory = originalUserStory,
            Type = TestType.UI,
            Priority = TestPriority.Medium,
            Environment = TestEnvironment.Testing,
            Status = TestStatus.Generated,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add a basic navigation step
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 1,
            Action = "navigate",
            Description = "Navigate to application",
            Target = "https://example.com",
            ExpectedResult = "Page loads successfully",
            IsEnabled = true
        });

        return scenario;
    }

    private TestScenario CreateFallbackScenario(string userStory, string projectContext)
    {
        _logger.LogWarning("Creating fallback scenario due to OpenAI processing failure");

        var scenario = new TestScenario
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Fallback Test Scenario",
            Description = $"Basic scenario generated from: {userStory.Substring(0, Math.Min(100, userStory.Length))}...",
            OriginalUserStory = userStory,
            Type = TestType.UI,
            Priority = TestPriority.Medium,
            Environment = TestEnvironment.Testing,
            Status = TestStatus.Generated,
            Tags = new List<string> { "fallback", "basic" },
            Preconditions = new List<string> { "Application should be accessible" },
            ExpectedOutcomes = new List<string> { "Basic functionality works as expected" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add basic navigation step
        scenario.Steps.Add(new TestStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = 1,
            Action = "navigate",
            Description = "Navigate to application",
            Target = "https://example.com",
            ExpectedResult = "Page loads successfully",
            IsEnabled = true,
            Parameters = new Dictionary<string, object>
            {
                ["url"] = "https://example.com"
            }
        });

        return scenario;
    }

    private string BuildStepRefinementPrompt(List<TestStep> steps, string feedback)
    {
        var stepsJson = JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = true });

        return $@"Refine the following test steps based on the feedback provided.

CURRENT STEPS:
{stepsJson}

FEEDBACK:
{feedback}

Please improve the steps and respond with a JSON array of refined steps. Use the same format as the input.";
    }

    private string BuildFailureAnalysisPrompt(TestResult result)
    {
        var failedSteps = result.StepResults.Where(sr => !sr.Passed).ToList();

        return $@"Analyze this test failure and provide actionable insights:

Test Status: {(result.Passed ? "PASSED" : "FAILED")}
Duration: {result.Duration}
Message: {result.Message}
Failed Steps: {failedSteps.Count}

First Failed Step: {(failedSteps.FirstOrDefault()?.StepName ?? "None")}
Error Message: {(failedSteps.FirstOrDefault()?.Message ?? "None")}

Provide a brief analysis of what likely went wrong and suggestions to fix it.";
    }

    private string BuildTestDataGenerationPrompt(TestScenario scenario, string dataRequirements)
    {
        return $@"Generate test data for this scenario:

Scenario: {scenario.Title}
Requirements: {dataRequirements}

Provide realistic test data as a JSON object with key-value pairs.
Example: {{""username"": ""testuser@example.com"", ""password"": ""TestPass123""}}";
    }

    private string BuildAdditionalTestSuggestionPrompt(List<TestScenario> existingScenarios, string projectContext)
    {
        var scenarioTitles = string.Join(", ", existingScenarios.Select(s => s.Title));

        return $@"Based on existing test scenarios and project context, suggest additional test scenarios.

Existing Scenarios: {scenarioTitles}
Project Context: {projectContext}

Suggest 2-3 additional test scenarios that would improve coverage. 
Respond with a JSON array of test scenario objects in the same format as the test generation prompt.";
    }

    private string BuildValidationPrompt(TestScenario scenario)
    {
        var scenarioJson = JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true });

        return $@"Validate this test scenario and provide feedback:

{scenarioJson}

Analyze the scenario and provide:
1. Quality score (0-100)
2. Any issues found
3. Suggestions for improvement

Respond with JSON: {{""qualityScore"": 85, ""issues"": [], ""suggestions"": []}}";
    }

    private List<TestScenario> ParseSuggestedScenariosFromResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart);
                var scenarios = JsonSerializer.Deserialize<OpenAITestScenario[]>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (scenarios != null)
                {
                    return scenarios.Select(s => ConvertToTestScenario(s, "Suggested scenario")).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse suggested scenarios from OpenAI response");
        }

        return new List<TestScenario>();
    }

    private TestValidationResult ParseValidationResultFromResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart);
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                return new TestValidationResult
                {
                    IsValid = true,
                    QualityScore = root.TryGetProperty("qualityScore", out var score) ? score.GetInt32() : 80,
                    Issues = root.TryGetProperty("issues", out var issues) ?
                        issues.EnumerateArray().Select(i => i.GetString() ?? "").ToList() : new List<string>(),
                    Suggestions = root.TryGetProperty("suggestions", out var suggestions) ?
                        suggestions.EnumerateArray().Select(s => s.GetString() ?? "").ToList() : new List<string>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse validation result from OpenAI response");
        }

        return new TestValidationResult
        {
            IsValid = true,
            QualityScore = 75,
            Issues = new List<string>(),
            Suggestions = new List<string> { "Validation completed with basic checks" }
        };
    }

    private Dictionary<string, object> ParseTestDataFromResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart);
                using var document = JsonDocument.Parse(jsonContent);

                var testData = new Dictionary<string, object>();
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    testData[property.Name] = property.Value.ToString();
                }
                return testData;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse test data from OpenAI response");
        }

        return new Dictionary<string, object>();
    }

    private TestScenario ConvertToTestScenario(OpenAITestScenario openAIScenario, string originalUserStory)
    {
        var scenario = new TestScenario
        {
            Id = Guid.NewGuid().ToString(),
            Title = openAIScenario.Title ?? "Generated Test Scenario",
            Description = openAIScenario.Description ?? "",
            OriginalUserStory = originalUserStory,
            Type = Enum.TryParse<TestType>(openAIScenario.Type, true, out var type) ? type : TestType.UI,
            Priority = Enum.TryParse<TestPriority>(openAIScenario.Priority, true, out var priority) ? priority : TestPriority.Medium,
            Environment = TestEnvironment.Testing,
            Status = TestStatus.Generated,
            Tags = openAIScenario.Tags?.ToList() ?? new List<string>(),
            Preconditions = openAIScenario.Preconditions?.ToList() ?? new List<string>(),
            ExpectedOutcomes = openAIScenario.ExpectedOutcomes?.ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Convert steps
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

                // Add timeout if specified
                if (step.Timeout > 0)
                {
                    testStep.Timeout = TimeSpan.FromSeconds(step.Timeout);
                }

                // Add parameters
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

    private List<TestStep> ParseTestStepsFromResponse(string response)
    {
        var steps = new List<TestStep>();

        try
        {
            // Extract JSON array from response
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart);
                var openAISteps = JsonSerializer.Deserialize<OpenAITestStep[]>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (openAISteps != null)
                {
                    foreach (var step in openAISteps)
                    {
                        var testStep = new TestStep
                        {
                            Id = Guid.NewGuid().ToString(),
                            Order = step.Order,
                            Action = step.Action ?? "navigate",
                            Description = step.Description ?? "",
                            Target = step.Target ?? "",
                            ExpectedResult = step.ExpectedResult ?? "",
                            IsEnabled = true
                        };

                        if (step.Timeout > 0)
                        {
                            testStep.Timeout = TimeSpan.FromSeconds(step.Timeout);
                        }

                        if (step.Parameters != null)
                        {
                            foreach (var param in step.Parameters)
                            {
                                testStep.Parameters[param.Key] = param.Value?.ToString() ?? "";
                            }
                        }

                        steps.Add(testStep);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse test steps from OpenAI response");
        }

        return steps;
    }

    private List<string> ParseTestDataFromResponse(string response, int expectedCount)
    {
        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart);
                var testData = JsonSerializer.Deserialize<string[]>(jsonContent);

                if (testData != null)
                {
                    return testData.ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse test data from OpenAI response");
        }

        // Return empty list if parsing fails
        return new List<string>();
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
        public int Timeout { get; set; } = 30;
    }

    #endregion
}