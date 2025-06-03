using System.Text;
using System.Text.Json;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GenericTestingFramework.Services.LLM;

/// <summary>
/// OpenAI implementation of the LLM service for test generation and analysis
/// </summary>
public class OpenAILLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAILLMService> _logger;
    private readonly LLMConfiguration _configuration;
    private const string OpenAIApiUrl = "https://api.openai.com/v1/chat/completions";

    public OpenAILLMService(
        HttpClient httpClient,
        IOptions<LLMConfiguration> configuration,
        ILogger<OpenAILLMService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration.Value;
        _logger = logger;

        // Configure HTTP client for OpenAI
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GenericTestingFramework/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
    }

    public async Task<TestScenario> GenerateTestFromNaturalLanguage(
        string userStory,
        string projectContext,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating test scenario from user story");

        var prompt = BuildTestGenerationPrompt(userStory, projectContext);

        try
        {
            var response = await CallOpenAI(prompt, cancellationToken);
            var scenario = ParseTestScenarioFromResponse(response, userStory);

            _logger.LogInformation("Successfully generated test scenario: {Title}", scenario.Title);
            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test from natural language");
            return CreateFallbackScenario(userStory, projectContext);
        }
    }

    public async Task<List<TestStep>> RefineTestSteps(
        List<TestStep> steps,
        string feedback,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refining {StepCount} test steps based on feedback", steps.Count);

        var stepsJson = JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = true });
        var prompt = BuildStepRefinementPrompt(stepsJson, feedback);

        try
        {
            var response = await CallOpenAI(prompt, cancellationToken);
            var refinedSteps = ParseTestStepsFromResponse(response);

            _logger.LogInformation("Successfully refined test steps to {NewStepCount} steps", refinedSteps.Count);
            return refinedSteps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refine test steps");
            return steps; // Return original steps if refinement fails
        }
    }

    public async Task<string> AnalyzeTestFailure(
        TestResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing test failure for scenario {ScenarioId}", result.ScenarioId);

        var prompt = BuildFailureAnalysisPrompt(result);

        try
        {
            var analysis = await CallOpenAI(prompt, cancellationToken);
            _logger.LogInformation("Successfully analyzed test failure");
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze test failure");
            return "Unable to analyze failure automatically. Please review the test results manually for detailed error information.";
        }
    }

    public async Task<Dictionary<string, object>> GenerateTestData(
        TestScenario testScenario,
        string dataRequirements,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating test data for scenario {ScenarioId}", testScenario.Id);

        var prompt = BuildTestDataGenerationPrompt(testScenario, dataRequirements);

        try
        {
            var response = await CallOpenAI(prompt, cancellationToken);
            var testData = ParseTestDataFromResponse(response);

            _logger.LogInformation("Successfully generated test data with {DataCount} entries", testData.Count);
            return testData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test data");
            return new Dictionary<string, object>();
        }
    }

    public async Task<List<TestScenario>> OptimizeTestScenarios(
        List<TestScenario> scenarios,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing {ScenarioCount} test scenarios", scenarios.Count);

        var optimizedScenarios = new List<TestScenario>();

        foreach (var scenario in scenarios)
        {
            try
            {
                var prompt = BuildOptimizationPrompt(scenario);
                var response = await CallOpenAI(prompt, cancellationToken);
                var optimizedScenario = ParseOptimizedScenarioFromResponse(response, scenario);
                optimizedScenarios.Add(optimizedScenario);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to optimize scenario {ScenarioId}, keeping original", scenario.Id);
                optimizedScenarios.Add(scenario);
            }
        }

        _logger.LogInformation("Successfully optimized test scenarios");
        return optimizedScenarios;
    }

    public async Task<List<TestScenario>> SuggestAdditionalTests(
        List<TestScenario> existingScenarios,
        string projectContext,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting additional tests based on {ExistingCount} scenarios", existingScenarios.Count);

        var prompt = BuildAdditionalTestSuggestionPrompt(existingScenarios, projectContext);

        try
        {
            var response = await CallOpenAI(prompt, cancellationToken);
            var suggestedScenarios = ParseSuggestedScenariosFromResponse(response);

            _logger.LogInformation("Successfully suggested {SuggestedCount} additional test scenarios", suggestedScenarios.Count);
            return suggestedScenarios;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suggest additional tests");
            return new List<TestScenario>();
        }
    }

    public async Task<TestValidationResult> ValidateTestScenario(
        TestScenario scenario,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating test scenario {ScenarioId}", scenario.Id);

        var prompt = BuildValidationPrompt(scenario);

        try
        {
            var response = await CallOpenAI(prompt, cancellationToken);
            var validationResult = ParseValidationResultFromResponse(response);

            _logger.LogInformation("Successfully validated test scenario with quality score {QualityScore}", validationResult.QualityScore);
            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate test scenario");
            return new TestValidationResult
            {
                IsValid = false,
                QualityScore = 0,
                Issues = new List<string> { "Unable to validate scenario automatically" }
            };
        }
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
            top_p = _configuration.TopP
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(OpenAIApiUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"OpenAI API call failed with status {response.StatusCode}: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseContent);

        var choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No response choices returned from OpenAI");
        }

        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private string BuildTestGenerationPrompt(string userStory, string projectContext)
    {
        return $@"Generate a comprehensive automated test scenario for the following user story:

User Story: {userStory}

Project Context: {projectContext}

Please analyze the user story and determine:
1. Whether this requires UI testing, API testing, or both
2. The specific test steps needed to validate the user story
3. Expected outcomes and assertions

Return your response as a JSON object with this exact structure:
{{
    ""title"": ""Brief descriptive title for the test"",
    ""type"": ""UI"" | ""API"" | ""Mixed"",
    ""description"": ""Detailed description of what this test validates"",
    ""priority"": ""Low"" | ""Medium"" | ""High"" | ""Critical"",
    ""tags"": [""tag1"", ""tag2""],
    ""preconditions"": [""condition1"", ""condition2""],
    ""expectedOutcomes"": [""outcome1"", ""outcome2""],
    ""steps"": [
        {{
            ""order"": 1,
            ""action"": ""navigate|click|enter_text|api_get|api_post|verify|wait|etc"",
            ""target"": ""CSS selector, URL, or API endpoint"",
            ""description"": ""Human readable description of this step"",
            ""expectedResult"": ""What should happen when this step executes"",
            ""parameters"": {{""key"": ""value""}},
            ""timeout"": ""00:00:30"",
            ""takeScreenshot"": false,
            ""continueOnFailure"": false
        }}
    ]
}}

Focus on creating realistic, executable test steps that thoroughly validate the user story requirements.";
    }

    private string BuildStepRefinementPrompt(string stepsJson, string feedback)
    {
        return $@"Please refine the following test steps based on the provided feedback:

Current Test Steps:
{stepsJson}

Feedback for Improvement:
{feedback}

Please analyze the feedback and improve the test steps accordingly. Return the refined steps in the same JSON format as the input, but with improvements based on the feedback.

Considerations:
- Make steps more reliable and maintainable
- Add better error handling where needed
- Improve assertions and validations
- Optimize step ordering and timing
- Add missing steps if identified in feedback";
    }

    private string BuildFailureAnalysisPrompt(TestResult result)
    {
        var failedSteps = result.StepResults.Where(sr => !sr.Passed).ToList();
        var failedStepsJson = JsonSerializer.Serialize(failedSteps, new JsonSerializerOptions { WriteIndented = true });

        return $@"Analyze the following test failure and provide actionable insights:

Test Result Summary:
- Scenario ID: {result.ScenarioId}
- Overall Status: {(result.Passed ? "PASSED" : "FAILED")}
- Duration: {result.Duration}
- Environment: {result.Environment}
- Executed By: {result.ExecutedBy}
- Message: {result.Message}

Failed Steps:
{failedStepsJson}

Error Details:
{JsonSerializer.Serialize(result.Error, new JsonSerializerOptions { WriteIndented = true })}

Log Output:
{result.LogOutput}

Please provide:
1. Root cause analysis of the failure
2. Specific recommendations to fix the issue
3. Suggestions to prevent similar failures in the future
4. Any test improvements that could make it more robust

Format your response as a clear, actionable analysis that a developer or QA engineer can use to resolve the issue.";
    }

    private string BuildTestDataGenerationPrompt(TestScenario testScenario, string dataRequirements)
    {
        return $@"Generate test data for the following test scenario:

Test Scenario: {testScenario.Title}
Description: {testScenario.Description}
Type: {testScenario.Type}

Data Requirements:
{dataRequirements}

Test Steps that need data:
{JsonSerializer.Serialize(testScenario.Steps.Where(s => s.Parameters.Any() || s.StepData.Any()), new JsonSerializerOptions { WriteIndented = true })}

Generate realistic test data that covers:
- Valid data scenarios
- Edge cases
- Invalid data for negative testing

Return the data as a JSON object with key-value pairs where keys are parameter names and values are the test data values.";
    }

    private string BuildOptimizationPrompt(TestScenario scenario)
    {
        return $@"Optimize the following test scenario for better performance, reliability, and maintainability:

Current Scenario:
{JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true })}

