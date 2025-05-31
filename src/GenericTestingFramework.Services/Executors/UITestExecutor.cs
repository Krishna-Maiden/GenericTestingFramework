using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Drawing;

namespace GenericTestingFramework.Services.Executors;

/// <summary>
/// Test executor for UI/Web testing using Selenium WebDriver
/// </summary>
public class UITestExecutor : BaseTestExecutor, ITestExecutor
{
    private readonly ILogger<UITestExecutor> _logger;
    private IWebDriver? _driver;
    private WebDriverWait? _wait;
    private readonly UITestConfiguration _configuration;

    public string Name => "UI Test Executor";

    public UITestExecutor(ILogger<UITestExecutor> logger, UITestConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool CanExecute(TestType testType)
    {
        return testType == TestType.UI || testType == TestType.Mixed;
    }

    public async Task<TestResult> ExecuteTest(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var result = new TestResult
        {
            ScenarioId = scenario.Id,
            Environment = scenario.Environment,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            await Initialize(scenario.Configuration, cancellationToken);

            _logger.LogInformation("Starting UI test execution for scenario {ScenarioId}", scenario.Id);

            // Execute preconditions
            await ExecutePreconditions(scenario.Preconditions, result);

            // Execute main test steps
            foreach (var step in scenario.Steps.Where(s => s.IsEnabled).OrderBy(s => s.Order))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Passed = false;
                    result.Message = "Test execution was cancelled";
                    break;
                }

                var stepResult = await ExecuteUIStep(step, cancellationToken);
                result.AddStepResult(stepResult);

                if (!stepResult.Passed && !step.ContinueOnFailure)
                {
                    _logger.LogWarning("Step {StepOrder} failed, stopping execution", step.Order);
                    break;
                }

                // Wait after step if specified
                if (step.WaitAfter.HasValue)
                {
                    await Task.Delay(step.WaitAfter.Value, cancellationToken);
                }
            }

            result.Complete();
            _logger.LogInformation("UI test execution completed for scenario {ScenarioId}. Passed: {Passed}", 
                scenario.Id, result.Passed);
        }
        catch (OperationCanceledException)
        {
            result.Passed = false;
            result.Message = "Test execution was cancelled";
            _logger.LogInformation("UI test execution was cancelled for scenario {ScenarioId}", scenario.Id);
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.Message = $"Test execution failed: {ex.Message}";
            result.Error = new TestError
            {
                ErrorType = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace
            };
            _logger.LogError(ex, "UI test execution failed for scenario {ScenarioId}", scenario.Id);
        }
        finally
        {
            await Cleanup(cancellationToken);
        }

