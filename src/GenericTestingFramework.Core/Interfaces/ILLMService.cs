using GenericTestingFramework.Core.Models;

namespace GenericTestingFramework.Core.Interfaces;

/// <summary>
/// Interface for Large Language Model services that generate and refine test scenarios
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// Generates a test scenario from natural language user story
    /// </summary>
    /// <param name="userStory">User story or requirement in natural language</param>
    /// <param name="projectContext">Additional context about the project</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated test scenario</returns>
    Task<TestScenario> GenerateTestFromNaturalLanguage(
        string userStory, 
        string projectContext, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refines existing test steps based on feedback
    /// </summary>
    /// <param name="steps">Current test steps</param>
    /// <param name="feedback">Feedback for improvement</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refined test steps</returns>
    Task<List<TestStep>> RefineTestSteps(
        List<TestStep> steps, 
        string feedback, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes test failure and provides actionable insights
    /// </summary>
    /// <param name="result">Failed test result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis and recommendations</returns>
    Task<string> AnalyzeTestFailure(
        TestResult result, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates test data based on test requirements
    /// </summary>
    /// <param name="testScenario">Test scenario requiring data</param>
    /// <param name="dataRequirements">Specific data requirements</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated test data</returns>
    Task<Dictionary<string, object>> GenerateTestData(
        TestScenario testScenario, 
        string dataRequirements, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes test scenarios for better performance and reliability
    /// </summary>
    /// <param name="scenarios">Test scenarios to optimize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimized test scenarios</returns>
    Task<List<TestScenario>> OptimizeTestScenarios(
        List<TestScenario> scenarios, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests additional test cases based on existing scenarios
    /// </summary>
    /// <param name="existingScenarios">Current test scenarios</param>
    /// <param name="projectContext">Project context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Suggested additional test scenarios</returns>
    Task<List<TestScenario>> SuggestAdditionalTests(
        List<TestScenario> existingScenarios, 
        string projectContext, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates test scenario completeness and quality
    /// </summary>
    /// <param name="scenario">Test scenario to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation feedback and suggestions</returns>
    Task<TestValidationResult> ValidateTestScenario(
        TestScenario scenario, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of test scenario validation by LLM
/// </summary>
public class TestValidationResult
{
    /// <summary>
    /// Whether the test scenario is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Quality score (0-100)
    /// </summary>
    public int QualityScore { get; set; }

    /// <summary>
    /// Issues found in the test scenario
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Suggestions for improvement
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Missing test coverage areas
    /// </summary>
    public List<string> MissingCoverage { get; set; } = new();

    /// <summary>
    /// Recommended additional assertions
    /// </summary>
    public List<string> RecommendedAssertions { get; set; } = new();
}