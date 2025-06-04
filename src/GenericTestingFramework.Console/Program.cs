using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GenericTestingFramework.Core.Interfaces;
using GenericTestingFramework.Services;
using GenericTestingFramework.Services.LLM;
using GenericTestingFramework.Services.Repository;
using GenericTestingFramework.Services.Executors;
using GenericTestingFramework.Services.Documents;
using GenericTestingFramework.Services.TestGeneration;
using GenericTestingFramework.Services.Documents.Models;

Console.WriteLine("🚀 Dynamic Test Generation Framework - Console Application (GPT-3.5-Turbo Powered)");
Console.WriteLine("==================================================================================");

try
{
    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    // Setup DI container
    var services = new ServiceCollection();

    // Add logging
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // Add configuration
    services.AddSingleton<IConfiguration>(configuration);

    // Configure LLM service
    services.Configure<LLMConfiguration>(
        configuration.GetSection(LLMConfiguration.SectionName));

    // Create and register UI Test Configuration directly
    var uiTestConfig = new UITestConfiguration();
    configuration.GetSection("UITestConfiguration").Bind(uiTestConfig);
    services.AddSingleton(uiTestConfig);

    // Create and register API Test Configuration directly  
    var apiTestConfig = new APITestConfiguration();
    configuration.GetSection("APITestConfiguration").Bind(apiTestConfig);
    services.AddSingleton(apiTestConfig);

    // Add HTTP clients
    services.AddHttpClient<OpenAILLMService>();
    services.AddHttpClient<APITestExecutor>();

    // Register document manager
    services.AddSingleton<IDocumentManager, DocumentManager>();

    // Register framework services with real OpenAI LLM service
    services.AddSingleton<ILLMService, OpenAILLMService>();
    services.AddSingleton<ITestRepository, InMemoryTestRepository>();

    // Register test executors
    services.AddTransient<ITestExecutor, UITestExecutor>();
    services.AddTransient<ITestExecutor, MockAPITestExecutor>();

    // Register main service
    services.AddTransient<TestAutomationService>();

    var serviceProvider = services.BuildServiceProvider();

    // Validate OpenAI configuration
    var llmConfig = new LLMConfiguration();
    configuration.GetSection(LLMConfiguration.SectionName).Bind(llmConfig);

    Console.WriteLine("\n🤖 OpenAI GPT-3.5-Turbo Integration Status:");
    if (!llmConfig.IsValid())
    {
        Console.WriteLine("❌ Configuration Issues:");
        foreach (var error in llmConfig.GetValidationErrors())
        {
            Console.WriteLine($"   • {error}");
        }
        Console.WriteLine("\n💡 Please check your appsettings.json file");
    }
    else if (llmConfig.ApiKey == "YOUR_OPENAI_API_KEY_HERE")
    {
        Console.WriteLine("⚠️  API Key Not Configured");
        Console.WriteLine("   Please update 'ApiKey' in appsettings.json with your actual OpenAI API key");
        Console.WriteLine("   Get your key from: https://platform.openai.com/api-keys");
    }
    else
    {
        Console.WriteLine("✅ Configuration Valid");
        Console.WriteLine($"   Model: {llmConfig.Model}");
        Console.WriteLine($"   Max Tokens: {llmConfig.MaxTokens}");
        Console.WriteLine($"   Temperature: {llmConfig.Temperature}");
    }

    var testService = serviceProvider.GetRequiredService<TestAutomationService>();
    var documentManager = serviceProvider.GetRequiredService<IDocumentManager>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting Dynamic Test Generation Framework");

    // Check for available user story files with enhanced discovery
    var userStoryFiles = GetAvailableUserStoryFiles();

    Console.WriteLine("\n📝 Dynamic Test Generation Options");
    Console.WriteLine("==================================");

    // Enhanced file discovery feedback
    if (!userStoryFiles.Any())
    {
        Console.WriteLine("\n⚠️  No user story files found in standard locations.");
        Console.WriteLine("   Searched in:");
        ShowSearchedPaths();
        Console.WriteLine("\n   Consider:");
        Console.WriteLine("   1. Create docs/user-stories/ directory");
        Console.WriteLine("   2. Place your .txt or .md files there");
        Console.WriteLine("   3. Or use option 1 to upload from any location");
    }
    else
    {
        Console.WriteLine($"\n✅ Found {userStoryFiles.Count} user story file(s)");
    }

    // Show available options
    Console.WriteLine("\nChoose an option:");
    Console.WriteLine("1. Upload a user story file (browse to any location)");
    Console.WriteLine("2. Enter user story text manually");

    if (userStoryFiles.Any())
    {
        Console.WriteLine("3. Select from available user story files:");
        for (int i = 0; i < userStoryFiles.Count; i++)
        {
            var fileName = Path.GetFileName(userStoryFiles[i]);
            var fileInfo = new FileInfo(userStoryFiles[i]);
            var relativePath = GetRelativePath(userStoryFiles[i]);
            Console.WriteLine($"   {i + 1}. {fileName} ({relativePath})");
        }
    }

    Console.WriteLine($"\nEnter your choice (1, 2{(userStoryFiles.Any() ? ", or 3" : "")}):");
    var choice = Console.ReadLine()?.Trim();

    UserStoryDocument? document = null;
    string projectId = "dynamic-testing";

    switch (choice)
    {
        case "1":
            document = await HandleFileUpload(documentManager);
            break;

        case "2":
            document = await HandleManualInput(documentManager);
            break;

        case "3":
            if (userStoryFiles.Any())
            {
                document = await HandleFileSelection(documentManager, userStoryFiles);
            }
            else
            {
                Console.WriteLine("❌ No user story files available.");
                return;
            }
            break;

        default:
            Console.WriteLine("❌ Invalid choice. Exiting...");
            return;
    }

    if (document == null)
    {
        Console.WriteLine("❌ No user story provided. Exiting...");
        return;
    }

    // Display document information
    Console.WriteLine($"\n📄 Processing User Story Document:");
    Console.WriteLine($"   File: {document.FileName}");
    Console.WriteLine($"   Content Length: {document.Content.Length} characters");
    Console.WriteLine($"   Project Context: {document.ProjectContext}");
    Console.WriteLine($"   Uploaded: {document.UploadedAt:yyyy-MM-dd HH:mm:ss}");

    // Show content preview
    var preview = document.Content.Length > 200 ?
        document.Content.Substring(0, 200) + "..." :
        document.Content;
    Console.WriteLine($"\n📋 Content Preview:");
    Console.WriteLine($"   {preview}");

    // Generate test scenario dynamically using OpenAI GPT-3.5-Turbo
    Console.WriteLine("\n🤖 Generating test scenario using OpenAI GPT-3.5-Turbo...");
    Console.WriteLine("   This may take 5-15 seconds depending on API response time...");
    var scenarioId = await testService.CreateTestFromUserStory(
        document.Content,
        projectId,
        document.ProjectContext);

    Console.WriteLine($"✅ Generated test scenario: {scenarioId}");

    // Get the generated scenario details
    var projectTests = await testService.GetProjectTests(projectId);
    var generatedScenario = projectTests.FirstOrDefault();

    if (generatedScenario != null)
    {
        Console.WriteLine($"\n📋 Generated Test Scenario Details:");
        Console.WriteLine($"   Title: {generatedScenario.Title}");
        Console.WriteLine($"   Type: {generatedScenario.Type}");
        Console.WriteLine($"   Priority: {generatedScenario.Priority}");
        Console.WriteLine($"   Steps: {generatedScenario.Steps.Count}");
        Console.WriteLine($"   Tags: {string.Join(", ", generatedScenario.Tags)}");

        Console.WriteLine($"\n📋 Generated Test Steps:");
        foreach (var step in generatedScenario.Steps.OrderBy(s => s.Order))
        {
            Console.WriteLine($"   {step.Order}. {step.Action.ToUpper()} - {step.Description}");
            Console.WriteLine($"      Target: {step.Target}");
            Console.WriteLine($"      Expected: {step.ExpectedResult}");
            if (step.Parameters.Any())
            {
                var paramStr = string.Join(", ", step.Parameters.Select(p => $"{p.Key}={p.Value}"));
                Console.WriteLine($"      Parameters: {paramStr}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"📋 Preconditions:");
        foreach (var precondition in generatedScenario.Preconditions)
        {
            Console.WriteLine($"   • {precondition}");
        }

        Console.WriteLine($"\n📋 Expected Outcomes:");
        foreach (var outcome in generatedScenario.ExpectedOutcomes)
        {
            Console.WriteLine($"   • {outcome}");
        }

        // Ask user if they want to execute the test
        Console.WriteLine("\n🚀 Execute the generated test? (y/n):");
        var executeChoice = Console.ReadLine()?.ToLowerInvariant();

        if (executeChoice == "y" || executeChoice == "yes")
        {
            Console.WriteLine("\n🔄 Executing dynamically generated test...");
            Console.WriteLine("   (Browser will open if headless mode is disabled)");

            var result = await testService.ExecuteTest(scenarioId);
            Console.WriteLine($"\n✅ Test Execution Result: {(result.Passed ? "PASSED" : "FAILED")}");
            Console.WriteLine($"   Duration: {result.Duration}");
            Console.WriteLine($"   Steps executed: {result.StepResults.Count}");
            Console.WriteLine($"   Success rate: {result.GetSuccessRate():F1}%");

            if (!result.Passed)
            {
                Console.WriteLine($"\n❌ Test Failed: {result.Message}");
                var firstFailure = result.GetFirstFailure();
                if (firstFailure != null)
                {
                    Console.WriteLine($"   First Failed Step: {firstFailure.StepName}");
                    Console.WriteLine($"   Error: {firstFailure.Message}");
                }
            }

            // Show detailed step results
            Console.WriteLine("\n📋 Detailed Step Results:");
            foreach (var stepResult in result.StepResults)
            {
                var status = stepResult.Passed ? "✅ PASS" : "❌ FAIL";
                Console.WriteLine($"   {status} - {stepResult.StepName}");
                Console.WriteLine($"          Action: {stepResult.Action} on {stepResult.Target}");
                Console.WriteLine($"          Duration: {stepResult.Duration.TotalMilliseconds:F0}ms");
                if (!stepResult.Passed)
                {
                    Console.WriteLine($"          Error: {stepResult.Message}");
                }
                if (!string.IsNullOrEmpty(stepResult.ScreenshotPath))
                {
                    Console.WriteLine($"          Screenshot: {stepResult.ScreenshotPath}");
                }
                Console.WriteLine();
            }
        }
    }

    // Show document management summary
    Console.WriteLine("\n📚 Document Management Summary:");
    var allDocuments = await documentManager.GetUserStories("");
    Console.WriteLine($"   Total user story documents: {allDocuments.Count}");

    foreach (var doc in allDocuments.Take(5)) // Show last 5 documents
    {
        Console.WriteLine($"   • {doc.FileName} (uploaded: {doc.UploadedAt:yyyy-MM-dd HH:mm})");
        Console.WriteLine($"     Context: {doc.ProjectContext}");
    }

    // Get test statistics
    Console.WriteLine("\n📊 Test Execution Statistics:");
    var fromDate = DateTime.UtcNow.AddDays(-1);
    var toDate = DateTime.UtcNow;

    var stats = await testService.GetTestStatistics(projectId, fromDate, toDate);
    Console.WriteLine($"   Total Scenarios: {stats.TotalScenarios}");
    Console.WriteLine($"   Total Executions: {stats.TotalExecutions}");
    Console.WriteLine($"   Pass Rate: {stats.PassRate:F1}%");
    Console.WriteLine($"   Average Duration: {stats.AverageDuration}");

    // Health check
    Console.WriteLine("\n🏥 Executor Health Status:");
    var healthStatus = await testService.GetExecutorHealthStatus();

    foreach (var executor in healthStatus)
    {
        var status = executor.Value.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
        Console.WriteLine($"   {executor.Key}: {status} ({executor.Value.ResponseTime.TotalMilliseconds:F0}ms)");
    }

    Console.WriteLine("\n🎉 Dynamic Test Generation Completed!");
    Console.WriteLine("\n💡 Framework Features Demonstrated:");
    Console.WriteLine("   ✓ User story file upload and management");
    Console.WriteLine("   ✓ OpenAI GPT-3.5-Turbo powered intelligent test generation");
    Console.WriteLine("   ✓ Advanced natural language processing of user stories");
    Console.WriteLine("   ✓ Smart extraction of URLs, credentials, and actions");
    Console.WriteLine("   ✓ Context-aware test step generation with AI");
    Console.WriteLine("   ✓ Real-time test execution with detailed reporting");
    Console.WriteLine("   ✓ Screenshot capture on test failures");
    Console.WriteLine("   ✓ Comprehensive test result analysis");

    logger.LogInformation("Dynamic Test Generation Framework completed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

#region Helper Methods

static List<string> GetAvailableUserStoryFiles()
{
    var files = new List<string>();
    var currentDirectory = Directory.GetCurrentDirectory();

    // Enhanced search paths - try both relative and absolute paths
    var searchPaths = new List<string>
    {
        // Relative to current directory
        Path.Combine(currentDirectory, "docs", "user-stories"),
        Path.Combine(currentDirectory, "docs"),
        Path.Combine(currentDirectory, "user-stories"),
        Path.Combine(currentDirectory),
        
        // Go up directory tree and search
        Path.Combine(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory, "docs", "user-stories"),
        Path.Combine(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory, "docs"),
        
        // Go up two levels (in case we're in bin/Debug/net8.0)
        Path.Combine(Directory.GetParent(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory)?.FullName ?? currentDirectory, "docs", "user-stories"),
        Path.Combine(Directory.GetParent(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory)?.FullName ?? currentDirectory, "docs"),
        
        // Go up three levels (in case we're in bin/Debug/net8.0)
        Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory)?.FullName ?? currentDirectory)?.FullName ?? currentDirectory, "docs", "user-stories"),
        Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory)?.FullName ?? currentDirectory)?.FullName ?? currentDirectory, "docs"),
        
        // Search from solution root if we can find it
        FindSolutionRoot(currentDirectory)
    };

    // Remove duplicates and null paths
    searchPaths = searchPaths.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

    var extensions = new[] { "*.txt", "*.md", "*.markdown" };

    Console.WriteLine($"\n🔍 Searching for user story files...");
    Console.WriteLine($"   Current directory: {currentDirectory}");

    foreach (var path in searchPaths.Where(Directory.Exists))
    {
        Console.WriteLine($"   Searching: {path}");

        foreach (var ext in extensions)
        {
            try
            {
                var foundFiles = Directory.GetFiles(path, ext, SearchOption.TopDirectoryOnly);
                files.AddRange(foundFiles);

                if (foundFiles.Any())
                {
                    Console.WriteLine($"     Found {foundFiles.Length} {ext} file(s)");
                    foreach (var file in foundFiles)
                    {
                        Console.WriteLine($"       - {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     Error searching {path}: {ex.Message}");
            }
        }
    }

    var uniqueFiles = files.Distinct().ToList();
    Console.WriteLine($"\n   Total unique files found: {uniqueFiles.Count}");

    return uniqueFiles;
}

static string? FindSolutionRoot(string startPath)
{
    var current = new DirectoryInfo(startPath);

    while (current != null)
    {
        // Look for .sln file or typical solution indicators
        if (current.GetFiles("*.sln").Any() ||
            current.GetDirectories("src").Any() ||
            current.GetDirectories("docs").Any())
        {
            var docsPath = Path.Combine(current.FullName, "docs", "user-stories");
            if (Directory.Exists(docsPath))
            {
                return docsPath;
            }

            docsPath = Path.Combine(current.FullName, "docs");
            if (Directory.Exists(docsPath))
            {
                return docsPath;
            }
        }

        current = current.Parent;
    }

    return null;
}

static void ShowSearchedPaths()
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var searchPaths = new[]
    {
        "docs/user-stories/",
        "docs/",
        "user-stories/",
        "../docs/user-stories/",
        "../../docs/user-stories/",
        "../../../docs/user-stories/"
    };

    foreach (var path in searchPaths)
    {
        var fullPath = Path.Combine(currentDirectory, path);
        var exists = Directory.Exists(fullPath) ? "✅" : "❌";
        Console.WriteLine($"     {exists} {path}");
    }
}

