using System.Text.Json;

namespace GenericTestingFramework.Services.Documents;

/// <summary>
/// Manages user story documents and extracts test scenarios
/// </summary>
public interface IDocumentManager
{
    Task<UserStoryDocument> UploadUserStory(string filePath, CancellationToken cancellationToken = default);
    Task<UserStoryDocument> CreateUserStoryFromText(string userStoryText, string projectContext = "", CancellationToken cancellationToken = default);
    Task<List<UserStoryDocument>> GetUserStories(string projectId, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserStory(string documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of document manager for user stories
/// </summary>
public class DocumentManager : IDocumentManager
{
    private readonly Dictionary<string, UserStoryDocument> _documents = new();
    private readonly ILogger<DocumentManager> _logger;

    public DocumentManager(ILogger<DocumentManager> logger)
    {
        _logger = logger;
    }

    public async Task<UserStoryDocument> UploadUserStory(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"User story file not found: {filePath}");

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        
        var document = new UserStoryDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = fileName,
            Content = content,
            FilePath = filePath,
            UploadedAt = DateTime.UtcNow,
            ProjectContext = ExtractProjectContextFromContent(content)
        };

        _documents[document.Id] = document;
        _logger.LogInformation("Uploaded user story document: {FileName}", fileName);
        
        return document;
    }

    public async Task<UserStoryDocument> CreateUserStoryFromText(string userStoryText, string projectContext = "", CancellationToken cancellationToken = default)
    {
        var document = new UserStoryDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = $"UserStory_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            Content = userStoryText,
            UploadedAt = DateTime.UtcNow,
            ProjectContext = !string.IsNullOrEmpty(projectContext) ? projectContext : ExtractProjectContextFromContent(userStoryText)
        };

        _documents[document.Id] = document;
        _logger.LogInformation("Created user story document from text");
        
        return await Task.FromResult(document);
    }

    public async Task<List<UserStoryDocument>> GetUserStories(string projectId, CancellationToken cancellationToken = default)
    {
        var documents = _documents.Values
            .Where(d => string.IsNullOrEmpty(projectId) || d.ProjectContext.Contains(projectId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

        return await Task.FromResult(documents);
    }

    public async Task<bool> DeleteUserStory(string documentId, CancellationToken cancellationToken = default)
    {
        var removed = _documents.Remove(documentId);
        if (removed)
        {
            _logger.LogInformation("Deleted user story document: {DocumentId}", documentId);
        }
        return await Task.FromResult(removed);
    }

    private string ExtractProjectContextFromContent(string content)
    {
        // Extract project context from content using keywords
        var keywords = new[] { "portal", "admin", "authentication", "login", "confessions", "tracking" };
        var foundKeywords = keywords.Where(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (foundKeywords.Any())
        {
            return string.Join(", ", foundKeywords);
        }

        // Try to extract URLs
        var urlPattern = @"https?://[^\s]+";
        var match = System.Text.RegularExpressions.Regex.Match(content, urlPattern);
        if (match.Success)
        {
            return $"Web application at {match.Value}";
        }

        return "General web application";
    }
}

/// <summary>
/// Represents a user story document
/// </summary>
public class UserStoryDocument
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public DateTime UploadedAt { get; set; }
    public string ProjectContext { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}