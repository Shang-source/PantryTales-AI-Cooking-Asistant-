using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Interfaces;
using Microsoft.Extensions.Options;

namespace backend.Services.Embedding;

/// <summary>
/// OpenAI embedding provider using text-embedding-3-small/large models.
/// </summary>
public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OpenAIEmbeddingProvider> _logger;

    public string ProviderName => "OpenAI";
    public int Dimensions => _options.Dimensions;

    public OpenAIEmbeddingProvider(
        HttpClient httpClient,
        IOptions<EmbeddingOptions> options,
        ILogger<OpenAIEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured. Embedding generation will fail.");
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateBatchEmbeddingsAsync([text], cancellationToken);
        return result.FirstOrDefault() ?? [];
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
        {
            return [];
        }

        try
        {
            var request = new OpenAIEmbeddingRequest
            {
                Model = _options.Model,
                Input = textList,
                Dimensions = _options.Dimensions
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var safeError = errorContent.Length > 200 ? errorContent[..200] + "..." : errorContent;
                _logger.LogError("OpenAI Embedding API error: {StatusCode} - {Error}", response.StatusCode, safeError);
                throw new HttpRequestException($"OpenAI Embedding API error: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(JsonOptions, cancellationToken);

            if (result?.Data == null || result.Data.Count == 0)
            {
                throw new InvalidOperationException("OpenAI returned empty embedding response.");
            }

            // Sort by index to ensure correct order
            var embeddings = result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList();

            _logger.LogDebug("Generated {Count} embeddings with {Dimensions} dimensions", embeddings.Count, Dimensions);

            return embeddings;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex, "Failed to generate embeddings for {Count} texts", textList.Count);
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    #region OpenAI API Models

    private sealed class OpenAIEmbeddingRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("input")] public List<string> Input { get; set; } = [];
        [JsonPropertyName("dimensions")] public int Dimensions { get; set; }
    }

    private sealed class OpenAIEmbeddingResponse
    {
        [JsonPropertyName("data")] public List<OpenAIEmbeddingData> Data { get; set; } = [];
    }

    private sealed class OpenAIEmbeddingData
    {
        [JsonPropertyName("embedding")] public float[] Embedding { get; set; } = [];
        [JsonPropertyName("index")] public int Index { get; set; }
    }

    #endregion
}
