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
using System.Text.Json;

namespace GenericTestingFramework.Services.Executors;

/// <summary>
/// Enhanced UI Test executor with smart selector finding and better error handling
/// </summary>
public class EnhancedUITestExecutor : BaseTestExecutor, ITestExecutor
{
    private readonly ILogger<EnhancedUITestExecutor> _logger;
    private IWebDriver? _driver;
    private WebDriverWait? _wait;
    private readonly UITestConfiguration _configuration;

    public string Name => "Enhanced UI Test Executor";

    public EnhancedUITestExecutor(ILogger<EnhancedUITestExecutor> logger, UITestConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool CanExecute(TestType testType)
    {
        return testType == TestType.UI || testType == TestType.Mixed;
    }

    // Add this method to your EnhancedUITestExecutor.cs

    /// <summary>
    /// Smart authentication verification that actually checks for login success/failure
    /// </summary>
    private async Task ExecuteVerifyAuthentication(TestStep step, StepResult stepResult)
    {
        try
        {
            if (_driver == null)
            {
                stepResult.Passed = false;
                stepResult.Message = "WebDriver not initialized";
                return;
            }

            _logger.LogDebug("🔍 Performing smart authentication verification");

            var currentUrl = _driver.Url;
            var pageSource = _driver.PageSource?.ToLowerInvariant() ?? "";
            var pageTitle = _driver.Title?.ToLowerInvariant() ?? "";

            _logger.LogDebug("Current URL: {Url}", currentUrl);
            _logger.LogDebug("Page Title: {Title}", _driver.Title);

            // Check for authentication failure indicators first
            var failureIndicators = new[]
            {
            "invalid", "incorrect", "wrong", "error", "failed", "denied",
            "unauthorized", "forbidden", "bad credentials", "login failed",
            "authentication failed", "access denied", "invalid username",
            "invalid password", "wrong password", "user not found",
            "login error", "signin error", "authentication error"
        };

            bool hasFailureIndicators = failureIndicators.Any(indicator =>
                pageSource.Contains(indicator) || pageTitle.Contains(indicator));

            if (hasFailureIndicators)
            {
                stepResult.Passed = false;
                stepResult.Message = "Authentication failed - error messages detected on page";
                stepResult.ActualResult = "Found authentication failure indicators";
                _logger.LogWarning("❌ Authentication failure detected on page");
                return;
            }

            // Check if still on login page (authentication failed)
            bool stillOnLoginPage = currentUrl.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                                   currentUrl.Contains("signin", StringComparison.OrdinalIgnoreCase) ||
                                   pageSource.Contains("login") && pageSource.Contains("password") ||
                                   pageTitle.Contains("login") || pageTitle.Contains("sign in");

            if (stillOnLoginPage)
            {
                // Double-check by looking for login form elements
                try
                {
                    var loginElements = await Task.FromResult(_driver.FindElements(By.CssSelector(
                        "input[type='password'], input[name*='password'], .login-form, .signin-form, #loginForm")));

                    if (loginElements.Any(e => e.Displayed))
                    {
                        stepResult.Passed = false;
                        stepResult.Message = "Authentication failed - still on login page with login form visible";
                        stepResult.ActualResult = $"URL: {currentUrl}, Login elements found: {loginElements.Count}";
                        _logger.LogWarning("❌ Still on login page - authentication likely failed");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error checking for login elements: {Error}", ex.Message);
                }
            }

            // Check for authentication success indicators
            var successIndicators = new[]
            {
            "dashboard", "welcome", "admin", "profile", "account", "logout", "sign out",
            "home", "main", "portal", "panel", "workspace", "console"
        };

            bool hasSuccessIndicators = successIndicators.Any(indicator =>
                pageSource.Contains(indicator) || pageTitle.Contains(indicator) || currentUrl.Contains(indicator, StringComparison.OrdinalIgnoreCase));

            // Check for common post-login elements
            var successElements = new List<IWebElement>();
            var successSelectors = new[]
            {
            "a[href*='logout'], a[href*='signout'], .logout, .sign-out, #logout",
            ".user-menu, .profile-menu, .account-menu, .user-info",
            ".dashboard, #dashboard, .admin-panel, .main-content",
            "nav, .navbar, .navigation, .menu-bar",
            ".welcome, .greeting, .user-welcome"
        };

            foreach (var selector in successSelectors)
            {
                try
                {
                    var elements = _driver.FindElements(By.CssSelector(selector));
                    successElements.AddRange(elements.Where(e => e.Displayed));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error finding success elements with selector '{Selector}': {Error}", selector, ex.Message);
                }
            }

            // URL-based success check (not on login page)
            bool urlIndicatesSuccess = !currentUrl.Contains("login", StringComparison.OrdinalIgnoreCase) &&
                                      !currentUrl.Contains("signin", StringComparison.OrdinalIgnoreCase) &&
                                      !currentUrl.Contains("auth", StringComparison.OrdinalIgnoreCase);

            // Determine authentication success
            bool authenticationSucceeded = (hasSuccessIndicators || successElements.Any() || urlIndicatesSuccess) &&
                                          !hasFailureIndicators &&
                                          !stillOnLoginPage;

            if (authenticationSucceeded)
            {
                stepResult.Passed = true;
                stepResult.Message = "Authentication successful - indicators found";
                stepResult.ActualResult = $"URL: {currentUrl}, Success elements: {successElements.Count}, Success indicators: {hasSuccessIndicators}";
                _logger.LogInformation("✅ Authentication verification passed");
            }
            else
            {
                stepResult.Passed = false;
                stepResult.Message = "Authentication verification failed - no clear success indicators found";
                stepResult.ActualResult = $"URL: {currentUrl}, Title: {_driver.Title}, Success elements: {successElements.Count}";
                _logger.LogWarning("❌ Authentication verification failed - unclear if login succeeded");
            }

            // Log detailed analysis for debugging
            _logger.LogDebug("Authentication Analysis:");
            _logger.LogDebug("  URL changed from login: {UrlChanged}", urlIndicatesSuccess);
            _logger.LogDebug("  Success indicators in content: {HasSuccessIndicators}", hasSuccessIndicators);
            _logger.LogDebug("  Success elements found: {SuccessElements}", successElements.Count);
            _logger.LogDebug("  Failure indicators: {HasFailureIndicators}", hasFailureIndicators);
            _logger.LogDebug("  Still on login page: {StillOnLogin}", stillOnLoginPage);

        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Authentication verification failed with error: {ex.Message}";
            _logger.LogError(ex, "Error during authentication verification");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Verify that authentication actually failed (for negative test cases)
    /// </summary>
    private async Task ExecuteVerifyAuthenticationFailure(TestStep step, StepResult stepResult)
    {
        try
        {
            if (_driver == null)
            {
                stepResult.Passed = false;
                stepResult.Message = "WebDriver not initialized";
                return;
            }

            _logger.LogDebug("🔍 Verifying authentication failure");

            var currentUrl = _driver.Url;
            var pageSource = _driver.PageSource?.ToLowerInvariant() ?? "";
            var pageTitle = _driver.Title?.ToLowerInvariant() ?? "";

            // Check for failure indicators
            var failureIndicators = new[]
            {
            "invalid", "incorrect", "wrong", "error", "failed", "denied",
            "unauthorized", "forbidden", "bad credentials", "login failed",
            "authentication failed", "invalid username", "invalid password"
        };

            bool hasFailureIndicators = failureIndicators.Any(indicator =>
                pageSource.Contains(indicator) || pageTitle.Contains(indicator));

            // Check if still on login page
            bool stillOnLoginPage = currentUrl.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                                   currentUrl.Contains("signin", StringComparison.OrdinalIgnoreCase) ||
                                   pageSource.Contains("login") && pageSource.Contains("password");

            // For negative testing, we WANT authentication to fail
            bool authenticationProperlyFailed = hasFailureIndicators || stillOnLoginPage;

            stepResult.Passed = authenticationProperlyFailed;
            stepResult.Message = authenticationProperlyFailed ?
                "Authentication properly failed as expected" :
                "Authentication did not fail as expected";
            stepResult.ActualResult = $"URL: {currentUrl}, Failure indicators: {hasFailureIndicators}, On login page: {stillOnLoginPage}";

        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Authentication failure verification error: {ex.Message}";
            _logger.LogError(ex, "Error during authentication failure verification");
        }

        await Task.CompletedTask;
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

            _logger.LogInformation("🚀 Starting enhanced UI test execution for scenario {ScenarioId}", scenario.Id);

            // Execute preconditions
            await ExecutePreconditions(scenario.Preconditions, result);

            // Execute main test steps with enhanced logging
            foreach (var step in scenario.Steps.Where(s => s.IsEnabled).OrderBy(s => s.Order))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Passed = false;
                    result.Message = "Test execution was cancelled";
                    break;
                }

                _logger.LogInformation("🔄 Executing step {Order}: {Description}", step.Order, step.Description);

                var stepResult = await ExecuteUIStepEnhanced(step, cancellationToken);
                result.AddStepResult(stepResult);

                // Enhanced failure handling with screenshots
                if (!stepResult.Passed)
                {
                    _logger.LogWarning("❌ Step {StepOrder} failed: {StepMessage}", step.Order, stepResult.Message);

                    // Capture failure screenshot with enhanced naming
                    var failureScreenshot = await CaptureScreenshot($"failed_step_{step.Order}_{step.Action}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                    if (!string.IsNullOrEmpty(failureScreenshot))
                    {
                        stepResult.ScreenshotPath = failureScreenshot;
                        result.Screenshots.Add(failureScreenshot);
                        _logger.LogInformation("📸 Failure screenshot captured: {ScreenshotPath}", failureScreenshot);
                    }

                    if (!step.ContinueOnFailure)
                    {
                        _logger.LogWarning("⏹️ Step {StepOrder} failed, stopping execution (ContinueOnFailure=false)", step.Order);
                        break;
                    }
                }
                else
                {
                    _logger.LogInformation("✅ Step {StepOrder} completed successfully", step.Order);
                }

                // Take screenshot if requested
                if (step.TakeScreenshot)
                {
                    var screenshot = await CaptureScreenshot($"step_{step.Order}_{step.Action}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                    if (!string.IsNullOrEmpty(screenshot) && string.IsNullOrEmpty(stepResult.ScreenshotPath))
                    {
                        stepResult.ScreenshotPath = screenshot;
                        result.Screenshots.Add(screenshot);
                    }
                }

                // Wait after step if specified
                if (step.WaitAfter.HasValue)
                {
                    _logger.LogDebug("⏳ Waiting {Ms}ms after step completion", step.WaitAfter.Value.TotalMilliseconds);
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
                    _logger.LogInformation("📸 Final failure screenshot captured: {ScreenshotPath}", finalScreenshot);
                }
            }

            _logger.LogInformation("🏁 Enhanced UI test execution completed for scenario {ScenarioId}. Status: {Status}",
                scenario.Id, result.Passed ? "PASSED" : "FAILED");
        }
        catch (OperationCanceledException)
        {
            result.Passed = false;
            result.Message = "Test execution was cancelled";
            _logger.LogInformation("⏹️ Enhanced UI test execution was cancelled for scenario {ScenarioId}", scenario.Id);
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
                _logger.LogInformation("📸 Exception screenshot captured: {ScreenshotPath}", exceptionScreenshot);
            }

            _logger.LogError(ex, "💥 Enhanced UI test execution failed for scenario {ScenarioId}", scenario.Id);
        }
        finally
        {
            await Cleanup(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Enhanced element finding with multiple selector attempts and detailed logging
    /// </summary>
    private async Task<IWebElement?> FindElementEnhanced(string locatorString, TimeSpan? timeout = null)
    {
        if (_driver == null) return null;

        var wait = timeout.HasValue ? new WebDriverWait(_driver, timeout.Value) : _wait;

        // Split the locator string by comma to try multiple selectors
        var selectors = locatorString.Split(',').Select(s => s.Trim()).ToArray();

        _logger.LogDebug("Attempting to find element with {SelectorCount} selector(s)", selectors.Length);

        foreach (var selector in selectors)
        {
            try
            {
                _logger.LogDebug("Trying selector: {Selector}", selector);

                By by = selector switch
                {
                    var s when s.StartsWith("//") => By.XPath(s),
                    var s when s.StartsWith("#") => By.Id(s[1..]),
                    var s when s.StartsWith(".") => By.ClassName(s[1..]),
                    var s when s.Contains("=") => ParseLocator(s),
                    _ => By.CssSelector(selector)
                };

                var element = await Task.FromResult(wait?.Until(driver =>
                {
                    try
                    {
                        var el = driver.FindElement(by);
                        return el?.Displayed == true ? el : null;
                    }
                    catch
                    {
                        return null;
                    }
                }));

                if (element != null)
                {
                    _logger.LogDebug("✅ Successfully found element with selector: {Selector}", selector);
                    return element;
                }
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogDebug("⏰ Timeout waiting for element with selector: {Selector}", selector);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("❌ Failed to find element with selector '{Selector}': {Error}", selector, ex.Message);
            }
        }

        // If all selectors failed, try to provide helpful debugging information
        await LogPageDebuggingInfo(locatorString);

        _logger.LogWarning("❌ Element not found with any of the {Count} selectors: {Selectors}",
            selectors.Length, string.Join(" | ", selectors));

        return null;
    }

    /// <summary>
    /// Log debugging information about the current page to help with selector issues
    /// </summary>
    private async Task LogPageDebuggingInfo(string originalSelector)
    {
        if (_driver == null) return;

        try
        {
            var jsExecutor = (IJavaScriptExecutor)_driver;

            // Get page title and URL
            _logger.LogDebug("🔍 Page Debug Info:");
            _logger.LogDebug("   URL: {Url}", _driver.Url);
            _logger.LogDebug("   Title: {Title}", _driver.Title);

            // Check for common form elements
            var inputElements = await Task.FromResult(jsExecutor.ExecuteScript(@"
                var inputs = document.querySelectorAll('input');
                var result = [];
                for (var i = 0; i < Math.min(inputs.length, 10); i++) {
                    var input = inputs[i];
                    result.push({
                        tag: input.tagName,
                        type: input.type || 'text',
                        id: input.id || '',
                        name: input.name || '',
                        className: input.className || '',
                        placeholder: input.placeholder || '',
                        value: input.value || ''
                    });
                }
                return result;
            "));

            if (inputElements != null)
            {
                _logger.LogDebug("📝 Found input elements on page:");
                var inputs = JsonSerializer.Deserialize<JsonElement[]>(JsonSerializer.Serialize(inputElements));
                foreach (var input in inputs?.Take(5) ?? Array.Empty<JsonElement>())
                {
                    var type = input.TryGetProperty("type", out var t) ? t.GetString() : "text";
                    var id = input.TryGetProperty("id", out var i) ? i.GetString() : "";
                    var name = input.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var className = input.TryGetProperty("className", out var c) ? c.GetString() : "";
                    var placeholder = input.TryGetProperty("placeholder", out var p) ? p.GetString() : "";

                    _logger.LogDebug("   - Type: {Type}, ID: '{Id}', Name: '{Name}', Class: '{Class}', Placeholder: '{Placeholder}'",
                        type, id, name, className, placeholder);
                }
            }

            // Check for buttons
            var buttonElements = await Task.FromResult(jsExecutor.ExecuteScript(@"
                var buttons = document.querySelectorAll('button, input[type=""submit""], input[type=""button""]');
                var result = [];
                for (var i = 0; i < Math.min(buttons.length, 5); i++) {
                    var button = buttons[i];
                    result.push({
                        tag: button.tagName,
                        type: button.type || '',
                        id: button.id || '',
                        className: button.className || '',
                        text: button.textContent || button.value || ''
                    });
                }
                return result;
            "));

            if (buttonElements != null)
            {
                _logger.LogDebug("🔘 Found button elements on page:");
                var buttons = JsonSerializer.Deserialize<JsonElement[]>(JsonSerializer.Serialize(buttonElements));
                foreach (var button in buttons?.Take(3) ?? Array.Empty<JsonElement>())
                {
                    var tag = button.TryGetProperty("tag", out var t) ? t.GetString() : "";
                    var id = button.TryGetProperty("id", out var i) ? i.GetString() : "";
                    var className = button.TryGetProperty("className", out var c) ? c.GetString() : "";
                    var text = button.TryGetProperty("text", out var txt) ? txt.GetString() : "";

                    _logger.LogDebug("   - Tag: {Tag}, ID: '{Id}', Class: '{Class}', Text: '{Text}'",
                        tag, id, className, text?.Trim());
                }
            }

            // Suggest alternative selectors based on original selector intent
            if (originalSelector.Contains("username") || originalSelector.Contains("email"))
            {
                _logger.LogDebug("💡 Suggested selectors for username/email field:");
                _logger.LogDebug("   - input[name='email']");
                _logger.LogDebug("   - input[name='username']");
                _logger.LogDebug("   - input[placeholder*='email']");
                _logger.LogDebug("   - input[placeholder*='username']");
                _logger.LogDebug("   - input[type='email']");
            }
            else if (originalSelector.Contains("password"))
            {
                _logger.LogDebug("💡 Suggested selectors for password field:");
                _logger.LogDebug("   - input[type='password']");
                _logger.LogDebug("   - input[name='password']");
                _logger.LogDebug("   - input[placeholder*='password']");
            }
            else if (originalSelector.Contains("button") || originalSelector.Contains("submit") || originalSelector.Contains("login"))
            {
                _logger.LogDebug("💡 Suggested selectors for submit/login button:");
                _logger.LogDebug("   - button[type='submit']");
                _logger.LogDebug("   - input[type='submit']");
                _logger.LogDebug("   - button:contains('Login')");
                _logger.LogDebug("   - button:contains('Sign')");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to get page debugging info: {Error}", ex.Message);
        }
    }

    private async Task<StepResult> ExecuteUIStepEnhanced(TestStep step, CancellationToken cancellationToken)
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
                _logger.LogDebug("⏳ Waiting {Ms}ms before step execution", step.WaitBefore.Value.TotalMilliseconds);
                await Task.Delay(step.WaitBefore.Value, cancellationToken);
            }

            _logger.LogDebug("🔧 Executing enhanced UI step: {Action} on {Target}", step.Action, step.Target);

            switch (step.Action.ToLowerInvariant())
            {
                case "navigate":
                    await ExecuteNavigate(step, stepResult);
                    break;
                case "click":
                    await ExecuteEnhancedClick(step, stepResult);
                    break;
                case "enter_text":
                case "type":
                    await ExecuteEnhancedEnterText(step, stepResult);
                    break;
                case "verify_element":
                    await ExecuteVerifyElement(step, stepResult);
                    break;
                case "wait":
                    await ExecuteWait(step, stepResult);
                    break;
                case "verify_text":
                    await ExecuteVerifyText(step, stepResult);
                    break;
                case "take_screenshot":
                    await ExecuteTakeScreenshot(step, stepResult);
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

            _logger.LogError(ex, "💥 Enhanced UI step execution failed: {Action} on {Target}", step.Action, step.Target);
        }

        return stepResult;
    }

    /// <summary>
    /// Enhanced text entry with better error handling and retry logic
    /// </summary>
    private async Task ExecuteEnhancedEnterText(TestStep step, StepResult stepResult)
    {
        try
        {
            _logger.LogDebug("🔍 Looking for text input element with selectors: {Target}", step.Target);

            var element = await FindElementEnhanced(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found with any selector: {step.Target}";

                // Provide specific guidance for text input fields
                stepResult.Message += "\n💡 Try inspecting the page to find the correct input field selector.";
                stepResult.Message += "\n   Common patterns: input[name='email'], input[type='email'], #username, etc.";
                return;
            }

            var text = step.GetParameterValue("value")?.ToString() ?? step.GetParameterValue("text")?.ToString() ?? "";
            var clearFirst = step.GetParameterValue("clearFirst")?.ToString()?.ToLowerInvariant() == "true";

            _logger.LogDebug("✅ Found input element, entering text: {TextLength} characters", text.Length);

            // Scroll element into view
            var jsExecutor = (IJavaScriptExecutor)_driver!;
            jsExecutor.ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", element);

            // Wait a moment for scroll to complete
            await Task.Delay(500);

            // Focus on element
            try
            {
                element.Click();
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not click element for focus: {Error}", ex.Message);
            }

            // Clear existing text if specified
            if (clearFirst)
            {
                try
                {
                    element.Clear();
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not clear element: {Error}", ex.Message);
                    // Try alternative clearing method
                    element.SendKeys(Keys.Control + "a");
                    await Task.Delay(100);
                    element.SendKeys(Keys.Delete);
                    await Task.Delay(100);
                }
            }

            // Enter the text
            element.SendKeys(text);
            await Task.Delay(300);

            // Verify text was entered
            var enteredValue = element.GetAttribute("value") ?? "";
            if (enteredValue.Contains(text) || text.Contains(enteredValue))
            {
                stepResult.Passed = true;
                stepResult.Message = $"Successfully entered text into element. Value: '{enteredValue}'";
                stepResult.ActualResult = $"Text entered: {text} (verified: {enteredValue})";
            }
            else
            {
                stepResult.Passed = false;
                stepResult.Message = $"Text entry may have failed. Expected: '{text}', Found: '{enteredValue}'";
                stepResult.ActualResult = $"Expected: {text}, Actual: {enteredValue}";
            }
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Text entry failed: {ex.Message}";
            _logger.LogError(ex, "Enhanced text entry failed for step: {StepDescription}", step.Description);
        }
    }

    /// <summary>
    /// Enhanced click with better element detection and retry logic
    /// </summary>
    private async Task ExecuteEnhancedClick(TestStep step, StepResult stepResult)
    {
        try
        {
            _logger.LogDebug("🔍 Looking for clickable element with selectors: {Target}", step.Target);

            var element = await FindElementEnhanced(step.Target, step.Timeout);
            if (element == null)
            {
                stepResult.Passed = false;
                stepResult.Message = $"Element not found with any selector: {step.Target}";

                // Provide specific guidance for buttons
                stepResult.Message += "\n💡 Try inspecting the page to find the correct button selector.";
                stepResult.Message += "\n   Common patterns: button[type='submit'], .btn-primary, input[type='submit'], etc.";
                return;
            }

            _logger.LogDebug("✅ Found clickable element: {TagName}", element.TagName);

            // Wait for element to be clickable
            try
            {
                _wait?.Until(ExpectedConditions.ElementToBeClickable(element));
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogDebug("Element may not be immediately clickable, proceeding anyway");
            }

            // Scroll element into view
            var jsExecutor = (IJavaScriptExecutor)_driver!;
            jsExecutor.ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", element);

            // Wait for scroll and any animations
            await Task.Delay(1000);

            // Try different click methods
            bool clickSucceeded = false;
            Exception? lastException = null;

            // Method 1: Regular click
            try
            {
                element.Click();
                clickSucceeded = true;
                _logger.LogDebug("✅ Regular click succeeded");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogDebug("Regular click failed: {Error}", ex.Message);
            }

            // Method 2: JavaScript click if regular click failed
            if (!clickSucceeded)
            {
                try
                {
                    jsExecutor.ExecuteScript("arguments[0].click();", element);
                    clickSucceeded = true;
                    _logger.LogDebug("✅ JavaScript click succeeded");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogDebug("JavaScript click failed: {Error}", ex.Message);
                }
            }

            // Method 3: Actions click if JavaScript click failed
            if (!clickSucceeded)
            {
                try
                {
                    var actions = new Actions(_driver);
                    actions.MoveToElement(element).Click().Perform();
                    clickSucceeded = true;
                    _logger.LogDebug("✅ Actions click succeeded");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogDebug("Actions click failed: {Error}", ex.Message);
                }
            }

            if (clickSucceeded)
            {
                stepResult.Passed = true;
                stepResult.Message = $"Successfully clicked element: {step.Target}";
                stepResult.ActualResult = "Element clicked successfully";

                // Wait a moment for any page changes
                await Task.Delay(1000);
            }
            else
            {
                stepResult.Passed = false;
                stepResult.Message = $"Click failed with all methods: {lastException?.Message}";
                stepResult.ActualResult = "All click methods failed";
            }
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Click operation failed: {ex.Message}";
            _logger.LogError(ex, "Enhanced click failed for step: {StepDescription}", step.Description);
        }
    }

    #region Standard UI Methods (from original UITestExecutor)

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

    private async Task ExecuteVerifyElement(TestStep step, StepResult stepResult)
    {
        try
        {
            var verificationMode = step.GetParameterValue("mode")?.ToString()?.ToLowerInvariant() ?? "visible";

            var element = await FindElementEnhanced(step.Target, step.Timeout);

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

    private async Task ExecuteVerifyText(TestStep step, StepResult stepResult)
    {
        try
        {
            var element = await FindElementEnhanced(step.Target, step.Timeout);
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
                    var element = await FindElementEnhanced(step.Target, step.Timeout ?? TimeSpan.FromSeconds(30));
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
            return await Task.FromResult(filePath);
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

    #region Interface Implementation

    public async Task<ExecutorValidationResult> ValidateScenario(TestScenario scenario)
    {
        var result = new ExecutorValidationResult { CanExecute = true };

        if (scenario.Type != TestType.UI && scenario.Type != TestType.Mixed)
        {
            result.CanExecute = false;
            result.Messages.Add($"Enhanced UI executor cannot handle test type: {scenario.Type}");
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
                "navigate", "click", "enter_text", "verify_element", "verify_text",
                "wait", "take_screenshot", "scroll"
            },
            MaxParallelExecutions = _configuration.MaxParallelSessions,
            SupportsScreenshots = true,
            SupportsVideoRecording = false,
            SupportedBrowsers = new List<string> { "chrome", "firefox", "edge" },
            AdditionalCapabilities = new Dictionary<string, object>
            {
                ["enhanced_selectors"] = true,
                ["smart_element_finding"] = true,
                ["debug_logging"] = true,
                ["multiple_click_methods"] = true
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
            result.Message = "Enhanced UI Test Executor is healthy";
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

            _logger.LogInformation("Enhanced UI Test Executor initialized with browser: {Browser}, headless: {Headless}",
                browserType, headless);

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Enhanced UI Test Executor");
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
            _logger.LogInformation("Enhanced UI Test Executor cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Enhanced UI Test Executor cleanup");
        }

        await Task.CompletedTask;
    }

    #endregion

    #region WebDriver Creation Methods

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

    #endregion



}