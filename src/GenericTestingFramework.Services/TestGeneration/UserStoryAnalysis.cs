using System.Text.RegularExpressions;

namespace GenericTestingFramework.Services.TestGeneration.Models;

/// <summary>
/// Comprehensive analysis result of a user story using NLP techniques
/// </summary>
public class UserStoryAnalysis
{
    /// <summary>
    /// Original user story content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Project context information
    /// </summary>
    public string ProjectContext { get; set; } = string.Empty;

    /// <summary>
    /// Keywords extracted and categorized
    /// </summary>
    public AnalysisKeywords Keywords { get; set; } = new();

    /// <summary>
    /// Actions identified in the user story
    /// </summary>
    public List<TestAction> Actions { get; set; } = new();

    /// <summary>
    /// URLs and endpoints found
    /// </summary>
    public List<string> Urls { get; set; } = new();

    /// <summary>
    /// Credentials and authentication data
    /// </summary>
    public Dictionary<string, string> Credentials { get; set; } = new();

    /// <summary>
    /// Form fields and input elements identified
    /// </summary>
    public List<FormField> FormFields { get; set; } = new();

    /// <summary>
    /// Test data requirements
    /// </summary>
    public TestDataRequirements TestData { get; set; } = new();

    /// <summary>
    /// Navigation flow type determined
    /// </summary>
    public NavigationFlowType NavigationFlow { get; set; } = NavigationFlowType.General;

    /// <summary>
    /// UI elements and selectors identified
    /// </summary>
    public List<UIElement> UIElements { get; set; } = new();

    /// <summary>
    /// API endpoints and methods if applicable
    /// </summary>
    public List<ApiEndpoint> ApiEndpoints { get; set; } = new();

    /// <summary>
    /// Business rules and validation requirements
    /// </summary>
    public List<BusinessRule> BusinessRules { get; set; } = new();

    /// <summary>
    /// Confidence score of the analysis (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; } = 0;

    /// <summary>
    /// Complexity assessment
    /// </summary>
    public ComplexityAssessment Complexity { get; set; } = new();

    /// <summary>
    /// Recommended test approach
    /// </summary>
    public TestApproach RecommendedApproach { get; set; } = new();
}

/// <summary>
/// Categorized keywords from analysis
/// </summary>
public class AnalysisKeywords
{
    public List<string> Authentication { get; set; } = new();
    public List<string> Navigation { get; set; } = new();
    public List<string> FormInteraction { get; set; } = new();
    public List<string> Verification { get; set; } = new();
    public List<string> DataEntry { get; set; } = new();
    public List<string> UserInterface { get; set; } = new();
    public List<string> Business { get; set; } = new();
    public List<string> Technical { get; set; } = new();
}

/// <summary>
/// Test action identified in user story
/// </summary>
public class TestAction
{
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ActionType Type { get; set; } = ActionType.UI;
    public int Priority { get; set; } = 1;
    public List<string> Prerequisites { get; set; } = new();
}

/// <summary>
/// Form field identified in analysis
/// </summary>
public class FormField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = false;
    public string DefaultValue { get; set; } = string.Empty;
    public List<string> ValidationRules { get; set; } = new();
    public List<string> PossibleSelectors { get; set; } = new();
}

/// <summary>
/// UI element identified for testing
/// </summary>
public class UIElement
{
    public string ElementType { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public List<string> PossibleSelectors { get; set; } = new();
    public string ExpectedText { get; set; } = string.Empty;
    public bool IsInteractive { get; set; } = false;
}

/// <summary>
/// API endpoint information
/// </summary>
public class ApiEndpoint
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string ExpectedResponse { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
}

/// <summary>
/// Business rule or validation requirement
/// </summary>
public class BusinessRule
{
    public string Rule { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string ExpectedBehavior { get; set; } = string.Empty;
    public RulePriority Priority { get; set; } = RulePriority.Medium;
}

/// <summary>
/// Test data requirements analysis
/// </summary>
public class TestDataRequirements
{
    public List<string> RequiredFields { get; set; } = new();
    public List<string> OptionalFields { get; set; } = new();
    public Dictionary<string, string> TestValues { get; set; } = new();
    public List<string> EdgeCases { get; set; } = new();
    public List<string> InvalidDataScenarios { get; set; } = new();
}

/// <summary>
/// Complexity assessment of the user story
/// </summary>
public class ComplexityAssessment
{
    public int OverallScore { get; set; } = 1; // 1-10 scale
    public int UIComplexity { get; set; } = 1;
    public int BusinessLogicComplexity { get; set; } = 1;
    public int DataComplexity { get; set; } = 1;
    public int IntegrationComplexity { get; set; } = 1;
    public List<string> ComplexityFactors { get; set; } = new();
}

/// <summary>
/// Recommended test approach based on analysis
/// </summary>
public class TestApproach
{
    public TestStrategyType Strategy { get; set; } = TestStrategyType.UI;
    public List<TestType> RecommendedTypes { get; set; } = new();
    public int EstimatedSteps { get; set; } = 1;
    public TimeSpan EstimatedDuration { get; set; } = TimeSpan.FromMinutes(5);
    public List<string> RecommendedTools { get; set; } = new();
    public List<string> RiskFactors { get; set; } = new();
}

/// <summary>
/// Type of navigation flow identified
/// </summary>
public enum NavigationFlowType
{
    General,
    Authentication,
    Registration,
    FormSubmission,
    DataEntry,
    Reporting,
    Configuration,
    Workflow,
    ECommerce,
    Search
}

/// <summary>
/// Type of test action
/// </summary>
public enum ActionType
{
    UI,
    API,
    Database,
    FileSystem,
    Network,
    Business
}

/// <summary>
/// Priority level for business rules
/// </summary>
public enum RulePriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Test strategy type
/// </summary>
public enum TestStrategyType
{
    UI,
    API,
    Mixed,
    Integration,
    EndToEnd
}

/// <summary>
/// Test type enumeration for analysis
/// </summary>
public enum TestType
{
    Functional,
    Regression,
    Smoke,
    Integration,
    Performance,
    Security,
    Usability,
    Accessibility
}