using GenericTestingFramework.Services.Documents.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace GenericTestingFramework.Services.Documents;

/// <summary>
/// Enhanced DocumentManager with duplicate prevention and better tracking
/// </summary>
public class DocumentManager : IDocumentManager
{
    private readonly Dictionary<string, UserStoryDocument> _documents = new();
    private readonly Dictionary<string, string> _contentHashes = new(); // Hash -> DocumentId mapping
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

        // Check for duplicate content
        var contentHash = ComputeContentHash(content);
        if (_contentHashes.TryGetValue(contentHash, out var existingDocId))
        {
            var existingDoc = _documents[existingDocId];
            _logger.LogInformation("File {FileName} has same content as existing document {ExistingFileName}. Returning existing document.",
                fileName, existingDoc.FileName);
            return existingDoc;
        }

        var document = new UserStoryDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = fileName,
            Content = content,
            FilePath = filePath,
            Source = DocumentSource.FileUpload,
            FileFormat = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
            UploadedAt = DateTime.UtcNow,
            ProjectContext = ExtractProjectContextFromContent(content),
            Metadata = await ExtractMetadata(content)
        };

        _documents[document.Id] = document;
        _contentHashes[contentHash] = document.Id;

        _logger.LogInformation("Uploaded user story document: {FileName} with ID: {DocumentId}", fileName, document.Id);

        return document;
    }

    public async Task<UserStoryDocument> CreateUserStoryFromText(string userStoryText, string projectContext = "", CancellationToken cancellationToken = default)
    {
        // Check for duplicate content
        var contentHash = ComputeContentHash(userStoryText);
        if (_contentHashes.TryGetValue(contentHash, out var existingDocId))
        {
            var existingDoc = _documents[existingDocId];
            _logger.LogInformation("Text content matches existing document {ExistingFileName}. Returning existing document.",
                existingDoc.FileName);
            return existingDoc;
        }

        var document = new UserStoryDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = $"UserStory_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            Content = userStoryText,
            Source = DocumentSource.ManualEntry,
            FileFormat = "text",
            UploadedAt = DateTime.UtcNow,
            ProjectContext = !string.IsNullOrEmpty(projectContext) ? projectContext : ExtractProjectContextFromContent(userStoryText),
            Metadata = await ExtractMetadata(userStoryText)
        };

        _documents[document.Id] = document;
        _contentHashes[contentHash] = document.Id;

        _logger.LogInformation("Created user story document from text with ID: {DocumentId}", document.Id);

        return document;
    }

    public async Task<List<UserStoryDocument>> GetUserStories(string projectId, CancellationToken cancellationToken = default)
    {
        var documents = _documents.Values
            .Where(d => string.IsNullOrEmpty(projectId) || d.ProjectContext.Contains(projectId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

        _logger.LogDebug("Retrieved {Count} user story documents for project: {ProjectId}", documents.Count, projectId);
        return await Task.FromResult(documents);
    }

    public async Task<List<UserStoryDocument>> SearchUserStories(string searchQuery, string projectId = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return await GetUserStories(projectId, cancellationToken);

        var query = searchQuery.ToLowerInvariant();
        var documents = _documents.Values
            .Where(d =>
                (string.IsNullOrEmpty(projectId) || d.ProjectContext.Contains(projectId, StringComparison.OrdinalIgnoreCase)) &&
                (d.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 d.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 d.ProjectContext.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 d.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                 d.Metadata.Keywords.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

        _logger.LogInformation("Found {Count} documents matching search query: {Query}", documents.Count, searchQuery);
        return await Task.FromResult(documents);
    }

    public async Task<UserStoryDocument?> GetUserStoryById(string documentId, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(documentId, out var document);
        _logger.LogDebug("Retrieved document by ID: {DocumentId}, Found: {Found}", documentId, document != null);
        return await Task.FromResult(document);
    }

    public async Task<bool> UpdateUserStory(UserStoryDocument document, CancellationToken cancellationToken = default)
    {
        if (document == null || !_documents.ContainsKey(document.Id))
        {
            _logger.LogWarning("Cannot update document: Document is null or not found with ID: {DocumentId}", document?.Id);
            return false;
        }

        var oldDocument = _documents[document.Id];

        // If content changed, update hash mapping
        if (oldDocument.Content != document.Content)
        {
            // Remove old hash mapping
            var oldHash = ComputeContentHash(oldDocument.Content);
            _contentHashes.Remove(oldHash);

            // Add new hash mapping
            var newHash = ComputeContentHash(document.Content);
            _contentHashes[newHash] = document.Id;

            // Update metadata
            document.Metadata = await ExtractMetadata(document.Content);
            document.ProjectContext = ExtractProjectContextFromContent(document.Content);
        }

        document.Touch(); // Updates LastModified and Version
        _documents[document.Id] = document;

        _logger.LogInformation("Updated user story document: {DocumentId}", document.Id);
        return true;
    }

    public async Task<bool> DeleteUserStory(string documentId, CancellationToken cancellationToken = default)
    {
        if (_documents.TryGetValue(documentId, out var document))
        {
            // Remove hash mapping
            var contentHash = ComputeContentHash(document.Content);
            _contentHashes.Remove(contentHash);

            var removed = _documents.Remove(documentId);
            if (removed)
            {
                _logger.LogInformation("Deleted user story document: {DocumentId}", documentId);
            }
            return await Task.FromResult(removed);
        }

        _logger.LogWarning("Cannot delete document: Document not found with ID: {DocumentId}", documentId);
        return false;
    }

    public async Task<DocumentValidationResult> ValidateUserStory(UserStoryDocument document)
    {
        var validation = new DocumentValidationResult();
        var issues = document.Validate();

        validation.IsValid = !issues.Any();
        validation.Issues.AddRange(issues);

        // Calculate quality score
        int score = 100;

        // Deduct points for issues
        score -= issues.Count * 10;

        // Deduct points for missing elements
        if (string.IsNullOrWhiteSpace(document.ProjectContext))
            score -= 15;

        if (!document.Metadata.Urls.Any() && !ContainsActionWords(document.Content))
            score -= 10;

        if (document.Content.Length < 50)
        {
            score -= 20;
            validation.Issues.Add("User story content is too short for effective test generation");
        }

        if (document.Content.Length > 10000)
        {
            score -= 10;
            validation.Warnings.Add("User story content is very long and may be complex to process");
        }

        // Add suggestions based on analysis
        if (!document.Metadata.Urls.Any())
            validation.Suggestions.Add("Consider including specific URLs or endpoints for better test targeting");

        if (!document.Metadata.Actions.Any())
            validation.Suggestions.Add("Include specific actions (login, navigate, click, etc.) for clearer test steps");

        if (string.IsNullOrWhiteSpace(document.ProjectContext))
            validation.Suggestions.Add("Add project context for better test scenario generation");

        if (!document.Tags.Any())
            validation.Suggestions.Add("Add relevant tags for better organization and searchability");

        validation.QualityScore = Math.Max(0, Math.Min(100, score));

        _logger.LogDebug("Validated document {DocumentId}: Quality Score: {Score}, Valid: {IsValid}",
            document.Id, validation.QualityScore, validation.IsValid);

        return await Task.FromResult(validation);
    }

    public async Task<DocumentMetadata> ExtractMetadata(string content)
    {
        var metadata = new DocumentMetadata();

        // Extract URLs
        metadata.Urls = ExtractUrls(content);

        // Extract email addresses
        metadata.EmailAddresses = ExtractEmailAddresses(content);

        // Extract keywords
        metadata.Keywords = ExtractKeywords(content);

        // Extract actions
        metadata.Actions = ExtractActions(content);

        // Extract test entities
        metadata.TestEntities = ExtractTestEntities(content);

        // Calculate complexity score
        metadata.ComplexityScore = CalculateComplexityScore(content);

        // Detect language (simple detection)
        metadata.Language = DetectLanguage(content);

        // Analyze sentiment (basic implementation)
        metadata.Sentiment = AnalyzeSentiment(content);

        _logger.LogDebug("Extracted metadata: {UrlCount} URLs, {KeywordCount} keywords, {ActionCount} actions",
            metadata.Urls.Count, metadata.Keywords.Count, metadata.Actions.Count);

        return await Task.FromResult(metadata);
    }

    #region Private Helper Methods

    private string ComputeContentHash(string content)
    {
        // Normalize content for hash comparison (remove extra whitespace, convert to lowercase)
        var normalizedContent = Regex.Replace(content.ToLowerInvariant().Trim(), @"\s+", " ");

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedContent));
        return Convert.ToBase64String(hashBytes);
    }

    private string ExtractProjectContextFromContent(string content)
    {
        // Extract project context from content using keywords and patterns
        var contextKeywords = new[] { "portal", "admin", "authentication", "login", "confessions", "tracking",
            "dashboard", "application", "system", "platform", "website", "app", "service" };

        var foundKeywords = contextKeywords
            .Where(k => content.Contains(k, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (foundKeywords.Any())
        {
            return string.Join(", ", foundKeywords.Take(5));
        }

        // Try to extract URLs for context
        var urls = ExtractUrls(content);
        if (urls.Any())
        {
            var domain = ExtractDomainFromUrl(urls.First());
            return $"Web application at {domain}";
        }

        // Look for project names in content
        var projectPattern = @"project[:\s]+([^\s,\.]+)";
        var match = Regex.Match(content, projectPattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return $"{match.Groups[1].Value} project";
        }

        return "General web application";
    }

    private List<string> ExtractUrls(string content)
    {
        var urls = new List<string>();
        var urlPattern = @"https?://[^\s]+";
        var matches = Regex.Matches(content, urlPattern);

        foreach (Match match in matches)
        {
            urls.Add(match.Value.TrimEnd('.', ',', ';', ')', ']', '}'));
        }

        return urls.Distinct().ToList();
    }

    private List<string> ExtractEmailAddresses(string content)
    {
        var emails = new List<string>();
        var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
        var matches = Regex.Matches(content, emailPattern);

        foreach (Match match in matches)
        {
            emails.Add(match.Value);
        }

        return emails.Distinct().ToList();
    }

    private List<string> ExtractKeywords(string content)
    {
        var keywords = new List<string>();
        var keywordPatterns = new Dictionary<string, string[]>
        {
            ["authentication"] = new[] { "login", "signin", "authenticate", "credentials", "password", "username", "email", "auth" },
            ["navigation"] = new[] { "navigate", "go to", "visit", "open", "access", "browse", "redirect" },
            ["form"] = new[] { "enter", "fill", "input", "type", "submit", "click", "select", "form", "field" },
            ["verification"] = new[] { "verify", "check", "confirm", "validate", "assert", "ensure", "test" },
            ["admin"] = new[] { "admin", "administrator", "management", "dashboard", "portal", "control" },
            ["user"] = new[] { "user", "customer", "client", "account", "profile", "member" },
            ["data"] = new[] { "data", "information", "details", "record", "save", "store", "retrieve" },
            ["security"] = new[] { "secure", "permission", "access", "role", "rights", "authorize" }
        };

        foreach (var category in keywordPatterns)
        {
            foreach (var keyword in category.Value)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    keywords.Add(category.Key);
                    break;
                }
            }
        }

        return keywords.Distinct().ToList();
    }

    private List<string> ExtractActions(string content)
    {
        var actions = new List<string>();
        var actionPatterns = new[]
        {
            @"\b(login|log in|sign in|signin)\b",
            @"\b(logout|log out|sign out|signout)\b",
            @"\b(navigate|go to|visit|browse to)\b",
            @"\b(enter|input|type|fill in|provide)\b",
            @"\b(click|press|tap|select)\b",
            @"\b(verify|check|validate|confirm|ensure)\b",
            @"\b(search|find|look for|query)\b",
            @"\b(submit|send|post|save)\b",
            @"\b(edit|update|modify|change)\b",
            @"\b(delete|remove|cancel)\b",
            @"\b(upload|download|attach)\b",
            @"\b(view|see|display|show)\b"
        };

        foreach (var pattern in actionPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var action = match.Value.ToLowerInvariant().Replace(" ", "_");
                actions.Add(action);
            }
        }

        return actions.Distinct().ToList();
    }

    private List<string> ExtractTestEntities(string content)
    {
        var entities = new List<string>();
        var entityPatterns = new[]
        {
            @"\b(button|link|field|input|form|page|screen|dialog|modal|menu)\b",
            @"\b(element|component|widget|control|panel|section)\b",
            @"\b(user|admin|customer|client|account|profile)\b",
            @"\b(message|error|warning|notification|alert)\b",
            @"\b(data|information|record|item|entry)\b"
        };

        foreach (var pattern in entityPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                entities.Add(match.Value.ToLowerInvariant());
            }
        }

        return entities.Distinct().ToList();
    }

    private int CalculateComplexityScore(string content)
    {
        int score = 1; // Base complexity

        // Length-based complexity
        if (content.Length > 500) score++;
        if (content.Length > 1000) score++;
        if (content.Length > 2000) score++;

        // URL complexity
        var urls = ExtractUrls(content);
        score += Math.Min(urls.Count, 3);

        // Action complexity
        var actions = ExtractActions(content);
        score += Math.Min(actions.Count / 2, 3);

        // Conditional complexity (if, when, unless, etc.)
        var conditionalWords = new[] { "if", "when", "unless", "provided", "given", "then", "else" };
        foreach (var word in conditionalWords)
        {
            if (content.Contains(word, StringComparison.OrdinalIgnoreCase))
                score++;
        }

        return Math.Min(score, 10); // Cap at 10
    }

    private string DetectLanguage(string content)
    {
        // Simple language detection based on common words
        var englishWords = new[] { "the", "and", "or", "is", "are", "was", "were", "have", "has", "will", "would", "should", "could" };
        var englishCount = englishWords.Count(word => content.Contains($" {word} ", StringComparison.OrdinalIgnoreCase));

        return englishCount > 0 ? "en" : "unknown";
    }

    private string AnalyzeSentiment(string content)
    {
        // Basic sentiment analysis
        var positiveWords = new[] { "want", "need", "should", "must", "will", "can", "able", "success", "complete", "good", "easy" };
        var negativeWords = new[] { "not", "cannot", "unable", "fail", "error", "wrong", "bad", "difficult", "hard", "problem" };

        var positiveCount = positiveWords.Count(word => content.Contains(word, StringComparison.OrdinalIgnoreCase));
        var negativeCount = negativeWords.Count(word => content.Contains(word, StringComparison.OrdinalIgnoreCase));

        if (positiveCount > negativeCount * 2) return "positive";
        if (negativeCount > positiveCount * 2) return "negative";
        return "neutral";
    }

    private bool ContainsActionWords(string content)
    {
        var actionWords = new[] { "click", "enter", "type", "navigate", "login", "submit", "verify", "check", "select", "upload" };
        return actionWords.Any(word => content.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private string ExtractDomainFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    public int GetDocumentCount()
    {
        return _documents.Count;
    }

    public List<string> GetAllDocumentIds()
    {
        return _documents.Keys.ToList();
    }

    #endregion
}