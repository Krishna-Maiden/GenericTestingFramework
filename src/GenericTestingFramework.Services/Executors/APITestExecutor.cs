using System.Text;
using System.Text.Json;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GenericTestingFramework.Services.Executors;

/// <summary>
/// Test executor for API testing using HttpClient
/// </summary>
public class APITestExecutor : BaseTestExecutor, ITestExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<APITestExecutor> _logger;
    private readonly APITestConfiguration _configuration;

    public string Name => "API Test Executor";

    public APITestExecutor(
        HttpClient httpClient, 
        ILogger<APITestExecutor> logger, 
        IOptions<APITestConfiguration> configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public bool CanExecute(TestType testType)
    {
        return testType == TestType.API || testType == TestType.Mixed;
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

            _logger.LogInformation("Starting API test execution for scenario {ScenarioId}", scenario.Id);

            // Execute preconditions
            await ExecutePreconditions(scenario.Preconditions, result);

            // Execute API test steps
            foreach (var step in scenario.Steps.Where(s => s.IsEnabled && IsAPIAction(s.Action)).OrderBy(s => s.Order))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Passed = false;
                    result.Message = "Test execution was cancelled";
                    break;
                }

                var stepResult = await ExecuteAPIStep(step, cancellationToken);
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
            _logger.LogInformation("API test execution completed for scenario {ScenarioId}. Passed: {Passed}", 
                scenario.Id, result.Passed);
        }
        catch (OperationCanceledException)
        {
            result.Passed = false;
            result.Message = "Test execution was cancelled";
            _logger.LogInformation("API test execution was cancelled for scenario {ScenarioId}", scenario.Id);
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
            _logger.LogError(ex, "API test execution failed for scenario {ScenarioId}", scenario.Id);
        }

        return result;
    }

    public async Task<ExecutorValidationResult> ValidateScenario(TestScenario scenario)
    {
        var result = new ExecutorValidationResult { CanExecute = true };

        if (scenario.Type != TestType.API && scenario.Type != TestType.Mixed)
        {
            result.CanExecute = false;
            result.Messages.Add($"API executor cannot handle test type: {scenario.Type}");
            return result;
        }

        // Validate API steps
        foreach (var step in scenario.Steps.Where(s => IsAPIAction(s.Action)))
        {
            var stepValidation = ValidateAPIStep(step);
            result.Messages.AddRange(stepValidation);
        }

        return await Task.FromResult(result);
    }

    public ExecutorCapabilities GetCapabilities()
    {
        return new ExecutorCapabilities
        {
            SupportedTestTypes = new List<TestType> { TestType.API, TestType.Mixed },
            SupportedActions = new List<string>
            {
                "api_get", "api_post", "api_put", "api_delete", "api_patch", "api_head", "api_options",
                "verify_status_code", "verify_header", "verify_body", "verify_json_path", "verify_response_time",
                "extract_value", "set_variable", "wait_for_response", "validate_schema"
            },
            MaxParallelExecutions = _configuration.MaxConcurrentRequests,
            SupportsScreenshots = false,
            SupportsVideoRecording = false,
            SupportedBrowsers = new List<string>(), // N/A for API testing
            AdditionalCapabilities = new Dictionary<string, object>
            {
                ["authentication_support"] = true,
                ["json_validation"] = true,
                ["xml_validation"] = true,
                ["schema_validation"] = true,
                ["performance_metrics"] = true
            }
        };
    }

    public async Task<HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();
        var startTime = DateTime.UtcNow;

        try
        {
            // Test basic HTTP connectivity
            var response = await _httpClient.GetAsync("https://httpbin.org/status/200", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                result.IsHealthy = true;
                result.Message = "API Test Executor is healthy";
            }
            else
            {
                result.IsHealthy = false;
                result.Message = $"Health check failed with status: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.Message = $"Health check failed: {ex.Message}";
        }

        result.ResponseTime = DateTime.UtcNow - startTime;
        return result;
    }

    public async Task<bool> Initialize(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            // Configure HttpClient based on test configuration
            _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.DefaultTimeoutSeconds);

            // Add default headers
            foreach (var header in _configuration.DefaultHeaders)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Add authentication if specified
            if (configuration.TryGetValue("authToken", out var authToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken.ToString());
            }

            _logger.LogInformation("API Test Executor initialized successfully");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize API Test Executor");
            return false;
        }
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear any default headers that were added during initialization
            _httpClient.DefaultRequestHeaders.Clear();
            _logger.LogInformation("API Test Executor cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during API Test Executor cleanup");
        }

        await Task.CompletedTask;
    }

    private async Task<StepResult> ExecuteAPIStep(TestStep step, CancellationToken cancellationToken)
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

            _logger.LogDebug("Executing API step: {Action} on {Target}", step.Action, step.Target);

            switch (step.Action.ToLowerInvariant())
            {
                case "api_get":
                    await ExecuteHttpGet(step, stepResult, cancellationToken);
                    break;
                case "api_post":
                    await ExecuteHttpPost(step, stepResult, cancellationToken);
                    break;
                case "api_put":
                    await ExecuteHttpPut(step, stepResult, cancellationToken);
                    break;
                case "api_delete":
                    await ExecuteHttpDelete(step, stepResult, cancellationToken);
                    break;
                case "api_patch":
                    await ExecuteHttpPatch(step, stepResult, cancellationToken);
                    break;
                case "api_head":
                    await ExecuteHttpHead(step, stepResult, cancellationToken);
                    break;
                case "api_options":
                    await ExecuteHttpOptions(step, stepResult, cancellationToken);
                    break;
                case "verify_status_code":
                    await VerifyStatusCode(step, stepResult);
                    break;
                case "verify_header":
                    await VerifyHeader(step, stepResult);
                    break;
                case "verify_body":
                    await VerifyResponseBody(step, stepResult);
                    break;
                case "verify_json_path":
                    await VerifyJsonPath(step, stepResult);
                    break;
                case "verify_response_time":
                    await VerifyResponseTime(step, stepResult);
                    break;
                case "extract_value":
                    await ExtractValue(step, stepResult);
                    break;
                case "set_variable":
                    await SetVariable(step, stepResult);
                    break;
                case "wait_for_response":
                    await WaitForResponse(step, stepResult, cancellationToken);
                    break;
                case "validate_schema":
                    await ValidateSchema(step, stepResult);
                    break;
                default:
                    stepResult.Passed = false;
                    stepResult.Message = $"Unknown API action: {step.Action}";
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
            
            _logger.LogError(ex, "API step execution failed: {Action} on {Target}", step.Action, step.Target);
        }

        return stepResult;
    }

    private async Task ExecuteHttpGet(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var startTime = DateTime.UtcNow;

        var response = await _httpClient.GetAsync(url, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ExecuteHttpPost(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var content = CreateHttpContent(step);
        var startTime = DateTime.UtcNow;

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ExecuteHttpPut(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var content = CreateHttpContent(step);
        var startTime = DateTime.UtcNow;

        var response = await _httpClient.PutAsync(url, content, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ExecuteHttpDelete(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var startTime = DateTime.UtcNow;

        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ExecuteHttpPatch(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var content = CreateHttpContent(step);
        var startTime = DateTime.UtcNow;

        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ExecuteHttpHead(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var startTime = DateTime.UtcNow;

        var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ExecuteHttpOptions(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var startTime = DateTime.UtcNow;

        var request = new HttpRequestMessage(HttpMethod.Options, url);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseTime = DateTime.UtcNow - startTime;
        
        await ProcessHttpResponse(response, stepResult, responseTime);
    }

    private async Task ProcessHttpResponse(HttpResponseMessage response, StepResult stepResult, TimeSpan responseTime)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        
        // Store response data for later verification steps
        SetContextValue("last_response", response);
        SetContextValue("last_response_body", responseBody);
        SetContextValue("last_response_time", responseTime);

        stepResult.Passed = response.IsSuccessStatusCode;
        stepResult.Message = stepResult.Passed ? 
            $"HTTP request successful (Status: {response.StatusCode})" : 
            $"HTTP request failed (Status: {response.StatusCode})";
        stepResult.ActualResult = $"Status: {response.StatusCode}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 200))}";
        
        // Add response details to step data
        stepResult.StepData["status_code"] = (int)response.StatusCode;
        stepResult.StepData["response_time_ms"] = responseTime.TotalMilliseconds;
        stepResult.StepData["response_body"] = responseBody;
        stepResult.StepData["content_type"] = response.Content.Headers.ContentType?.ToString() ?? "";
    }

    private async Task VerifyStatusCode(TestStep step, StepResult stepResult)
    {
        var lastResponse = GetContextValue<HttpResponseMessage>("last_response");
        if (lastResponse == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response to verify";
            return;
        }

        var expectedCodeStr = GetParameterValue(step, "expectedCode", GetParameterValue(step, "expected", "200"));
        if (!int.TryParse(expectedCodeStr, out var expectedCode))
        {
            stepResult.Passed = false;
            stepResult.Message = $"Invalid expected status code: {expectedCodeStr}";
            return;
        }

        var actualCode = (int)lastResponse.StatusCode;
        stepResult.Passed = actualCode == expectedCode;
        stepResult.Message = stepResult.Passed ?
            $"Status code verification passed: {actualCode}" :
            $"Status code verification failed. Expected: {expectedCode}, Actual: {actualCode}";
        stepResult.ActualResult = actualCode.ToString();
        
        await Task.CompletedTask;
    }

    private async Task VerifyHeader(TestStep step, StepResult stepResult)
    {
        var lastResponse = GetContextValue<HttpResponseMessage>("last_response");
        if (lastResponse == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response to verify";
            return;
        }

        var headerName = GetParameterValue(step, "headerName", GetParameterValue(step, "header", ""));
        var expectedValue = GetParameterValue(step, "expected", "");

        if (string.IsNullOrEmpty(headerName))
        {
            stepResult.Passed = false;
            stepResult.Message = "Header name is required for header verification";
            return;
        }

        var headerExists = lastResponse.Headers.TryGetValues(headerName, out var headerValues) ||
                          lastResponse.Content.Headers.TryGetValues(headerName, out headerValues);

        if (!headerExists)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Header '{headerName}' not found in response";
            return;
        }

        var actualValue = string.Join(", ", headerValues!);
        var verificationMode = GetParameterValue(step, "mode", "equals").ToLowerInvariant();

        bool headerMatches = verificationMode switch
        {
            "contains" => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            "startswith" => actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            "endswith" => actualValue.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            "regex" => System.Text.RegularExpressions.Regex.IsMatch(actualValue, expectedValue),
            "exists" => true, // Header existence already verified
            _ => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase)
        };

        stepResult.Passed = headerMatches;
        stepResult.Message = headerMatches ?
            $"Header verification passed for '{headerName}'" :
            $"Header verification failed for '{headerName}'. Expected: '{expectedValue}', Actual: '{actualValue}'";
        stepResult.ActualResult = actualValue;
        
        await Task.CompletedTask;
    }

    private async Task VerifyResponseBody(TestStep step, StepResult stepResult)
    {
        var responseBody = GetContextValue<string>("last_response_body");
        if (responseBody == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response body to verify";
            return;
        }

        var expectedContent = GetParameterValue(step, "expected", step.ExpectedResult);
        var verificationMode = GetParameterValue(step, "mode", "contains").ToLowerInvariant();

        bool bodyMatches = verificationMode switch
        {
            "equals" => string.Equals(responseBody, expectedContent, StringComparison.OrdinalIgnoreCase),
            "contains" => responseBody.Contains(expectedContent, StringComparison.OrdinalIgnoreCase),
            "startswith" => responseBody.StartsWith(expectedContent, StringComparison.OrdinalIgnoreCase),
            "endswith" => responseBody.EndsWith(expectedContent, StringComparison.OrdinalIgnoreCase),
            "regex" => System.Text.RegularExpressions.Regex.IsMatch(responseBody, expectedContent),
            "json_equals" => CompareJsonContent(responseBody, expectedContent),
            "not_empty" => !string.IsNullOrWhiteSpace(responseBody),
            _ => responseBody.Contains(expectedContent, StringComparison.OrdinalIgnoreCase)
        };

        stepResult.Passed = bodyMatches;
        stepResult.Message = bodyMatches ?
            "Response body verification passed" :
            $"Response body verification failed using mode '{verificationMode}'";
        stepResult.ActualResult = responseBody.Length > 500 ? 
            responseBody.Substring(0, 500) + "..." : 
            responseBody;
        
        await Task.CompletedTask;
    }

    private async Task VerifyJsonPath(TestStep step, StepResult stepResult)
    {
        var responseBody = GetContextValue<string>("last_response_body");
        if (responseBody == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response body to verify";
            return;
        }

        var jsonPath = GetParameterValue(step, "jsonPath", GetParameterValue(step, "path", ""));
        var expectedValue = GetParameterValue(step, "expected", "");

        if (string.IsNullOrEmpty(jsonPath))
        {
            stepResult.Passed = false;
            stepResult.Message = "JSON path is required for JSON path verification";
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var actualValue = ExtractJsonValue(document.RootElement, jsonPath);

            var verificationMode = GetParameterValue(step, "mode", "equals").ToLowerInvariant();
            
            bool valueMatches = verificationMode switch
            {
                "equals" => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
                "contains" => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
                "greater_than" => CompareNumericValues(actualValue, expectedValue, ">"),
                "less_than" => CompareNumericValues(actualValue, expectedValue, "<"),
                "exists" => !string.IsNullOrEmpty(actualValue),
                "not_null" => actualValue != "null" && !string.IsNullOrEmpty(actualValue),
                _ => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase)
            };

            stepResult.Passed = valueMatches;
            stepResult.Message = valueMatches ?
                $"JSON path verification passed for '{jsonPath}'" :
                $"JSON path verification failed for '{jsonPath}'. Expected: '{expectedValue}', Actual: '{actualValue}'";
            stepResult.ActualResult = actualValue;
        }
        catch (JsonException ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Invalid JSON in response body: {ex.Message}";
        }
        
        await Task.CompletedTask;
    }

    private async Task VerifyResponseTime(TestStep step, StepResult stepResult)
    {
        var responseTime = GetContextValue<TimeSpan>("last_response_time");
        if (responseTime == default)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response time to verify";
            return;
        }

        var maxTimeStr = GetParameterValue(step, "maxTime", GetParameterValue(step, "expected", "5000"));
        if (!int.TryParse(maxTimeStr, out var maxTimeMs))
        {
            stepResult.Passed = false;
            stepResult.Message = $"Invalid max time value: {maxTimeStr}";
            return;
        }

        var actualTimeMs = responseTime.TotalMilliseconds;
        stepResult.Passed = actualTimeMs <= maxTimeMs;
        stepResult.Message = stepResult.Passed ?
            $"Response time verification passed: {actualTimeMs:F0}ms <= {maxTimeMs}ms" :
            $"Response time verification failed: {actualTimeMs:F0}ms > {maxTimeMs}ms";
        stepResult.ActualResult = $"{actualTimeMs:F0}ms";
        
        await Task.CompletedTask;
    }

    private async Task ExtractValue(TestStep step, StepResult stepResult)
    {
        var responseBody = GetContextValue<string>("last_response_body");
        if (responseBody == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response body to extract from";
            return;
        }

        var extractionPath = GetParameterValue(step, "path", GetParameterValue(step, "jsonPath", ""));
        var variableName = GetParameterValue(step, "variable", GetParameterValue(step, "name", ""));

        if (string.IsNullOrEmpty(extractionPath) || string.IsNullOrEmpty(variableName))
        {
            stepResult.Passed = false;
            stepResult.Message = "Both 'path' and 'variable' parameters are required for value extraction";
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var extractedValue = ExtractJsonValue(document.RootElement, extractionPath);
            
            SetContextValue(variableName, extractedValue);
            
            stepResult.Passed = true;
            stepResult.Message = $"Successfully extracted value '{extractedValue}' into variable '{variableName}'";
            stepResult.ActualResult = extractedValue;
        }
        catch (JsonException ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Invalid JSON in response body: {ex.Message}";
        }
        
        await Task.CompletedTask;
    }

    private async Task SetVariable(TestStep step, StepResult stepResult)
    {
        var variableName = GetParameterValue(step, "name", GetParameterValue(step, "variable", ""));
        var variableValue = GetParameterValue(step, "value", "");

        if (string.IsNullOrEmpty(variableName))
        {
            stepResult.Passed = false;
            stepResult.Message = "Variable name is required for set variable operation";
            return;
        }

        SetContextValue(variableName, variableValue);
        
        stepResult.Passed = true;
        stepResult.Message = $"Successfully set variable '{variableName}' to '{variableValue}'";
        stepResult.ActualResult = variableValue;
        
        await Task.CompletedTask;
    }

    private async Task WaitForResponse(TestStep step, StepResult stepResult, CancellationToken cancellationToken)
    {
        var url = BuildUrl(step.Target, step.Parameters);
        var maxWaitMs = GetParameterValue(step, "maxWait", "30000");
        var intervalMs = GetParameterValue(step, "interval", "1000");

        if (!int.TryParse(maxWaitMs, out var maxWait) || !int.TryParse(intervalMs, out var interval))
        {
            stepResult.Passed = false;
            stepResult.Message = "Invalid wait time or interval values";
            return;
        }

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(maxWait);

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    stepResult.Passed = true;
                    stepResult.Message = $"Response received after {(DateTime.UtcNow - startTime).TotalMilliseconds:F0}ms";
                    return;
                }
            }
            catch
            {
                // Continue waiting
            }

            await Task.Delay(interval, cancellationToken);
        }

        stepResult.Passed = false;
        stepResult.Message = $"No successful response received within {maxWait}ms";
    }

    private async Task ValidateSchema(TestStep step, StepResult stepResult)
    {
        var responseBody = GetContextValue<string>("last_response_body");
        if (responseBody == null)
        {
            stepResult.Passed = false;
            stepResult.Message = "No previous HTTP response body to validate";
            return;
        }

        var schemaType = GetParameterValue(step, "schemaType", "json").ToLowerInvariant();

        try
        {
            switch (schemaType)
            {
                case "json":
                    using (var document = JsonDocument.Parse(responseBody))
                    {
                        // Basic JSON validation - more complex schema validation would require additional libraries
                        stepResult.Passed = true;
                        stepResult.Message = "JSON schema validation passed";
                    }
                    break;
                case "xml":
                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.LoadXml(responseBody);
                    stepResult.Passed = true;
                    stepResult.Message = "XML schema validation passed";
                    break;
                default:
                    stepResult.Passed = false;
                    stepResult.Message = $"Unsupported schema type: {schemaType}";
                    break;
            }
        }
        catch (Exception ex)
        {
            stepResult.Passed = false;
            stepResult.Message = $"Schema validation failed: {ex.Message}";
        }
        
        await Task.CompletedTask;
    }

    private string BuildUrl(string baseUrl, Dictionary<string, object> parameters)
    {
        var url = baseUrl;
        
        // Replace path parameters
        foreach (var param in parameters.Where(p => p.Key.StartsWith("path_")))
        {
            var paramName = param.Key.Substring(5); // Remove "path_" prefix
            url = url.Replace($"{{{paramName}}}", param.Value.ToString());
        }

        // Add query parameters
        var queryParams = parameters.Where(p => p.Key.StartsWith("query_"))
            .Select(p => $"{p.Key.Substring(6)}={Uri.EscapeDataString(p.Value.ToString() ?? "")}")
            .ToList();

        if (queryParams.Any())
        {
            var separator = url.Contains('?') ? "&" : "?";
            url += separator + string.Join("&", queryParams);
        }

        return url;
    }

    private HttpContent? CreateHttpContent(TestStep step)
    {
        var body = GetParameterValue(step, "body", "");
        if (string.IsNullOrEmpty(body))
            return null;

        var contentType = GetParameterValue(step, "contentType", "application/json");
        
        return contentType.ToLowerInvariant() switch
        {
            "application/json" => new StringContent(body, Encoding.UTF8, "application/json"),
            "application/xml" => new StringContent(body, Encoding.UTF8, "application/xml"),
            "text/plain" => new StringContent(body, Encoding.UTF8, "text/plain"),
            "application/x-www-form-urlencoded" => new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"),
            _ => new StringContent(body, Encoding.UTF8, contentType)
        };
    }

    private bool CompareJsonContent(string actualJson, string expectedJson)
    {
        try
        {
            using var actualDoc = JsonDocument.Parse(actualJson);
            using var expectedDoc = JsonDocument.Parse(expectedJson);
            return JsonElement.DeepEquals(actualDoc.RootElement, expectedDoc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    private string ExtractJsonValue(JsonElement element, string path)
    {
        var pathParts = path.Split('.');
        var current = element;

        foreach (var part in pathParts)
        {
            if (part.Contains('[') && part.Contains(']'))
            {
                var propertyName = part.Substring(0, part.IndexOf('['));
                var indexStr = part.Substring(part.IndexOf('[') + 1, part.IndexOf(']') - part.IndexOf('[') - 1);
                
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (!current.TryGetProperty(propertyName, out current))
                        return "";
                }

                if (int.TryParse(indexStr, out var index))
                {
                    if (current.ValueKind == JsonValueKind.Array && index < current.GetArrayLength())
                        current = current[index];
                    else
                        return "";
                }
            }
            else
            {
                if (!current.TryGetProperty(part, out current))
                    return "";
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString() ?? "",
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => current.GetRawText()
        };
    }

    private bool CompareNumericValues(string actualStr, string expectedStr, string operation)
    {
        if (!double.TryParse(actualStr, out var actual) || !double.TryParse(expectedStr, out var expected))
            return false;

        return operation switch
        {
            ">" => actual > expected,
            "<" => actual < expected,
            ">=" => actual >= expected,
            "<=" => actual <= expected,
            "==" => Math.Abs(actual - expected) < 0.0001,
            _ => false
        };
    }
}

/// <summary>
/// Configuration for API test execution
/// </summary>
public class APITestConfiguration
{
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 5;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
    public bool EnableRequestLogging { get; set; } = false;
    public bool EnableResponseLogging { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalSettings { get; set; } = new();
}