Please optimize:
1. Step ordering and dependencies
2. Wait times and timeouts
3. Assertions and validations
4. Error handling
5. Parallel execution opportunities

Return the optimized scenario in the same JSON format.";
    }

    private string BuildAdditionalTestSuggestionPrompt(List<TestScenario> existingScenarios, string projectContext)
    {
        var scenarioSummaries = existingScenarios.Select(s => new { s.Title, s.Type, s.Description }).Take(10);
        var summariesJson = JsonSerializer.Serialize(scenarioSummaries, new JsonSerializerOptions { WriteIndented = true });

        return $@"Based on the following existing test scenarios and project context, suggest additional test scenarios that would improve test coverage:

Project Context: {projectContext}

Existing Test Scenarios:
{summariesJson}

Please identify gaps in test coverage and suggest 3-5 additional test scenarios that would provide value. Focus on:
- Edge cases not covered
- Integration scenarios
- Error handling paths
- Performance considerations
- Security aspects

Return as an array of test scenario objects in the same format as the test generation prompt.";
    }

    private string BuildValidationPrompt(TestScenario scenario)
    {
        return $@"Validate the quality and completeness of this test scenario:

{JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true })}

Evaluate:
1. Completeness of test coverage
2. Quality of test steps
3. Assertion adequacy
4. Maintainability factors
5. Potential reliability issues

