namespace backend.Options;

/// <summary>
/// Configuration options for AI vision services (OpenAI GPT-4o).
/// </summary>
public class VisionOptions
{
    public const string SectionName = "Vision";

    /// <summary>
    /// OpenAI API key for vision services.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for vision. Default: gpt-4o.
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Base URL for OpenAI API. Default: https://api.openai.com/v1.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}
