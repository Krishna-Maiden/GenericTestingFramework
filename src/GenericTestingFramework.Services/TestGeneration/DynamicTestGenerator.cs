using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using GenericTestingFramework.Services.Documents;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace GenericTestingFramework.Services.TestGeneration;

/// <summary>
/// Dynamic test case generator that analyzes user stories and generates appropriate test scenarios
/// </summary>
public class DynamicTestGenerator : ILLMService
{
    private readonly ILogger<DynamicTestGenerator> _logger;
    private readonly IDocumentManager _documentManager;

    public DynamicTestGenerator(ILogger<DynamicTestGenerator> logger, IDocumentManager documentManager)
    {
        _logger = logger;
        _documentManager = documentManager;
    }

    public async Task<TestScenario> GenerateTestFromNaturalLanguage(
        string userStory, string projectContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating test scenario from user story dynamically");

        // Create document from user story
        var document = await _documentManager.CreateUserStoryFromText(userStory, projectContext, cancellationToken);

        // Analyze the user story content
        var analysis = AnalyzeUserStory(document.Content);
        
        // Generate test scenario based on analysis
        var scenario = new TestScenario
        {
            Title = GenerateTestTitle(analysis),
            Description = GenerateTestDescription(analysis, userStory),
            OriginalUserStory = userStory,
            Type = DetermineTestType(analysis),
            Status = TestStatus.Generated,
            Priority = DeterminePriority(analysis),
            Steps = GenerateTestSteps(analysis),
            Tags = ExtractTags(analysis),
            Preconditions = GeneratePreconditions(analysis),
            ExpectedOutcomes = GenerateExpectedOutcomes(analysis)
        };

        _logger.LogInformation("Generated test scenario: {Title} with {StepCount} steps", scenario.Title, scenario.Steps.Count);
        return scenario;
    }

    public Task<List<TestStep>> RefineTestSteps(List<TestStep> steps, string feedback, CancellationToken cancellationToken = default)
    {
        // Implement step refinement based on feedback
        return Task.FromResult(steps);
    }

    public Task<string> AnalyzeTestFailure(TestResult result, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Test failure analysis for scenario {result.ScenarioId}: {result.Message}");
    }

    public Task<Dictionary<string, object>> GenerateTestData(TestScenario testScenario, string dataRequirements, CancellationToken cancellationToken = default)
    {
        var testData = new Dictionary<string, object>();
        
        // Extract test data from scenario steps
        foreach (var step in testScenario.Steps)
        {
            if (step.Parameters.Any())
            {
                foreach (var param in step.Parameters)
                {
                    testData[param.Key] = param.Value;
                }
            }
        }

        return Task.FromResult(testData);
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
        var validation = new TestValidationResult
        {
            IsValid = scenario.Steps.Any(),
            QualityScore = CalculateQualityScore(scenario),
            Issues = ValidateScenarioIssues(scenario),
            Suggestions = GenerateImprovementSuggestions(scenario)
        };

        return Task.FromResult(validation);
    }

    #region Private Analysis Methods

    private UserStoryAnalysis AnalyzeUserStory(string userStory)
    {
        var analysis = new UserStoryAnalysis
        {
            Content = userStory,
            Keywords = ExtractKeywords(userStory),
            Actions = ExtractActions(userStory),
            Urls = ExtractUrls(userStory),
            Credentials = ExtractCredentials(userStory),
            FormFields = ExtractFormFields(userStory),
            NavigationFlow = DetermineNavigationFlow(userStory)
        };

        return analysis;
    }