static string GetRelativePath(string filePath)
{
    var currentDir = Directory.GetCurrentDirectory();
    try
    {
        return Path.GetRelativePath(currentDir, filePath);
    }
    catch
    {
        return filePath;
    }
}

static async Task<UserStoryDocument?> HandleFileUpload(IDocumentManager documentManager)
{
    Console.WriteLine("\n📁 Enter the full path to your user story file:");
    Console.WriteLine("   (You can drag and drop a file here or type the path)");
    var filePath = Console.ReadLine()?.Trim().Trim('"');

    if (string.IsNullOrWhiteSpace(filePath))
    {
        Console.WriteLine("❌ No file path provided.");
        return null;
    }

    // Handle drag-and-drop or quoted paths
    filePath = filePath.Trim('"', '\'');

    if (!File.Exists(filePath))
    {
        Console.WriteLine($"❌ File not found: {filePath}");

        // Try to help with common issues
        if (!Path.IsPathFullyQualified(filePath))
        {
            var currentDir = Directory.GetCurrentDirectory();
            var attemptPath = Path.Combine(currentDir, filePath);
            if (File.Exists(attemptPath))
            {
                filePath = attemptPath;
                Console.WriteLine($"✅ Found file at: {filePath}");
            }
            else
            {
                Console.WriteLine($"   Also tried: {attemptPath}");
                return null;
            }
        }
        else
        {
            return null;
        }
    }

    try
    {
        var document = await documentManager.UploadUserStory(filePath);
        Console.WriteLine($"✅ Successfully uploaded: {document.FileName}");
        return document;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to upload file: {ex.Message}");
        return null;
    }
}

