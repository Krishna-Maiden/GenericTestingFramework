using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using SeleniumExtras.WaitHelpers;
using System.Drawing;

namespace GenericTestingFramework.Services.Executors;

/// <summary>
/// Test executor for UI/Web testing using Selenium WebDriver with enhanced screenshot capabilities
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

                // ENHANCED: Always take screenshot on failure
                if (!stepResult.Passed)
                {
                    _logger.LogWarning("Step {StepOrder} failed: {StepMessage}", step.Order, stepResult.Message);

                    // Capture failure screenshot
                    var failureScreenshot = await CaptureScreenshot($"failed_step_{step.Order}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                    if (!string.IsNullOrEmpty(failureScreenshot))
                    {
                        stepResult.ScreenshotPath = failureScreenshot;
                        result.Screenshots.Add(failureScreenshot);
                        _logger.LogInformation("Failure screenshot captured: {ScreenshotPath}", failureScreenshot);
                    }

                    if (!step.ContinueOnFailure)
                    {
                        _logger.LogWarning("Step {StepOrder} failed, stopping execution", step.Order);
                        break;
                    }
                }

                // Take screenshot if requested or configured
                if (step.TakeScreenshot || _configuration.CaptureScreenshotOnFailure)
                {
                    var screenshot = await CaptureScreenshot($"step_{step.Order}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                    if (!string.IsNullOrEmpty(screenshot) && string.IsNullOrEmpty(stepResult.ScreenshotPath))
                    {
                        stepResult.ScreenshotPath = screenshot;
                        result.Screenshots.Add(screenshot);
                    }
                }

                // Wait after step if specified
                if (step.WaitAfter.HasValue)
                {
                    await Task.Delay(step.WaitAfter.Value, cancellationToken);
                }
            }

            result.Complete();

            // Take final screenshot if test failed
            if (!result.Passed)
            {
                var finalScreenshot = await CaptureScreenshot($"final_failure_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                if (!string.IsNullOrEmpty(finalScreenshot))
                {
                    result.Screenshots.Add(finalScreenshot);
                    _logger.LogInformation("Final failure screenshot captured: {ScreenshotPath}", finalScreenshot);
                }
            }

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

            // Capture exception screenshot
            var exceptionScreenshot = await CaptureScreenshot($"exception_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            if (!string.IsNullOrEmpty(exceptionScreenshot))
            {
                result.Screenshots.Add(exceptionScreenshot);
                _logger.LogInformation("Exception screenshot captured: {ScreenshotPath}", exceptionScreenshot);
            }

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

            // Ensure screenshot directory exists
            if (!Directory.Exists(_configuration.ScreenshotPath))
            {
                Directory.CreateDirectory(_configuration.ScreenshotPath);
                _logger.LogInformation("Created screenshot directory: {Path}", _configuration.ScreenshotPath);
            }

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

    #region UI Action Implementations (keeping existing implementations but adding better error handling)

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

        try
        {
            _driver.Navigate().GoToUrl(url);

            // Wait for page load
            await WaitForPageLoad();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully navigated to {url}";
            stepResult.ActualResult = _driver.Url;
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Navigation failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteClick(TestStep step, StepResult stepResult)
    {
        try
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

            // Scroll element into view
            var jsExecutor = (IJavaScriptExecutor)_driver!;
            jsExecutor.ExecuteScript("arguments[0].scrollIntoView(true);", element);

            // Small delay to ensure element is visible
            await Task.Delay(500);

            element.Click();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully clicked element: {step.Target}";
            stepResult.ActualResult = "Element clicked";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Click failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteEnterText(TestStep step, StepResult stepResult)
    {
        try
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

            // Scroll element into view
            var jsExecutor = (IJavaScriptExecutor)_driver!;
            jsExecutor.ExecuteScript("arguments[0].scrollIntoView(true);", element);

            // Focus on element
            element.Click();
            await Task.Delay(200);

            element.SendKeys(text);

            stepResult.Passed = true;
            stepResult.Message = $"Successfully entered text into element: {step.Target}";
            stepResult.ActualResult = $"Text entered: {text}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Text entry failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteVerifyElement(TestStep step, StepResult stepResult)
    {
        try
        {
            var verificationMode = step.GetParameterValue("mode")?.ToString()?.ToLowerInvariant() ?? "visible";

            var element = await FindElement(step.Target, step.Timeout);

            bool verificationPassed = verificationMode switch
            {
                "exists" => element != null,
                "visible" => element != null && element.Displayed,
                "enabled" => element != null && element.Enabled,
                "selected" => element != null && element.Selected,
                "not_exists" => element == null,
                "not_visible" => element == null || !element.Displayed,
                "not_enabled" => element == null || !element.Enabled,
                "not_selected" => element == null || !element.Selected,
                _ => element != null && element.Displayed
            };

            stepResult.Passed = verificationPassed;
            stepResult.Message = verificationPassed ?
                $"Element verification passed: {verificationMode} for {step.Target}" :
                $"Element verification failed: {verificationMode} for {step.Target}";
            stepResult.ActualResult = element != null ?
                $"Element found, Displayed: {element.Displayed}, Enabled: {element.Enabled}" :
                "Element not found";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Element verification failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteDoubleClick(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var actions = new Actions(_driver);
            actions.DoubleClick(element).Perform();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully double-clicked element: {step.Target}";
            stepResult.ActualResult = "Element double-clicked";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Double-click failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteRightClick(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var actions = new Actions(_driver);
            actions.ContextClick(element).Perform();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully right-clicked element: {step.Target}";
            stepResult.ActualResult = "Element right-clicked";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Right-click failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteHover(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var actions = new Actions(_driver);
            actions.MoveToElement(element).Perform();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully hovered over element: {step.Target}";
            stepResult.ActualResult = "Element hovered";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Hover failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteClearText(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            element.Clear();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully cleared text from element: {step.Target}";
            stepResult.ActualResult = "Text cleared";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Clear text failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteSelectOption(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var optionValue = step.GetParameterValue("option")?.ToString() ?? step.GetParameterValue("value")?.ToString() ?? "";
            var selectMethod = step.GetParameterValue("method")?.ToString()?.ToLowerInvariant() ?? "text";

            var select = new SelectElement(element);

            switch (selectMethod)
            {
                case "value":
                    select.SelectByValue(optionValue);
                    break;
                case "index":
                    if (int.TryParse(optionValue, out var index))
                        select.SelectByIndex(index);
                    else
                        throw new ArgumentException($"Invalid index value: {optionValue}");
                    break;
                default: // text
                    select.SelectByText(optionValue);
                    break;
            }

            stepResult.Passed = true;
            stepResult.Message = $"Successfully selected option '{optionValue}' in element: {step.Target}";
            stepResult.ActualResult = $"Option selected: {optionValue}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Select option failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteSelectCheckbox(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var shouldCheck = step.GetParameterValue("checked")?.ToString()?.ToLowerInvariant() != "false";
            var isCurrentlyChecked = element.Selected;

            if (shouldCheck && !isCurrentlyChecked)
            {
                element.Click();
            }
            else if (!shouldCheck && isCurrentlyChecked)
            {
                element.Click();
            }

            stepResult.Passed = true;
            stepResult.Message = $"Successfully {(shouldCheck ? "checked" : "unchecked")} checkbox: {step.Target}";
            stepResult.ActualResult = $"Checkbox {(shouldCheck ? "checked" : "unchecked")}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Checkbox operation failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteUploadFile(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var filePath = step.GetParameterValue("filePath")?.ToString() ?? step.GetParameterValue("file")?.ToString() ?? "";

            if (!File.Exists(filePath))
            {
                stepResult.Passed = false;
                stepResult.Message = $"File not found: {filePath}";
                return;
            }

            element.SendKeys(filePath);

            stepResult.Passed = true;
            stepResult.Message = $"Successfully uploaded file: {filePath}";
            stepResult.ActualResult = $"File uploaded: {filePath}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"File upload failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteSwitchFrame(TestStep step, StepResult stepResult)
    {
        if (_driver == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "WebDriver not initialized";
            return;
        }

        var frameIdentifier = step.Target;

        try
        {
            if (int.TryParse(frameIdentifier, out var frameIndex))
            {
                _driver.SwitchTo().Frame(frameIndex);
            }
            else if (frameIdentifier.ToLowerInvariant() == "default")
            {
                _driver.SwitchTo().DefaultContent();
            }
            else
            {
                var frameElement = await FindElement(frameIdentifier, step.Timeout);
                if (frameElement != null)
                {
                    _driver.SwitchTo().Frame(frameElement);
                }
                else
                {
                    _driver.SwitchTo().Frame(frameIdentifier);
                }
            }

            stepResult.Passed = true;
            stepResult.Message = $"Successfully switched to frame: {frameIdentifier}";
            stepResult.ActualResult = $"Frame switched: {frameIdentifier}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Failed to switch to frame: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteSwitchWindow(TestStep step, StepResult stepResult)
    {
        if (_driver == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "WebDriver not initialized";
            return;
        }

        var windowIdentifier = step.Target;

        try
        {
            if (int.TryParse(windowIdentifier, out var windowIndex))
            {
                var handles = _driver.WindowHandles;
                if (windowIndex < handles.Count)
                {
                    _driver.SwitchTo().Window(handles[windowIndex]);
                }
                else
                {
                    throw new ArgumentException($"Window index {windowIndex} out of range");
                }
            }
            else if (windowIdentifier.ToLowerInvariant() == "new" || windowIdentifier.ToLowerInvariant() == "latest")
            {
                var handles = _driver.WindowHandles;
                _driver.SwitchTo().Window(handles.Last());
            }
            else
            {
                _driver.SwitchTo().Window(windowIdentifier);
            }

            stepResult.Passed = true;
            stepResult.Message = $"Successfully switched to window: {windowIdentifier}";
            stepResult.ActualResult = $"Window switched: {windowIdentifier}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Failed to switch to window: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteScroll(TestStep step, StepResult stepResult)
    {
        if (_driver == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "WebDriver not initialized";
            return;
        }

        try
        {
            var scrollDirection = step.GetParameterValue("direction")?.ToString()?.ToLowerInvariant() ?? "down";
            var scrollAmount = step.GetParameterValue("amount")?.ToString() ?? "500";

            var jsExecutor = (IJavaScriptExecutor)_driver;

            var script = scrollDirection switch
            {
                "up" => $"window.scrollBy(0, -{scrollAmount});",
                "down" => $"window.scrollBy(0, {scrollAmount});",
                "left" => $"window.scrollBy(-{scrollAmount}, 0);",
                "right" => $"window.scrollBy({scrollAmount}, 0);",
                "top" => "window.scrollTo(0, 0);",
                "bottom" => "window.scrollTo(0, document.body.scrollHeight);",
                _ => $"window.scrollBy(0, {scrollAmount});"
            };

            // If target is specified, scroll to element
            if (!string.IsNullOrEmpty(step.Target) && step.Target != "page")
            {
                var element = await FindElement(step.Target, step.Timeout);
                if (element != null)
                {
                    jsExecutor.ExecuteScript("arguments[0].scrollIntoView(true);", element);
                }
            }
            else
            {
                jsExecutor.ExecuteScript(script);
            }

            stepResult.Passed = true;
            stepResult.Message = $"Successfully scrolled {scrollDirection}";
            stepResult.ActualResult = $"Scrolled {scrollDirection} by {scrollAmount}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Scroll failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteVerifyText(TestStep step, StepResult stepResult)
    {
        try
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
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Text verification failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteVerifyAttribute(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElement(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found: {step.Target}";
                return;
            }

            var attributeName = step.GetParameterValue("attribute")?.ToString() ?? step.GetParameterValue("name")?.ToString() ?? "";
            var expectedValue = step.GetParameterValue("expected")?.ToString() ?? step.ExpectedResult;

            if (string.IsNullOrEmpty(attributeName))
            {
                stepResult.Passed = false;
                stepResult.Message = "Attribute name is required for attribute verification";
                return;
            }

            var actualValue = element.GetAttribute(attributeName) ?? "";
            var verificationMode = step.GetParameterValue("mode")?.ToString()?.ToLowerInvariant() ?? "equals";

            bool attributeMatches = verificationMode switch
            {
                "contains" => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
                "startswith" => actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
                "endswith" => actualValue.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
                "exists" => !string.IsNullOrEmpty(actualValue),
                "not_exists" => string.IsNullOrEmpty(actualValue),
                _ => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase)
            };

            stepResult.Passed = attributeMatches;
            stepResult.Message = attributeMatches ?
                $"Attribute verification passed for '{attributeName}' on {step.Target}" :
                $"Attribute verification failed for '{attributeName}'. Expected: '{expectedValue}', Actual: '{actualValue}'";
            stepResult.ActualResult = actualValue;
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Attribute verification failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteWait(TestStep step, StepResult stepResult)
    {
        try
        {
            var waitType = step.GetParameterValue("type")?.ToString()?.ToLowerInvariant() ?? "duration";
            var duration = step.GetParameterValue("duration")?.ToString() ?? "1000";

            switch (waitType)
            {
                case "duration":
                    if (int.TryParse(duration, out var milliseconds))
                    {
                        await Task.Delay(milliseconds);
                        stepResult.Passed = true;
                        stepResult.Message = $"Successfully waited for {milliseconds}ms";
                    }
                    else
                    {
                        stepResult.Passed = false;
                        stepResult.Message = $"Invalid duration: {duration}";
                    }
                    break;

                case "element":
                    var element = await FindElement(step.Target, step.Timeout ?? TimeSpan.FromSeconds(30));
                    stepResult.Passed = element != null;
                    stepResult.Message = stepResult.Passed ?
                        $"Element appeared: {step.Target}" :
                        $"Element did not appear within timeout: {step.Target}";
                    break;

                case "page_load":
                    await WaitForPageLoad();
                    stepResult.Passed = true;
                    stepResult.Message = "Page load completed";
                    break;

                default:
                    stepResult.Passed = false;
                    stepResult.Message = $"Unknown wait type: {waitType}";
                    break;
            }

            stepResult.ActualResult = $"Wait type: {waitType}, Duration: {duration}";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Wait failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteTakeScreenshot(TestStep step, StepResult stepResult)
    {
        try
        {
            var fileName = step.GetParameterValue("fileName")?.ToString() ??
                          $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            var screenshotPath = await CaptureScreenshot(fileName);

            if (!string.IsNullOrEmpty(screenshotPath))
            {
                stepResult.Passed = true;
                stepResult.Message = $"Screenshot captured: {screenshotPath}";
                stepResult.ScreenshotPath = screenshotPath;
                stepResult.ActualResult = screenshotPath;
            }
            else
            {
                stepResult.Passed = false;
                stepResult.Message = "Failed to capture screenshot";
            }
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Screenshot failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteScript(TestStep step, StepResult stepResult)
    {
        if (_driver == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "WebDriver not initialized";
            return;
        }

        try
        {
            var script = step.GetParameterValue("script")?.ToString() ?? step.Target;
            var arguments = step.GetParameterValue("arguments")?.ToString();

            var jsExecutor = (IJavaScriptExecutor)_driver;
            object result;

            if (!string.IsNullOrEmpty(arguments))
            {
                // Parse arguments if provided (simple comma-separated values)
                var args = arguments.Split(',').Select(arg => arg.Trim()).ToArray();
                result = jsExecutor.ExecuteScript(script, args);
            }
            else
            {
                result = jsExecutor.ExecuteScript(script);
            }

            stepResult.Passed = true;
            stepResult.Message = "JavaScript executed successfully";
            stepResult.ActualResult = result?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"JavaScript execution failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteDragDrop(TestStep step, StepResult stepResult)
    {
        try
        {
            var sourceSelector = step.Target;
            var targetSelector = step.GetParameterValue("target")?.ToString() ??
                                step.GetParameterValue("destination")?.ToString() ?? "";

            if (string.IsNullOrEmpty(targetSelector))
            {
                stepResult.Passed = false;
                stepResult.Message = "Target/destination parameter is required for drag and drop";
                return;
            }

            var sourceElement = await FindElement(sourceSelector, step.Timeout);
            var targetElement = await FindElement(targetSelector, step.Timeout);

            if (sourceElement == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Source element not found: {sourceSelector}";
                return;
            }

            if (targetElement == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Target element not found: {targetSelector}";
                return;
            }

            var actions = new Actions(_driver);
            actions.DragAndDrop(sourceElement, targetElement).Perform();

            stepResult.Passed = true;
            stepResult.Message = $"Successfully dragged from {sourceSelector} to {targetSelector}";
            stepResult.ActualResult = $"Drag and drop completed";
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Drag and drop failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Helper Methods

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding element: {Locator}", locator);
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
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
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
        {
            _logger.LogWarning("WebDriver does not support screenshots");
            return string.Empty;
        }

        try
        {
            // Ensure screenshot directory exists
            if (!Directory.Exists(_configuration.ScreenshotPath))
            {
                Directory.CreateDirectory(_configuration.ScreenshotPath);
            }

            var screenshot = screenshotDriver.GetScreenshot();
            var filePath = Path.Combine(_configuration.ScreenshotPath, $"{fileName}.png");

            screenshot.SaveAsFile(filePath);

            _logger.LogInformation("Screenshot captured: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot: {FileName}", fileName);
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

    #endregion
}

/// <summary>
/// Enhanced configuration for UI test execution with screenshot settings
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