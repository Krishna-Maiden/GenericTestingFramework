# AI-Powered Generic Testing Framework

A revolutionary testing framework that generates and executes automated tests from natural language user stories using AI/LLM technology.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-blue)
![Build Status](https://img.shields.io/badge/Build-Passing-green)

## 🚀 Features

### ✨ **AI-Powered Test Generation**
- Convert natural language user stories into executable test scenarios
- Automatic test step generation with proper assertions
- Intelligent test optimization and refinement
- Context-aware test data generation

### 🎯 **Universal Testing**
- **UI Testing**: Selenium-based web automation
- **API Testing**: RESTful API validation and verification
- **Mixed Scenarios**: Combined UI and API testing workflows
- **Cross-Browser Support**: Chrome, Firefox, Edge

### 🤖 **Smart Automation**
- File-free test management (no project-specific files needed)
- Generic framework works across all projects
- Parallel test execution with intelligent scheduling
- Real-time failure analysis and recommendations

### 📊 **Advanced Analytics**
- Comprehensive test execution statistics
- Performance metrics and trend analysis
- Interactive dashboards and reporting
- Health monitoring for all test executors

## 🏗️ Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Web API       │    │   Console App   │    │   Test Runner   │
│   (REST)        │    │   (CLI)         │    │   (Direct)      │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          └──────────────────────┼──────────────────────┘
                                 │
                    ┌─────────────▼───────────────┐
                    │   TestAutomationService     │
                    │   (Orchestration Layer)     │
                    └─────────────┬───────────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │                         │                         │
┌───────▼────────┐    ┌───────────▼────────┐    ┌──────────▼─────────┐
│  LLM Service   │    │  Test Executors    │    │   Repository       │
│  (OpenAI)      │    │  - UI (Selenium)   │    │   (In-Memory/DB)   │
│                │    │  - API (HttpClient)│    │                    │
└────────────────┘    └────────────────────┘    └────────────────────┘
```

## 🚦 Quick Start

### Prerequisites

- **.NET 8 SDK** or later
- **OpenAI API Key** (optional - framework includes mock service for demos)
- **Chrome Browser** (for UI testing)

### 1. Clone and Build

```bash
git clone <repository-url>
cd GenericTestingFramework
dotnet restore
dotnet build
```

### 2. Configure OpenAI (Optional)

Update `appsettings.json` in WebAPI project:

```json
{
  "LLM": {
    "ApiKey": "your-openai-api-key-here",
    "Model": "gpt-4",
    "MaxTokens": 4000,
    "Temperature": 0.7
  }
}
```

### 3. Run Demo

```bash
# Console demo with mock AI
cd src/GenericTestingFramework.Console
dotnet run

# Web API with Swagger UI
cd src/GenericTestingFramework.WebAPI
dotnet run
# Visit https://localhost:7234
```

## 🎮 Usage Examples

### Console Application

```csharp
var testService = serviceProvider.GetRequiredService<TestAutomationService>();

// Create test from user story
var scenarioId = await testService.CreateTestFromUserStory(
    "As a customer, I want to login to access my account",
    "insurance-project",
    "SafeGuard Insurance Platform with multi-factor authentication"
);

// Execute the test
var result = await testService.ExecuteTest(scenarioId);
Console.WriteLine($"Test Result: {(result.Passed ? "PASSED" : "FAILED")}");
```

### REST API

```bash
# Create test scenario
curl -X POST "https://localhost:7234/api/tests/create" \
  -H "Content-Type: application/json" \
  -d '{
    "userStory": "As a user, I want to get an auto insurance quote",
    "projectId": "safeguard-insurance",
    "projectContext": "Insurance quoting system with VIN validation"
  }'

# Execute test
curl -X POST "https://localhost:7234/api/tests/{scenarioId}/execute"
```

### Direct Service Usage

```csharp
// Dependency injection setup
services.AddSingleton<ILLMService, OpenAILLMService>();
services.AddSingleton<ITestRepository, InMemoryTestRepository>();
services.AddTransient<ITestExecutor, UITestExecutor>();
services.AddTransient<ITestExecutor, APITestExecutor>();
services.AddTransient<TestAutomationService>();

// Usage
var testService = serviceProvider.GetRequiredService<TestAutomationService>();

// Generate and execute tests
var scenarios = await testService.SuggestAdditionalTests(
    "insurance-project", 
    "Comprehensive insurance platform testing"
);

var results = await testService.ExecuteTestsParallel(
    scenarios.Select(s => s.Id).ToList(), 
    maxConcurrency: 3
);
```

## 🧪 Sample User Stories

The framework works with natural language user stories. Here are examples:

### Insurance Domain
```
"As a SafeGuard Insurance customer, I want to get an auto insurance quote online so that I can compare rates and coverage options for my vehicle."

