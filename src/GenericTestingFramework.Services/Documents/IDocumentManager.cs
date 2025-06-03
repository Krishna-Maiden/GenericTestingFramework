using GenericTestingFramework.Services.Documents.Models;

namespace GenericTestingFramework.Services.Documents;

/// <summary>
/// Interface for managing user story documents and content processing
/// </summary>
public interface IDocumentManager
{
    /// <summary>
    /// Uploads a user story file and processes its content
    /// </summary>
    /// <param name="filePath">Path to the user story file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed user story document</returns>
    Task<UserStoryDocument> UploadUserStory(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a user story document from text input
    /// </summary>
    /// <param name="userStoryText">User story content as text</param>
    /// <param name="projectContext">Optional project context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user story document</returns>
    Task<UserStoryDocument> CreateUserStoryFromText(string userStoryText, string projectContext = "", CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user story documents by project filter
    /// </summary>
    /// <param name="projectId">Project identifier (empty for all projects)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching user story documents</returns>
    Task<List<UserStoryDocument>> GetUserStories(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches user story documents by content and metadata
    /// </summary>
    /// <param name="searchQuery">Search query for content</param>
    /// <param name="projectId">Optional project filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching documents</returns>
    Task<List<UserStoryDocument>> SearchUserStories(string searchQuery, string projectId = "", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific user story document by ID
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User story document or null if not found</returns>
    Task<UserStoryDocument?> GetUserStoryById(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user story document
    /// </summary>
    /// <param name="document">Updated document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update successful</returns>
    Task<bool> UpdateUserStory(UserStoryDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user story document
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deletion successful</returns>
    Task<bool> DeleteUserStory(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates user story document content and format
    /// </summary>
    /// <param name="document">Document to validate</param>
    /// <returns>Validation result with issues and suggestions</returns>
    Task<DocumentValidationResult> ValidateUserStory(UserStoryDocument document);

    /// <summary>
    /// Extracts metadata from user story content
    /// </summary>
    /// <param name="content">User story content</param>
    /// <returns>Extracted metadata</returns>
    Task<DocumentMetadata> ExtractMetadata(string content);
}