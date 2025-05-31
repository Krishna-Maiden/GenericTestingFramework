using GenericTestingFramework.Core.Models;

namespace GenericTestingFramework.Services.Executors;

/// <summary>
/// Base class for test executors with common functionality
/// </summary>
public abstract class BaseTestExecutor
{
    protected readonly Dictionary<string, object> _testContext = new();

    protected async Task ExecutePreconditions(List<string> preconditions, TestResult result)
    {
        foreach (var precondition in preconditions)
        {
            // Log precondition check
            Console.WriteLine($"Checking precondition: {precondition}");
            await Task.Delay(100); // Simulate precondition check
        }
    }

    protected bool IsUIAction(string action)
    {
        var uiActions = new[] { "navigate", "click", "double_click", "right_click", "hover", 
            "enter_text", "clear_text", "select_option", "select_checkbox", "upload_file", 
            "switch_frame", "switch_window", "scroll", "verify_text", "verify_element", 
            "verify_attribute", "take_screenshot", "execute_script", "drag_drop" };
        return uiActions.Contains(action.ToLowerInvariant());
    }

    protected bool IsAPIAction(string action)
    {
        return action.ToLowerInvariant().StartsWith("api_") || 
               action.ToLowerInvariant().StartsWith("verify_") ||
               action.ToLowerInvariant() == "extract_value" ||
               action.ToLowerInvariant() == "set_variable";
    }

    protected List<string> ValidateUIStep(TestStep step)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(step.Target))
            errors.Add($"UI step '{step.Action}' requires a target element");

        // Validate specific UI actions
        switch (step.Action.ToLowerInvariant())
        {
            case "enter_text":
            case "type":
                if (!step.Parameters.ContainsKey("value") && !step.StepData.ContainsKey("value"))
                    errors.Add("Text input actions require a 'value' parameter");
                break;

            case "select_option":
                if (!step.Parameters.ContainsKey("option") && !step.StepData.ContainsKey("option"))
                    errors.Add("Select option actions require an 'option' parameter");
                break;

            case "upload_file":
                if (!step.Parameters.ContainsKey("filePath") && !step.StepData.ContainsKey("filePath"))
                    errors.Add("File upload actions require a 'filePath' parameter");
                break;
        }
            
        return errors;
    }

    protected List<string> ValidateAPIStep(TestStep step)
    {
        var errors = new List<string>();
        
        if (step.Action.StartsWith("api_") && string.IsNullOrEmpty(step.Target))
            errors.Add($"API step '{step.Action}' requires a target URL");

        // Validate specific API actions
        switch (step.Action.ToLowerInvariant())
        {
            case "api_post":
            case "api_put":
            case "api_patch":
                if (!step.Parameters.ContainsKey("body") && !step.StepData.ContainsKey("body"))
                    errors.Add("HTTP POST/PUT/PATCH actions require a 'body' parameter");
                break;

            case "verify_status_code":
                if (!step.Parameters.ContainsKey("expectedCode") && !step.StepData.ContainsKey("expectedCode"))
                    errors.Add("Status code verification requires an 'expectedCode' parameter");
                break;

            case "verify_header":
                if (!step.Parameters.ContainsKey("headerName") && !step.StepData.ContainsKey("headerName"))
                    errors.Add("Header verification requires a 'headerName' parameter");
                break;
        }
            
        return errors;
    }

    protected string GetParameterValue(TestStep step, string key, string defaultValue = "")
    {
        if (step.Parameters.TryGetValue(key, out var paramValue))
            return paramValue?.ToString() ?? defaultValue;
        
        if (step.StepData.TryGetValue(key, out var stepValue))
            return stepValue?.ToString() ?? defaultValue;

        return defaultValue;
    }

    protected T GetParameterValue<T>(TestStep step, string key, T defaultValue = default)
    {
        var value = GetParameterValue(step, key);
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    protected void SetContextValue(string key, object value)
    {
        _testContext[key] = value;
    }

    protected T GetContextValue<T>(string key, T defaultValue = default)
    {
        if (_testContext.TryGetValue(key, out var value))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
}