"As a registered customer, I want to securely log into my account so that I can access my policy information and perform account activities."

"As a policyholder, I want to file a claim online so that I can quickly initiate the claims process after an incident."
```

### E-commerce Domain
```
"As a customer, I want to add items to my shopping cart so that I can purchase multiple products together."

"As a user, I want to search for products by category so that I can find what I'm looking for quickly."

"As a buyer, I want to complete checkout with my saved payment method so that I can finalize my purchase efficiently."
```

### Banking Domain
```
"As a bank customer, I want to transfer money between my accounts so that I can manage my finances effectively."

"As a user, I want to view my transaction history so that I can track my spending and account activity."
```

## 📁 Project Structure

```
GenericTestingFramework/
├── src/
│   ├── GenericTestingFramework.Core/          # Domain models and interfaces
│   │   ├── Models/                            # TestScenario, TestResult, etc.
│   │   └── Interfaces/                        # ILLMService, ITestExecutor, etc.
│   ├── GenericTestingFramework.Services/      # Business logic and services
│   │   ├── LLM/                              # OpenAI integration
│   │   ├── Executors/                        # UI and API test executors
│   │   ├── Repository/                       # Data storage abstractions
│   │   └── TestAutomationService.cs         # Main orchestration service
│   ├── GenericTestingFramework.WebAPI/        # REST API endpoints
│   └── GenericTestingFramework.Console/       # CLI application
├── tests/
│   ├── GenericTestingFramework.Tests.Unit/    # Unit tests
│   └── GenericTestingFramework.Tests.Integration/ # Integration tests
└── docs/                                      # Documentation
```

## 🔧 Configuration

### LLM Configuration
```json
{
  "LLM": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4",
    "MaxTokens": 4000,
    "Temperature": 0.7,
    "TimeoutSeconds": 300,
    "MaxRetries": 3,
    "EnableCaching": true,
    "SystemPrompt": "You are an expert test automation engineer..."
  }
}
```

### UI Test Configuration
```json
{
  "UITestConfiguration": {
    "BaseUrl": "https://your-app.com",
    "Headless": false,
    "WindowSize": "1920,1080",
    "DefaultTimeoutSeconds": 30,
    "ScreenshotPath": "./screenshots",
    "MaxParallelSessions": 3
  }
}
```

### API Test Configuration
```json
{
  "APITestConfiguration": {
    "DefaultTimeoutSeconds": 30,
    "MaxConcurrentRequests": 5,
    "DefaultHeaders": {
      "User-Agent": "GenericTestingFramework/1.0.0"
    }
  }
}
```

## 🏃‍♂️ Running Tests

### Unit Tests
```bash
cd tests/GenericTestingFramework.Tests.Unit
dotnet test
```

### Integration Tests
```bash
cd tests/GenericTestingFramework.Tests.Integration
dotnet test
```

### All Tests with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 🔌 API Endpoints

### Test Management
- `POST /api/tests/create` - Create test from user story
- `POST /api/tests/{id}/execute` - Execute test scenario
- `GET /api/projects/{projectId}/tests` - Get project tests
- `GET /api/projects/{projectId}/statistics` - Get test statistics

### Health & Monitoring
- `GET /health` - System health check
- `GET /version` - Framework version info

### Example API Usage

#### Create Test Scenario
```http
POST /api/tests/create
Content-Type: application/json

{
  "userStory": "As a user, I want to login to the application",
  "projectId": "my-project",
  "projectContext": "Web application with OAuth authentication"
}
```

#### Execute Test
```http
POST /api/tests/550e8400-e29b-41d4-a716-446655440000/execute
```

## 🧩 Extending the Framework

### Custom Test Executor

```csharp
public class DatabaseTestExecutor : BaseTestExecutor, ITestExecutor
{
    public string Name => "Database Test Executor";
    
    public bool CanExecute(TestType testType) => testType == TestType.Database;
    
    public async Task<TestResult> ExecuteTest(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        // Implementation for database testing
        // Connect to database, execute queries, validate results
        return new TestResult { /* ... */ };
    }
    
    // Implement other interface methods...
}

// Register in DI container
services.AddTransient<ITestExecutor, DatabaseTestExecutor>();
```

### Custom LLM Service

```csharp
public class AzureOpenAIService : ILLMService
{
    public async Task<TestScenario> GenerateTestFromNaturalLanguage(
        string userStory, string projectContext, CancellationToken cancellationToken = default)
    {
        // Implementation using Azure OpenAI
        // Custom prompt engineering, response parsing
        return new TestScenario { /* ... */ };
    }
    
