namespace backend.Services.ImageGeneration;

/// <summary>
/// Configuration options for image generation service (Gemini).
/// </summary>
public class ImageGenerationOptions
{
    public const string SectionName = "ImageGeneration";

    /// <summary>
    /// Gemini API key for image generation.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for image generation. Default: gemini-2.0-flash-preview-image-generation.
    /// </summary>
    public string Model { get; set; } = "gemini-2.0-flash-preview-image-generation";

    /// <summary>
    /// Base URL for Gemini API. Default: https://generativelanguage.googleapis.com/v1beta.
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// API endpoint to call. Default: generateContent.
    /// </summary>
    public string Endpoint { get; set; } = "generateContent";

    /// <summary>
    /// Whether image generation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Response modalities for image generation. Default: ["IMAGE"].
    /// </summary>
    public string[] ResponseModalities { get; set; } = ["IMAGE"];

    /// <summary>
    /// Default MIME type for generated images. Default: image/png.
    /// </summary>
    public string DefaultMimeType { get; set; } = "image/png";
}