Return a JSON object with:
{{
    ""isValid"": true/false,
    ""qualityScore"": 0-100,
    ""issues"": [""issue1"", ""issue2""],
    ""suggestions"": [""suggestion1"", ""suggestion2""],
    ""missingCoverage"": [""area1"", ""area2""],
    ""recommendedAssertions"": [""assertion1"", ""assertion2""]
}}";
    }

    private TestScenario ParseTestScenarioFromResponse(string response, string originalUserStory)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            var scenario = new TestScenario
            {
                Title = root.GetProperty("title").GetString() ?? "Generated Test",
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                OriginalUserStory = originalUserStory,
                Type = Enum.Parse<TestType>(root.GetProperty("type").GetString() ?? "Mixed"),
                Status = TestStatus.Generated,
                Priority = root.TryGetProperty("priority", out var priority) ?
                    Enum.Parse<TestPriority>(priority.GetString() ?? "Medium") : TestPriority.Medium
            };

            // Parse tags
            if (root.TryGetProperty("tags", out var tagsElement))
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    if (tag.GetString() is string tagValue)
                        scenario.Tags.Add(tagValue);
                }
            }

            // Parse preconditions
            if (root.TryGetProperty("preconditions", out var preconditionsElement))
            {
                foreach (var condition in preconditionsElement.EnumerateArray())
                {
                    if (condition.GetString() is string conditionValue)
                        scenario.Preconditions.Add(conditionValue);
                }
            }

            // Parse expected outcomes
            if (root.TryGetProperty("expectedOutcomes", out var outcomesElement))
            {
                foreach (var outcome in outcomesElement.EnumerateArray())
                {
                    if (outcome.GetString() is string outcomeValue)
                        scenario.ExpectedOutcomes.Add(outcomeValue);
                }
            }

            // Parse steps
            if (root.TryGetProperty("steps", out var stepsElement))
            {
                foreach (var stepElement in stepsElement.EnumerateArray())
                {
                    var step = ParseTestStepFromJson(stepElement);
                    scenario.Steps.Add(step);
                }
            }

            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse test scenario from LLM response");
            return CreateFallbackScenario(originalUserStory, "");
        }
    }

    private TestStep ParseTestStepFromJson(JsonElement stepElement)
    {
        var step = new TestStep
        {
            Order = stepElement.TryGetProperty("order", out var order) ? order.GetInt32() : 0,
            Action = stepElement.GetProperty("action").GetString() ?? "",
            Target = stepElement.GetProperty("target").GetString() ?? "",
            Description = stepElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
            ExpectedResult = stepElement.TryGetProperty("expectedResult", out var expected) ? expected.GetString() ?? "" : ""
        };

        // Parse parameters
        if (stepElement.TryGetProperty("parameters", out var paramsElement))
        {
            foreach (var param in paramsElement.EnumerateObject())
            {
                step.Parameters[param.Name] = param.Value.ToString();
            }
        }

        // Parse timeout
        if (stepElement.TryGetProperty("timeout", out var timeoutElement) &&
            TimeSpan.TryParse(timeoutElement.GetString(), out var timeout))
        {
            step.Timeout = timeout;
        }

        // Parse boolean properties
        if (stepElement.TryGetProperty("takeScreenshot", out var screenshot))
            step.TakeScreenshot = screenshot.GetBoolean();

        if (stepElement.TryGetProperty("continueOnFailure", out var continueOnFailure))
            step.ContinueOnFailure = continueOnFailure.GetBoolean();

        return step;
    }

    private List<TestStep> ParseTestStepsFromResponse(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var steps = new List<TestStep>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var stepElement in document.RootElement.EnumerateArray())
                {
                    steps.Add(ParseTestStepFromJson(stepElement));
                }
            }

            return steps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse test steps from LLM response");
            return new List<TestStep>();
        }
    }

    private Dictionary<string, object> ParseTestDataFromResponse(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var testData = new Dictionary<string, object>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                testData[property.Name] = property.Value.ToString();
            }

            return testData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse test data from LLM response");
            return new Dictionary<string, object>();
        }
    }

    private TestScenario ParseOptimizedScenarioFromResponse(string response, TestScenario originalScenario)
    {
        try
        {
            var optimizedScenario = ParseTestScenarioFromResponse(response, originalScenario.OriginalUserStory);
            optimizedScenario.Id = originalScenario.Id; // Keep original ID
            optimizedScenario.ProjectId = originalScenario.ProjectId;
            optimizedScenario.CreatedAt = originalScenario.CreatedAt;
            optimizedScenario.CreatedBy = originalScenario.CreatedBy;
            optimizedScenario.UpdatedAt = DateTime.UtcNow;

            return optimizedScenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse optimized scenario from LLM response");
            return originalScenario;
        }
    }

    private List<TestScenario> ParseSuggestedScenariosFromResponse(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var scenarios = new List<TestScenario>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var scenarioElement in document.RootElement.EnumerateArray())
                {
                    var scenarioJson = scenarioElement.GetRawText();
                    var scenario = ParseTestScenarioFromResponse(scenarioJson, "");
                    scenarios.Add(scenario);
                }
            }

            return scenarios;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse suggested scenarios from LLM response");
            return new List<TestScenario>();
        }
    }

    private TestValidationResult ParseValidationResultFromResponse(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            var result = new TestValidationResult
            {
                IsValid = root.GetProperty("isValid").GetBoolean(),
                QualityScore = root.GetProperty("qualityScore").GetInt32()
            };

            // Parse issues
            if (root.TryGetProperty("issues", out var issuesElement))
            {
                foreach (var issue in issuesElement.EnumerateArray())
                {
                    if (issue.GetString() is string issueValue)
                        result.Issues.Add(issueValue);
                }
            }

            // Parse suggestions
            if (root.TryGetProperty("suggestions", out var suggestionsElement))
            {
                foreach (var suggestion in suggestionsElement.EnumerateArray())
                {
                    if (suggestion.GetString() is string suggestionValue)
                        result.Suggestions.Add(suggestionValue);
                }
            }

            // Parse missing coverage
            if (root.TryGetProperty("missingCoverage", out var coverageElement))
            {
                foreach (var coverage in coverageElement.EnumerateArray())
                {
                    if (coverage.GetString() is string coverageValue)
                        result.MissingCoverage.Add(coverageValue);
                }
            }

            // Parse recommended assertions
            if (root.TryGetProperty("recommendedAssertions", out var assertionsElement))
            {
                foreach (var assertion in assertionsElement.EnumerateArray())
                {
                    if (assertion.GetString() is string assertionValue)
                        result.RecommendedAssertions.Add(assertionValue);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse validation result from LLM response");
            return new TestValidationResult
            {
                IsValid = false,
                QualityScore = 0,
                Issues = new List<string> { "Failed to parse validation response" }
            };
        }
    }

    private TestScenario CreateFallbackScenario(string userStory, string projectContext)
    {
        return new TestScenario
        {
            Title = "Manual Test Scenario Required",
            Description = "LLM generation failed. Manual test case creation required.",
            OriginalUserStory = userStory,
            Type = TestType.Mixed,
            Status = TestStatus.Draft,
            Priority = TestPriority.Medium,
            Steps = new List<TestStep>
            {
                new TestStep
                {
                    Order = 1,
                    Action = "manual_review",
                    Target = "N/A",
                    Description = "Manual test case creation required due to LLM generation failure",
                    ExpectedResult = "Test case should be manually created based on the user story",
                    Parameters = new Dictionary<string, object>
                    {
                        ["user_story"] = userStory,
                        ["project_context"] = projectContext
                    }
                }
            }
        };
    }
}