    // Implement other interface methods...
}
```

## 📊 Monitoring & Analytics

### Test Execution Metrics
- Success/failure rates by project and test type
- Average execution duration trends
- Most frequently failing test scenarios
- Performance degradation alerts

### Health Monitoring
- Executor availability and response times
- LLM service connectivity and quota usage
- Resource utilization (CPU, memory, network)
- Test environment status

### Reporting Features
- Executive dashboards with key metrics
- Detailed test execution reports
- Trend analysis and predictive insights
- Custom report generation via API

## 🔒 Security & Best Practices

### API Security
- Bearer token authentication for sensitive operations
- Rate limiting to prevent abuse
- Input validation and sanitization
- Secure storage of API keys and credentials

### Test Data Security
- No sensitive data in test scenarios
- Encrypted storage of test results
- Audit trails for all test operations
- Compliance with data protection regulations

### Infrastructure Security
- Secure communication (HTTPS/TLS)
- Container security best practices
- Network isolation for test environments
- Regular security updates and patches

## 🚀 Deployment

### Docker Deployment
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/GenericTestingFramework.WebAPI/GenericTestingFramework.WebAPI.csproj", "src/GenericTestingFramework.WebAPI/"]
RUN dotnet restore "src/GenericTestingFramework.WebAPI/GenericTestingFramework.WebAPI.csproj"
COPY . .
RUN dotnet build "src/GenericTestingFramework.WebAPI/GenericTestingFramework.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/GenericTestingFramework.WebAPI/GenericTestingFramework.WebAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GenericTestingFramework.WebAPI.dll"]
```

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: testing-framework-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: testing-framework-api
  template:
    metadata:
      labels:
        app: testing-framework-api
    spec:
      containers:
      - name: api
        image: testing-framework:latest
        ports:
        - containerPort: 80
        env:
        - name: LLM__ApiKey
          valueFrom:
            secretKeyRef:
              name: openai-secret
              key: api-key
```

### Azure DevOps Pipeline
```yaml
trigger:
- main

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

steps:
- task: DotNetCoreCLI@2
  displayName: 'Restore packages'
  inputs:
    command: 'restore'
    projects: '**/*.csproj'

- task: DotNetCoreCLI@2
  displayName: 'Build'
  inputs:
    command: 'build'
    projects: '**/*.csproj'
    arguments: '--configuration $(buildConfiguration)'

- task: DotNetCoreCLI@2
  displayName: 'Run tests'
  inputs:
    command: 'test'
    projects: '**/tests/**/*.csproj'
    arguments: '--configuration $(buildConfiguration) --collect "Code coverage"'

- task: DotNetCoreCLI@2
  displayName: 'Publish'
  inputs:
    command: 'publish'
    projects: '**/GenericTestingFramework.WebAPI.csproj'
    arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)'

- task: PublishBuildArtifacts@1
  inputs:
    PathtoPublish: '$(Build.ArtifactStagingDirectory)'
    ArtifactName: 'drop'
```

## 🤝 Contributing

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/amazing-feature`)
3. **Make your changes** with proper tests
4. **Commit your changes** (`git commit -m 'Add amazing feature'`)
5. **Push to the branch** (`git push origin feature/amazing-feature`)
6. **Open a Pull Request**

### Development Guidelines
- Follow C# coding standards and conventions
- Write comprehensive unit tests for new features
- Update documentation for any API changes
- Ensure all tests pass before submitting PR
- Include performance impact analysis for significant changes

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **OpenAI** for providing the GPT models that power our AI test generation
- **Selenium** community for the robust web automation framework
- **Microsoft** for the excellent .NET ecosystem and development tools
- **Insurance Industry** partners for providing real-world testing scenarios

## 📞 Support & Contact

- **Documentation**: [Full Documentation](docs/)
- **Issues**: [GitHub Issues](https://github.com/your-org/generic-testing-framework/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-org/generic-testing-framework/discussions)
- **Email**: support@your-company.com

## 🗺️ Roadmap

### Version 1.1 (Q2 2025)
- [ ] Database testing executor
- [ ] Performance testing capabilities
- [ ] Visual regression testing
- [ ] Advanced AI test optimization

### Version 1.2 (Q3 2025)
- [ ] Mobile app testing support
- [ ] Multi-language test generation
- [ ] Advanced analytics dashboard
- [ ] Enterprise SSO integration

### Version 2.0 (Q4 2025)
- [ ] Self-healing test capabilities
- [ ] Predictive test failure analysis
- [ ] Advanced AI model fine-tuning
- [ ] Cloud-native distributed execution

---

**Built with ❤️ by the AI Testing Framework Team**

*Revolutionizing software testing through artificial intelligence and automation*