        return result;
    }

    public async Task<ExecutorValidationResult> ValidateScenario(TestScenario scenario)
    {
        var result = new ExecutorValidationResult { CanExecute = true };

        if (scenario.Type != TestType.UI && scenario.Type != TestType.Mixed)
        {
            result.CanExecute = false;
            result.Messages.Add($"UI executor cannot handle test type: {scenario.Type}");
            return result;
        }

        // Validate steps
        foreach (var step in scenario.Steps)
        {
            if (!IsUIAction(step.Action))
            {
                result.Messages.Add($"Step '{step.Action}' is not a UI action and may be skipped");
                continue;
            }

            var stepValidation = ValidateUIStep(step);
            result.Messages.AddRange(stepValidation);
        }

        return await Task.FromResult(result);
    }

    public ExecutorCapabilities GetCapabilities()
    {
        return new ExecutorCapabilities
        {
            SupportedTestTypes = new List<TestType> { TestType.UI, TestType.Mixed },
            SupportedActions = new List<string>
            {
                "navigate", "click", "double_click", "right_click", "hover",
                "enter_text", "clear_text", "select_option", "select_checkbox",
                "upload_file", "switch_frame", "switch_window", "switch_tab",
                "scroll", "verify_text", "verify_element", "verify_attribute",
                "wait", "take_screenshot", "execute_script", "drag_drop"
            },
            MaxParallelExecutions = _configuration.MaxParallelSessions,
            SupportsScreenshots = true,
            SupportsVideoRecording = false,
            SupportedBrowsers = new List<string> { "chrome", "firefox", "edge", "safari" },
            AdditionalCapabilities = new Dictionary<string, object>
            {
                ["headless_support"] = true,
                ["mobile_emulation"] = true,
                ["cross_browser"] = true,
                ["javascript_execution"] = true
            }
        };
    }

    public async Task<HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();
        var startTime = DateTime.UtcNow;

        try
        {
            // Test WebDriver initialization
            var options = CreateChromeOptions();
            using var driver = new ChromeDriver(options);
            
            // Test basic navigation
            driver.Navigate().GoToUrl("about:blank");
            
            result.IsHealthy = true;
            result.Message = "UI Test Executor is healthy";
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.Message = $"Health check failed: {ex.Message}";
        }

        result.ResponseTime = DateTime.UtcNow - startTime;
        return await Task.FromResult(result);
    }

    public async Task<bool> Initialize(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            var browserType = configuration.GetValueOrDefault("browser", "chrome").ToString()?.ToLowerInvariant() ?? "chrome";
            var headless = configuration.GetValueOrDefault("headless", _configuration.Headless);
            var windowSize = configuration.GetValueOrDefault("windowSize", _configuration.WindowSize).ToString();

            _driver = CreateWebDriver(browserType, headless, windowSize);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(_configuration.DefaultTimeoutSeconds));

            // Set implicit wait
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(_configuration.ImplicitWaitSeconds);

            _logger.LogInformation("UI Test Executor initialized with browser: {Browser}, headless: {Headless}", 
                browserType, headless);

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize UI Test Executor");
            return false;
        }
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        try
        {
            _driver?.Quit();
            _driver?.Dispose();
            _driver = null;
            _wait = null;
            _logger.LogInformation("UI Test Executor cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during UI Test Executor cleanup");
        }

        await Task.CompletedTask;
    }

    private async Task<StepResult> ExecuteUIStep(TestStep step, CancellationToken cancellationToken)
    {
        var stepResult = new StepResult
        {
            StepId = step.Id,
            StepName = step.Description,
            Action = step.Action,
            Target = step.Target,
            ExpectedResult = step.ExpectedResult,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Wait before step if specified
            if (step.WaitBefore.HasValue)
            {
                await Task.Delay(step.WaitBefore.Value, cancellationToken);
            }

            _logger.LogDebug("Executing UI step: {Action} on {Target}", step.Action, step.Target);

            switch (step.Action.ToLowerInvariant())
            {
                case "navigate":
                    await ExecuteNavigate(step, stepResult);
                    break;
                case "click":
                    await ExecuteClick(step, stepResult);
                    break;
                case "double_click":
                    await ExecuteDoubleClick(step, stepResult);
                    break;
                case "right_click":
                    await ExecuteRightClick(step, stepResult);
                    break;
                case "hover":
                    await ExecuteHover(step, stepResult);
                    break;
                case "enter_text":
                case "type":
                    await ExecuteEnterText(step, stepResult);
                    break;
                case "clear_text":
                    await ExecuteClearText(step, stepResult);
                    break;
                case "select_option":
                    await ExecuteSelectOption(step, stepResult);
                    break;
                case "select_checkbox":
                    await ExecuteSelectCheckbox(step, stepResult);
                    break;
                case "upload_file":
                    await ExecuteUploadFile(step, stepResult);
                    break;
                case "switch_frame":
                    await ExecuteSwitchFrame(step, stepResult);
                    break;
                case "switch_window":
                    await ExecuteSwitchWindow(step, stepResult);
                    break;
                case "scroll":
                    await ExecuteScroll(step, stepResult);
                    break;
                case "verify_text":
                    await ExecuteVerifyText(step, stepResult);
                    break;
                case "verify_element":
                    await ExecuteVerifyElement(step, stepResult);
                    break;
                case "verify_attribute":
                    await ExecuteVerifyAttribute(step, stepResult);
                    break;
                case "wait":
                    await ExecuteWait(step, stepResult);
                    break;
                case "take_screenshot":
                    await ExecuteTakeScreenshot(step, stepResult);
                    break;
                case "execute_script":
                    await ExecuteScript(step, stepResult);
                    break;
                case "drag_drop":
                    await ExecuteDragDrop(step, stepResult);
                    break;
                default:
                    stepResult.Passed = false;
                    stepResult.Message = $"Unknown UI action: {step.Action}";
                    break;
            }

            // Take screenshot if requested
            if (step.TakeScreenshot && stepResult.Passed)
            {
                stepResult.ScreenshotPath = await CaptureScreenshot($"step_{step.Order}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            }

            stepResult.Complete(stepResult.Passed, stepResult.Message);
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Step execution failed: {ex.Message}";
            stepResult.Exception = new StepException
            {
                Type = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace
            };
            stepResult.Complete(false, stepResult.Message);
            
            _logger.LogError(ex, "UI step execution failed: {Action} on {Target}", step.Action, step.Target);
        }

        return stepResult;
    }

    private async Task ExecuteNavigate(TestStep step, StepResult stepResult)
    {
        if (_driver == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "WebDriver not initialized";
            return;
        }

        var url = step.Target;
        
        // Handle relative URLs
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            var baseUrl = step.GetParameterValue("baseUrl")?.ToString() ?? _configuration.BaseUrl;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                url = new Uri(new Uri(baseUrl), url).ToString();
            }
        }

        _driver.Navigate().GoToUrl(url);
        
        // Wait for page load
        await WaitForPageLoad();
        
        stepResult.Passed = true;
        stepResult.Message = $"Successfully navigated to {url}";
        stepResult.ActualResult = _driver.Url;
        
        await Task.CompletedTask;
    }

    private async Task ExecuteClick(TestStep step, StepResult stepResult)
    {
        var element = await FindElement(step.Target, step.Timeout);
        if (element == null)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Element not found: {step.Target}";
            return;
        }

        // Wait for element to be clickable
        _wait?.Until(ExpectedConditions.ElementToBeClickable(element));
        
        element.Click();
        
        stepResult.Passed = true;
        stepResult.Message = $"Successfully clicked element: {step.Target}";
        stepResult.ActualResult = "Element clicked";
        
        await Task.CompletedTask;
    }

    private async Task ExecuteEnterText(TestStep step, StepResult stepResult)
    {
        var element = await FindElement(step.Target, step.Timeout);
        if (element == null)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Element not found: {step.Target}";
            return;
        }

        var text = step.GetParameterValue("value")?.ToString() ?? step.GetParameterValue("text")?.ToString() ?? "";
        
        // Clear existing text if specified
        var clearFirst = step.GetParameterValue("clearFirst")?.ToString()?.ToLowerInvariant() == "true";
        if (clearFirst)
        {
            element.Clear();
        }

        element.SendKeys(text);
        
        stepResult.Passed = true;
        stepResult.Message = $"Successfully entered text into element: {step.Target}";
        stepResult.ActualResult = $"Text entered: {text}";
        
        await Task.CompletedTask;
    }

    private async Task ExecuteVerifyText(TestStep step, StepResult stepResult)
    {
        var element = await FindElement(step.Target, step.Timeout);
        if (element == null)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Element not found: {step.Target}";
            return;
        }

        var expectedText = step.GetParameterValue("expected")?.ToString() ?? step.ExpectedResult;
        var actualText = element.Text;
        
        var verificationMode = step.GetParameterValue("mode")?.ToString()?.ToLowerInvariant() ?? "equals";
        
        bool textMatches = verificationMode switch
        {
            "contains" => actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
            "startswith" => actualText.StartsWith(expectedText, StringComparison.OrdinalIgnoreCase),
            "endswith" => actualText.EndsWith(expectedText, StringComparison.OrdinalIgnoreCase),
            "regex" => System.Text.RegularExpressions.Regex.IsMatch(actualText, expectedText),
            _ => string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase)
        };

        stepResult.Passed = textMatches;
        stepResult.Message = textMatches ? 
            $"Text verification passed for element: {step.Target}" : 
            $"Text verification failed. Expected: '{expectedText}', Actual: '{actualText}'";
        stepResult.ActualResult = actualText;
        
        await Task.CompletedTask;
    }

    // Additional UI action implementations would continue here...
    // For brevity, I'm including key methods. The complete implementation would include all UI actions.

    private async Task<IWebElement?> FindElement(string locator, TimeSpan? timeout = null)
    {
        if (_driver == null) return null;

        var wait = timeout.HasValue ? new WebDriverWait(_driver, timeout.Value) : _wait;
        
        try
        {
            By by = locator switch
            {
                var l when l.StartsWith("//") => By.XPath(l),
                var l when l.StartsWith("#") => By.Id(l[1..]),
                var l when l.StartsWith(".") => By.ClassName(l[1..]),
                var l when l.Contains("=") => ParseLocator(l),
                _ => By.CssSelector(locator)
            };

            return await Task.FromResult(wait?.Until(ExpectedConditions.ElementIsVisible(by)));
        }
        catch (WebDriverTimeoutException)
        {
            _logger.LogWarning("Element not found within timeout: {Locator}", locator);
            return null;
        }
    }

    private By ParseLocator(string locator)
    {
        var parts = locator.Split('=', 2);
        var strategy = parts[0].ToLowerInvariant();
        var value = parts[1];

        return strategy switch
        {
            "id" => By.Id(value),
            "name" => By.Name(value),
            "class" => By.ClassName(value),
            "tag" => By.TagName(value),
            "css" => By.CssSelector(value),
            "xpath" => By.XPath(value),
            "linktext" => By.LinkText(value),
            "partiallinktext" => By.PartialLinkText(value),
            _ => By.CssSelector(locator)
        };
    }

    private IWebDriver CreateWebDriver(string browserType, object headless, string windowSize)
    {
        var isHeadless = headless?.ToString()?.ToLowerInvariant() == "true";

        return browserType switch
        {
            "chrome" => CreateChromeDriver(isHeadless, windowSize),
            "firefox" => CreateFirefoxDriver(isHeadless, windowSize),
            "edge" => CreateEdgeDriver(isHeadless, windowSize),
            _ => CreateChromeDriver(isHeadless, windowSize) // Default to Chrome
        };
    }

    private ChromeDriver CreateChromeDriver(bool headless, string windowSize)
    {
        var options = CreateChromeOptions();
        
        if (headless)
        {
            options.AddArgument("--headless");
        }

        if (!string.IsNullOrEmpty(windowSize))
        {
            options.AddArgument($"--window-size={windowSize}");
        }

        return new ChromeDriver(options);
    }

    private ChromeOptions CreateChromeOptions()
    {
        var options = new ChromeOptions();
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-web-security");
        options.AddArgument("--allow-running-insecure-content");
        options.AddArgument("--ignore-certificate-errors");
        return options;
    }

    private FirefoxDriver CreateFirefoxDriver(bool headless, string windowSize)
    {
        var options = new FirefoxOptions();
        
        if (headless)
        {
            options.AddArgument("--headless");
        }

        return new FirefoxDriver(options);
    }

    private EdgeDriver CreateEdgeDriver(bool headless, string windowSize)
    {
        var options = new EdgeOptions();
        
        if (headless)
        {
            options.AddArgument("--headless");
        }

        return new EdgeDriver(options);
    }

    private async Task<string> CaptureScreenshot(string fileName)
    {
        if (_driver is not ITakesScreenshot screenshotDriver)
            return string.Empty;

        try
        {
            var screenshot = screenshotDriver.GetScreenshot();
            var filePath = Path.Combine(_configuration.ScreenshotPath, $"{fileName}.png");
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            screenshot.SaveAsFile(filePath);
            
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot");
            return string.Empty;
        }
    }

    private async Task WaitForPageLoad()
    {
        if (_driver == null) return;

        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
            await Task.Run(() => wait.Until(driver => 
                ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Page load wait failed");
        }
    }

    // Additional helper methods would be implemented here...
}

/// <summary>
/// Configuration for UI test execution
/// </summary>
public class UITestConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public bool Headless { get; set; } = false;
    public string WindowSize { get; set; } = "1920,1080";
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int ImplicitWaitSeconds { get; set; } = 10;
    public int MaxParallelSessions { get; set; } = 3;
    public string ScreenshotPath { get; set; } = "screenshots";
    public bool CaptureScreenshotOnFailure { get; set; } = true;
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}