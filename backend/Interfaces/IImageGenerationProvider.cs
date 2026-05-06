namespace backend.Interfaces;

/// <summary>
/// Provider-agnostic interface for AI image generation.
/// </summary>
public interface IImageGenerationProvider
{
    /// <summary>
    /// Name of the provider (e.g., "OpenAI").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Generate an image based on the provided prompt.
    /// </summary>
    Task<ImageGenerationResult> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Image generation request.
/// </summary>
public record ImageGenerationRequest(
    string Prompt);

/// <summary>
/// Result of image generation.
/// </summary>
public record ImageGenerationResult(
    bool Success,
    byte[]? ImageData,
    string? MimeType,
    string? ErrorMessage = null);
