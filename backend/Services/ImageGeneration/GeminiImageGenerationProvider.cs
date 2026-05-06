using System.Text;
using System.Text.Json;
using backend.Interfaces;
using Microsoft.Extensions.Options;

namespace backend.Services.ImageGeneration;

/// <summary>
/// Google Gemini image generation provider.
/// </summary>
public class GeminiImageGenerationProvider : IImageGenerationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ImageGenerationOptions _options;
    private readonly ILogger<GeminiImageGenerationProvider> _logger;

    public string ProviderName => "Gemini";

    public GeminiImageGenerationProvider(
        HttpClient httpClient,
        IOptions<ImageGenerationOptions> options,
        ILogger<GeminiImageGenerationProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Gemini API key is not configured. Image generation will fail.");
        }
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new ImageGenerationResult(false, null, null, "Image generation is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new ImageGenerationResult(false, null, null, "Gemini API key is missing.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new ImageGenerationResult(false, null, null, "Prompt is empty.");
        }

        try
        {
            var url = BuildEndpointUrl();
            var body = BuildRequestBody(request.Prompt);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            };

            // Always use header-based auth for security (API keys in query strings can leak via logs/metrics)
            httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var safeError = payload.Length > 200 ? payload[..200] + "..." : payload;
                _logger.LogError(
                    "Gemini Image API error: {StatusCode} - {Error}",
                    response.StatusCode,
                    safeError);
                return new ImageGenerationResult(false, null, null, "Gemini image generation failed.");
            }

            using var doc = JsonDocument.Parse(payload);
            if (!TryExtractImage(doc.RootElement, out var base64, out var mimeType))
            {
                _logger.LogWarning("Gemini response did not include image data.");
                return new ImageGenerationResult(false, null, null, "Gemini returned no image data.");
            }

            var imageBytes = Convert.FromBase64String(base64);
            var finalMimeType = string.IsNullOrWhiteSpace(mimeType)
                ? _options.DefaultMimeType
                : mimeType;

            return new ImageGenerationResult(true, imageBytes, finalMimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image from Gemini.");
            return new ImageGenerationResult(false, null, null, "Image generation failed.");
        }
    }

    private string BuildEndpointUrl()
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var model = Uri.EscapeDataString(_options.Model);
        var endpoint = _options.Endpoint.TrimStart(':');
        return $"{baseUrl}/models/{model}:{endpoint}";
    }

    private Dictionary<string, object?> BuildRequestBody(string prompt)
    {
        var body = new Dictionary<string, object?>
        {
            ["contents"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["text"] = prompt
                        }
                    }
                }
            }
        };

        var responseModalities = _options.ResponseModalities;
        if (responseModalities is { Length: > 0 })
        {
            body["generationConfig"] = new Dictionary<string, object?>
            {
                ["responseModalities"] = responseModalities
            };
        }

        return body;
    }

    private static bool TryExtractImage(
        JsonElement root,
        out string base64,
        out string? mimeType)
    {
        if (TryExtractFromCandidates(root, out base64, out mimeType))
        {
            return true;
        }

        if (TryExtractFromImages(root, out base64, out mimeType))
        {
            return true;
        }

        if (TryExtractFromPredictions(root, out base64, out mimeType))
        {
            return true;
        }

        base64 = string.Empty;
        mimeType = null;
        return false;
    }

    private static bool TryExtractFromCandidates(
        JsonElement root,
        out string base64,
        out string? mimeType)
    {
        if (TryGetProperty(root, "candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!TryGetProperty(candidate, "content", out var content) ||
                    !TryGetProperty(content, "parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (TryReadInlineData(part, out base64, out mimeType))
                    {
                        return true;
                    }
                }
            }
        }

        base64 = string.Empty;
        mimeType = null;
        return false;
    }

    private static bool TryExtractFromImages(
        JsonElement root,
        out string base64,
        out string? mimeType)
    {
        if (TryGetProperty(root, "images", out var images) &&
            images.ValueKind == JsonValueKind.Array)
        {
            foreach (var image in images.EnumerateArray())
            {
                if (TryGetProperty(image, "data", out var data) &&
                    data.ValueKind == JsonValueKind.String)
                {
                    base64 = data.GetString() ?? string.Empty;
                    mimeType = TryGetStringProperty(image, "mimeType", "mime_type")
                        ?? TryGetStringProperty(root, "mimeType", "mime_type");
                    return !string.IsNullOrWhiteSpace(base64);
                }

                if (TryGetProperty(image, "b64_json", out var b64Json) &&
                    b64Json.ValueKind == JsonValueKind.String)
                {
                    base64 = b64Json.GetString() ?? string.Empty;
                    mimeType = TryGetStringProperty(image, "mimeType", "mime_type");
                    return !string.IsNullOrWhiteSpace(base64);
                }
            }
        }

        base64 = string.Empty;
        mimeType = null;
        return false;
    }

    private static bool TryExtractFromPredictions(
        JsonElement root,
        out string base64,
        out string? mimeType)
    {
        if (TryGetProperty(root, "predictions", out var predictions) &&
            predictions.ValueKind == JsonValueKind.Array)
        {
            foreach (var prediction in predictions.EnumerateArray())
            {
                if (TryGetProperty(prediction, "bytesBase64Encoded", out var bytes) &&
                    bytes.ValueKind == JsonValueKind.String)
                {
                    base64 = bytes.GetString() ?? string.Empty;
                    mimeType = TryGetStringProperty(prediction, "mimeType", "mime_type");
                    return !string.IsNullOrWhiteSpace(base64);
                }
            }
        }

        base64 = string.Empty;
        mimeType = null;
        return false;
    }

    private static bool TryReadInlineData(
        JsonElement part,
        out string base64,
        out string? mimeType)
    {
        if ((TryGetProperty(part, "inlineData", out var inlineData) ||
             TryGetProperty(part, "inline_data", out inlineData)) &&
            TryGetProperty(inlineData, "data", out var data) &&
            data.ValueKind == JsonValueKind.String)
        {
            base64 = data.GetString() ?? string.Empty;
            mimeType = TryGetStringProperty(inlineData, "mimeType", "mime_type");
            return !string.IsNullOrWhiteSpace(base64);
        }

        base64 = string.Empty;
        mimeType = null;
        return false;
    }

    private static string? TryGetStringProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