static async Task<UserStoryDocument?> HandleManualInput(IDocumentManager documentManager)
{
    Console.WriteLine("\n✏️ Enter your user story (press Enter twice to finish):");
    var lines = new List<string>();
    string? line;
    int emptyLineCount = 0;

    while ((line = Console.ReadLine()) != null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            emptyLineCount++;
            if (emptyLineCount >= 2) break;
        }
        else
        {
            emptyLineCount = 0;
        }
        lines.Add(line);
    }

    var userStory = string.Join(Environment.NewLine, lines).Trim();

    if (string.IsNullOrWhiteSpace(userStory))
    {
        Console.WriteLine("❌ No user story provided.");
        return null;
    }

    Console.WriteLine("\n📝 Enter project context (optional):");
    var projectContext = Console.ReadLine()?.Trim() ?? "";

    try
    {
        var document = await documentManager.CreateUserStoryFromText(userStory, projectContext);
        Console.WriteLine($"✅ Successfully created user story document: {document.FileName}");
        return document;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to create user story: {ex.Message}");
        return null;
    }
}

static async Task<UserStoryDocument?> HandleFileSelection(IDocumentManager documentManager, List<string> availableFiles)
{
    Console.WriteLine($"\n📋 Available user story files:");
    for (int i = 0; i < availableFiles.Count; i++)
    {
        var fileName = Path.GetFileName(availableFiles[i]);
        var fileInfo = new FileInfo(availableFiles[i]);
        var relativePath = GetRelativePath(availableFiles[i]);
        Console.WriteLine($"   {i + 1}. {fileName}");
        Console.WriteLine($"      Path: {relativePath}");
        Console.WriteLine($"      Size: {fileInfo.Length} bytes");
        Console.WriteLine($"      Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
    }

    Console.WriteLine($"Select a file (1-{availableFiles.Count}):");
    var selection = Console.ReadLine()?.Trim();

    if (!int.TryParse(selection, out var fileIndex) || fileIndex < 1 || fileIndex > availableFiles.Count)
    {
        Console.WriteLine("❌ Invalid selection.");
        return null;
    }

    var selectedFile = availableFiles[fileIndex - 1];
    Console.WriteLine($"📄 Selected: {Path.GetFileName(selectedFile)}");

    try
    {
        var document = await documentManager.UploadUserStory(selectedFile);
        Console.WriteLine($"✅ Successfully loaded: {document.FileName}");
        return document;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to load file: {ex.Message}");
        return null;
    }
}

#endregion

#region Mock API Executor

// Keep the mock API executor for now since we're focusing on UI testing
public class MockAPITestExecutor : ITestExecutor
{
    public string Name => "Mock API Test Executor";

    public bool CanExecute(GenericTestingFramework.Core.Models.TestType testType) =>
        testType == GenericTestingFramework.Core.Models.TestType.API ||
        testType == GenericTestingFramework.Core.Models.TestType.Mixed;

    public async Task<GenericTestingFramework.Core.Models.TestResult> ExecuteTest(
        GenericTestingFramework.Core.Models.TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var result = new GenericTestingFramework.Core.Models.TestResult
        {
            ScenarioId = scenario.Id,
            StartedAt = DateTime.UtcNow,
            Environment = GenericTestingFramework.Core.Models.TestEnvironment.Testing
        };

        await Task.Delay(100, cancellationToken);
        result.Complete();
        return result;
    }

    public Task<GenericTestingFramework.Core.Interfaces.ExecutorValidationResult> ValidateScenario(
        GenericTestingFramework.Core.Models.TestScenario scenario)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.ExecutorValidationResult
        {
            CanExecute = true,
            Messages = new List<string> { "Mock API validation passed" }
        });
    }

    public GenericTestingFramework.Core.Interfaces.ExecutorCapabilities GetCapabilities()
    {
        return new GenericTestingFramework.Core.Interfaces.ExecutorCapabilities
        {
            SupportedTestTypes = new List<GenericTestingFramework.Core.Models.TestType>
            {
                GenericTestingFramework.Core.Models.TestType.API,
                GenericTestingFramework.Core.Models.TestType.Mixed
            },
            SupportedActions = new List<string> { "api_get", "api_post", "verify_status", "verify_body" },
            MaxParallelExecutions = 3,
            SupportsScreenshots = false
        };
    }

    public Task<GenericTestingFramework.Core.Interfaces.HealthCheckResult> PerformHealthCheck(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GenericTestingFramework.Core.Interfaces.HealthCheckResult
        {
            IsHealthy = true,
            Message = "Mock API executor is healthy",
            ResponseTime = TimeSpan.FromMilliseconds(25)
        });
    }

    public Task<bool> Initialize(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task Cleanup(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

#endregion