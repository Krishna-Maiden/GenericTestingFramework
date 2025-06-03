using System.ComponentModel.DataAnnotations;

namespace GenericTestingFramework.Services.Documents.Models;

/// <summary>
/// Represents a user story document with content and metadata
/// </summary>
public class UserStoryDocument
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Original filename or generated name
    /// </summary>
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Full content of the user story
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Original file path (if uploaded from file)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// When the document was uploaded or created
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the document was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Project context extracted or provided
    /// </summary>
    [StringLength(500)]
    public string ProjectContext { get; set; } = string.Empty;

    /// <summary>
    /// Source of the document (file upload, manual entry, etc.)
    /// </summary>
    public DocumentSource Source { get; set; } = DocumentSource.ManualEntry;

    /// <summary>
    /// File format/type
    /// </summary>
    public string FileFormat { get; set; } = "text";

    /// <summary>
    /// Size of the content in characters
    /// </summary>
    public int ContentLength => Content.Length;

    /// <summary>
    /// Additional metadata extracted from content
    /// </summary>
    public DocumentMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Tags associated with the document
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Validation status of the document
    /// </summary>
    public DocumentValidationStatus ValidationStatus { get; set; } = DocumentValidationStatus.NotValidated;

    /// <summary>
    /// Version number for tracking changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// User who created/uploaded the document
    /// </summary>
    [StringLength(100)]
    public string CreatedBy { get; set; } = "System";

    /// <summary>
    /// Additional custom properties
    /// </summary>
    public Dictionary<string, object> CustomProperties { get; set; } = new();

    /// <summary>
    /// Creates a preview of the document content
    /// </summary>
    /// <param name="maxLength">Maximum length of preview</param>
    /// <returns>Content preview</returns>
    public string GetContentPreview(int maxLength = 200)
    {
        if (Content.Length <= maxLength)
            return Content;

        return Content.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Updates the last modified timestamp
    /// </summary>
    public void Touch()
    {
        LastModified = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Validates the document structure and content
    /// </summary>
    /// <returns>List of validation errors</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(FileName))
            errors.Add("FileName is required");

        if (string.IsNullOrWhiteSpace(Content))
            errors.Add("Content is required");

        if (Content.Length > 50000) // 50KB limit
            errors.Add("Content exceeds maximum allowed size");

        if (string.IsNullOrWhiteSpace(ProjectContext))
            errors.Add("ProjectContext should be provided for better test generation");

        return errors;
    }
}

/// <summary>
/// Document metadata extracted from content
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// URLs found in the document
    /// </summary>
    public List<string> Urls { get; set; } = new();

    /// <summary>
    /// Email addresses found in the document
    /// </summary>
    public List<string> EmailAddresses { get; set; } = new();

    /// <summary>
    /// Keywords extracted from content
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Actions identified in the content
    /// </summary>
    public List<string> Actions { get; set; } = new();

    /// <summary>
    /// Test-related entities found
    /// </summary>
    public List<string> TestEntities { get; set; } = new();

    /// <summary>
    /// Estimated complexity score (1-10)
    /// </summary>
    public int ComplexityScore { get; set; } = 1;

    /// <summary>
    /// Language detected in the content
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Sentiment analysis result
    /// </summary>
    public string Sentiment { get; set; } = "neutral";
}

/// <summary>
/// Document validation result
/// </summary>
public class DocumentValidationResult
{
    /// <summary>
    /// Whether the document is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Quality score (0-100)
    /// </summary>
    public int QualityScore { get; set; }

    /// <summary>
    /// Issues found during validation
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Suggestions for improvement
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Warnings that don't prevent usage
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Source of the document
/// </summary>
public enum DocumentSource
{
    ManualEntry,
    FileUpload,
    WebImport,
    ApiImport,
    EmailImport
}

/// <summary>
/// Validation status of the document
/// </summary>
public enum DocumentValidationStatus
{
    NotValidated,
    Valid,
    Invalid,
    Warning
}