    private List<string> ExtractKeywords(string content)
    {
        var keywords = new List<string>();
        var keywordPatterns = new Dictionary<string, string[]>
        {
            ["authentication"] = new[] { "login", "signin", "authenticate", "credentials", "password", "username", "email" },
            ["navigation"] = new[] { "navigate", "go to", "visit", "open", "access", "browse" },
            ["form"] = new[] { "enter", "fill", "input", "type", "submit", "click", "select" },
            ["verification"] = new[] { "verify", "check", "confirm", "validate", "assert", "ensure" },
            ["admin"] = new[] { "admin", "administrator", "management", "dashboard", "portal" }
        };

        foreach (var category in keywordPatterns)
        {
            foreach (var keyword in category.Value)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    keywords.Add(category.Key);
                    break;
                }
            }
        }

        return keywords.Distinct().ToList();
    }

    private List<string> ExtractActions(string content)
    {
        var actions = new List<string>();
        var actionPatterns = new[]
        {
            @"login|sign in|authenticate",
            @"navigate|go to|visit",
            @"enter|input|type|fill",
            @"click|press|submit",
            @"verify|check|validate",
            @"logout|sign out",
            @"select|choose|pick"
        };

        foreach (var pattern in actionPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                actions.Add(match.Value.ToLowerInvariant());
            }
        }

        return actions.Distinct().ToList();
    }

    private List<string> ExtractUrls(string content)
    {
        var urls = new List<string>();
        var urlPattern = @"https?://[^\s]+";
        var matches = Regex.Matches(content, urlPattern);
        
        foreach (Match match in matches)
        {
            urls.Add(match.Value);
        }

        return urls;
    }

    private Dictionary<string, string> ExtractCredentials(string content)
    {
        var credentials = new Dictionary<string, string>();
        
        // Extract email patterns
        var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
        var emailMatch = Regex.Match(content, emailPattern);
        if (emailMatch.Success)
        {
            credentials["username"] = emailMatch.Value;
        }

        // Extract password patterns (looking for common password formats)
        var passwordPatterns = new[]
        {
            @"password[:\s]+([^\s,]+)",
            @"pwd[:\s]+([^\s,]+)",
            @"pass[:\s]+([^\s,]+)"
        };

        foreach (var pattern in passwordPatterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                credentials["password"] = match.Groups[1].Value;
                break;
            }
        }

        return credentials;
    }

    private List<string> ExtractFormFields(string content)
    {
        var fields = new List<string>();
        var fieldPatterns = new[]
        {
            "email", "username", "password", "name", "phone", "address",
            "firstname", "lastname", "company", "title", "description"
        };

        foreach (var field in fieldPatterns)
        {
            if (content.Contains(field, StringComparison.OrdinalIgnoreCase))
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private string DetermineNavigationFlow(string content)
    {
        if (content.Contains("login", StringComparison.OrdinalIgnoreCase))
            return "authentication_flow";
        if (content.Contains("register", StringComparison.OrdinalIgnoreCase))
            return "registration_flow";
        if (content.Contains("dashboard", StringComparison.OrdinalIgnoreCase))
            return "dashboard_flow";
        
        return "general_flow";
    }

    private string GenerateTestTitle(UserStoryAnalysis analysis)
    {
        if (analysis.Keywords.Contains("authentication"))
            return "Authentication Test";
        if (analysis.Keywords.Contains("navigation"))
            return "Navigation Test";
        if (analysis.Keywords.Contains("form"))
            return "Form Submission Test";
        
        return "General UI Test";
    }

    private string GenerateTestDescription(UserStoryAnalysis analysis, string userStory)
    {
        return $"Automated test generated from user story: {userStory.Substring(0, Math.Min(userStory.Length, 100))}...";
    }

    private TestType DetermineTestType(UserStoryAnalysis analysis)
    {
        if (analysis.Keywords.Contains("authentication") || analysis.Keywords.Contains("form"))
            return TestType.UI;
        
        return TestType.UI; // Default to UI for now
    }

    private TestPriority DeterminePriority(UserStoryAnalysis analysis)
    {
        if (analysis.Keywords.Contains("authentication") || analysis.Keywords.Contains("admin"))
            return TestPriority.High;
        
        return TestPriority.Medium;
    }

    private List<TestStep> GenerateTestSteps(UserStoryAnalysis analysis)
    {
        var steps = new List<TestStep>();
        var stepOrder = 1;

        // Generate navigation step if URL is present
        if (analysis.Urls.Any())
        {
            steps.Add(new TestStep
            {
                Order = stepOrder++,
                Action = "navigate",
                Target = analysis.Urls.First(),
                Description = $"Navigate to {analysis.Urls.First()}",
                ExpectedResult = "Page loads successfully",
                Timeout = TimeSpan.FromSeconds(30)
            });
        }

        // Generate authentication steps if credentials are present
        if (analysis.NavigationFlow == "authentication_flow" && analysis.Credentials.Any())
        {
            if (analysis.Credentials.ContainsKey("username"))
            {
                steps.Add(new TestStep
                {
                    Order = stepOrder++,
                    Action = "enter_text",
                    Target = "input[type='email'], input[name='email'], #email, #username, [name='username']",
                    Description = "Enter username/email",
                    ExpectedResult = "Username field populated",
                    Parameters = new Dictionary<string, object> { ["value"] = analysis.Credentials["username"] },
                    Timeout = TimeSpan.FromSeconds(10)
                });
            }

            if (analysis.Credentials.ContainsKey("password"))
            {
                steps.Add(new TestStep
                {
                    Order = stepOrder++,
                    Action = "enter_text",
                    Target = "input[type='password'], input[name='password'], #password",
                    Description = "Enter password",
                    ExpectedResult = "Password field populated",
                    Parameters = new Dictionary<string, object> { ["value"] = analysis.Credentials["password"] },
                    Timeout = TimeSpan.FromSeconds(10)
                });
            }

            // Add login button click
            steps.Add(new TestStep
            {
                Order = stepOrder++,
                Action = "click",
                Target = "button[type='submit'], input[type='submit'], .login-btn, #login, .btn-login, .submit-btn",
                Description = "Click login button",
                ExpectedResult = "Login form submitted",
                Timeout = TimeSpan.FromSeconds(10)
            });

            // Add wait for authentication
            steps.Add(new TestStep
            {
                Order = stepOrder++,
                Action = "wait",
                Target = "page",
                Description = "Wait for authentication to complete",
                ExpectedResult = "Page redirects after authentication",
                Parameters = new Dictionary<string, object> { ["type"] = "duration", ["duration"] = "3000" },
                Timeout = TimeSpan.FromSeconds(15)
            });

            // Add verification step
            steps.Add(new TestStep
            {
                Order = stepOrder++,
                Action = "verify_element",
                Target = ".dashboard, #dashboard, .admin-panel, .user-menu, .logout, .welcome",
                Description = "Verify successful login",
                ExpectedResult = "Dashboard or admin panel elements are visible",
                Parameters = new Dictionary<string, object> { ["mode"] = "visible" },
                Timeout = TimeSpan.FromSeconds(10)
            });
        }

        // If no specific steps generated, add basic verification
        if (!steps.Any())
        {
            steps.Add(new TestStep
            {
                Order = 1,
                Action = "verify_element",
                Target = "body",
                Description = "Verify page loads",
                ExpectedResult = "Page content is visible",
                Parameters = new Dictionary<string, object> { ["mode"] = "visible" }
            });
        }

        return steps;
    }

    private List<string> ExtractTags(UserStoryAnalysis analysis)
    {
        var tags = new List<string>();
        tags.AddRange(analysis.Keywords);
        
        if (analysis.Urls.Any())
            tags.Add("web");
        
        return tags.Distinct().ToList();
    }

    private List<string> GeneratePreconditions(UserStoryAnalysis analysis)
    {
        var preconditions = new List<string>();
        
        if (analysis.Urls.Any())
            preconditions.Add($"Application is accessible at {analysis.Urls.First()}");
        
        if (analysis.Credentials.Any())
            preconditions.Add("Valid user credentials are available");
        
        return preconditions;
    }

    private List<string> GenerateExpectedOutcomes(UserStoryAnalysis analysis)
    {
        var outcomes = new List<string>();
        
        if (analysis.NavigationFlow == "authentication_flow")
        {
            outcomes.Add("User is successfully authenticated");
            outcomes.Add("User is redirected to appropriate page after login");
        }
        else
        {
            outcomes.Add("Test scenario completes successfully");
            outcomes.Add("All verification steps pass");
        }
        
        return outcomes;
    }

    private int CalculateQualityScore(TestScenario scenario)
    {
        int score = 50; // Base score
        
        if (scenario.Steps.Count >= 3) score += 20;
        if (scenario.Preconditions.Any()) score += 10;
        if (scenario.ExpectedOutcomes.Any()) score += 10;
        if (scenario.Tags.Any()) score += 5;
        if (!string.IsNullOrEmpty(scenario.Description)) score += 5;
        
        return Math.Min(100, score);
    }

    private List<string> ValidateScenarioIssues(TestScenario scenario)
    {
        var issues = new List<string>();
        
        if (!scenario.Steps.Any())
            issues.Add("No test steps defined");
        
        if (string.IsNullOrEmpty(scenario.Description))
            issues.Add("Missing test description");
        
        return issues;
    }

    private List<string> GenerateImprovementSuggestions(TestScenario scenario)
    {
        var suggestions = new List<string>();
        
        if (scenario.Steps.Count < 3)
            suggestions.Add("Consider adding more verification steps");
        
        if (!scenario.Preconditions.Any())
            suggestions.Add("Add preconditions for better test clarity");
        
        return suggestions;
    }

    #endregion
}

/// <summary>
/// Analysis result of a user story
/// </summary>
public class UserStoryAnalysis
{
    public string Content { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<string> Urls { get; set; } = new();
    public Dictionary<string, string> Credentials { get; set; } = new();
    public List<string> FormFields { get; set; } = new();
    public string NavigationFlow { get; set; } = string.Empty;
}