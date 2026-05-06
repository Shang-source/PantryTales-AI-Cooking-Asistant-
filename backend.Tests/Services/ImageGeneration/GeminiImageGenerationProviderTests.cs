using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using backend.Interfaces;
using backend.Services.ImageGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace backend.Tests.Services.ImageGeneration;

public class GeminiImageGenerationProviderTests
{
    private const string TestApiKey = "test-gemini-api-key";
    private const string TestModel = "gemini-2.0-flash";
    private const string TestBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    private static ImageGenerationOptions CreateOptions(
        bool enabled = true,
        string? apiKey = TestApiKey,
        string? model = null,
        string? baseUrl = null)
    {
        return new ImageGenerationOptions
        {
            Enabled = enabled,
            ApiKey = apiKey ?? string.Empty,
            Model = model ?? TestModel,
            BaseUrl = baseUrl ?? TestBaseUrl,
            Endpoint = "generateContent",
            ResponseModalities = ["IMAGE"],
            DefaultMimeType = "image/png"
        };
    }

    [Fact]
    public void ProviderName_ReturnsGemini()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        Assert.Equal("Gemini", provider.ProviderName);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsError_WhenDisabled()
    {
        var handler = new MockHttpMessageHandler(
            (Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new Exception("Should not be called")));
        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions(enabled: false)),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test"));

        Assert.False(result.Success);
        Assert.Equal("Image generation is disabled.", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsError_WhenApiKeyMissing()
    {
        var handler = new MockHttpMessageHandler(
            (Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new Exception("Should not be called")));
        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions(apiKey: string.Empty)),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test"));

        Assert.False(result.Success);
        Assert.Equal("Gemini API key is missing.", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsError_WhenPromptEmpty()
    {
        var handler = new MockHttpMessageHandler(
            (Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new Exception("Should not be called")));
        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest(""));

        Assert.False(result.Success);
        Assert.Equal("Prompt is empty.", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateImageAsync_UsesXGoogApiKeyHeader()
    {
        var expectedBytes = new byte[] { 1, 2, 3 };
        var base64 = Convert.ToBase64String(expectedBytes);
        string? apiKeyHeader = null;
        string? authHeader = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Headers.TryGetValues("x-goog-api-key", out var apiKeyValues))
            {
                apiKeyHeader = apiKeyValues.FirstOrDefault();
            }
            if (request.Headers.TryGetValues("Authorization", out var authValues))
            {
                authHeader = authValues.FirstOrDefault();
            }

            var payload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = "image/png",
                                        data = base64
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return CreateJsonResponse(payload);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.True(result.Success);
        Assert.Equal(TestApiKey, apiKeyHeader);
        Assert.Null(authHeader); // Should NOT use Bearer auth
    }

    [Fact]
    public async Task GenerateImageAsync_DoesNotAppendApiKeyToQueryString()
    {
        var expectedBytes = new byte[] { 1, 2, 3 };
        var base64 = Convert.ToBase64String(expectedBytes);
        string? requestUrl = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();

            var payload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = "image/png",
                                        data = base64
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return CreateJsonResponse(payload);
        });

        using var httpClient = new HttpClient(handler);
        // Provider should NOT append key to query string
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.True(result.Success);
        Assert.NotNull(requestUrl);
        Assert.DoesNotContain("?key=", requestUrl);
        Assert.DoesNotContain("apiKey", requestUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateImageAsync_ExtractsImageFromCandidatesResponse()
    {
        var expectedBytes = new byte[] { 10, 20, 30, 40 };
        var base64 = Convert.ToBase64String(expectedBytes);

        var handler = new MockHttpMessageHandler(_ =>
        {
            var payload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = "image/jpeg",
                                        data = base64
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return CreateJsonResponse(payload);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.True(result.Success);
        Assert.Equal("image/jpeg", result.MimeType);
        Assert.Equal(expectedBytes, result.ImageData);
    }

    [Fact]
    public async Task GenerateImageAsync_UsesDefaultMimeType_WhenNotProvided()
    {
        var expectedBytes = new byte[] { 5, 10, 15 };
        var base64 = Convert.ToBase64String(expectedBytes);

        var handler = new MockHttpMessageHandler(_ =>
        {
            var payload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    inlineData = new
                                    {
                                        // No mimeType provided
                                        data = base64
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return CreateJsonResponse(payload);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.True(result.Success);
        Assert.Equal("image/png", result.MimeType); // Default from options
        Assert.Equal(expectedBytes, result.ImageData);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsError_WhenNoImageData()
    {
        var handler = new MockHttpMessageHandler(_ =>
        {
            var payload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = Array.Empty<object>()
                        }
                    }
                }
            };
            return CreateJsonResponse(payload);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.False(result.Success);
        Assert.Equal("Gemini returned no image data.", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsError_OnApiFailure()
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"Bad request\"}", Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        var result = await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.False(result.Success);
        Assert.Equal("Gemini image generation failed.", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateImageAsync_BuildsCorrectEndpointUrl()
    {
        var expectedBytes = new byte[] { 1, 2, 3 };
        var base64 = Convert.ToBase64String(expectedBytes);
        string? requestUrl = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();

            var payload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = "image/png",
                                        data = base64
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return CreateJsonResponse(payload);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new GeminiImageGenerationProvider(
            httpClient,
            MsOptions.Create(CreateOptions()),
            NullLogger<GeminiImageGenerationProvider>.Instance);

        await provider.GenerateImageAsync(new ImageGenerationRequest("test prompt"));

        Assert.NotNull(requestUrl);
        Assert.Contains($"/models/{TestModel}:generateContent", requestUrl);
        Assert.StartsWith(TestBaseUrl, requestUrl);
    }

    private static HttpResponseMessage CreateJsonResponse(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerAsync;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handlerAsync = request => Task.FromResult(handler(request));
        }

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerAsync)
        {
            _handlerAsync = handlerAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _handlerAsync(request);
    }
}
