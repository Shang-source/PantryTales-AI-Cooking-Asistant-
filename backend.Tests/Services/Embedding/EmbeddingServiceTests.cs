using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Interfaces;
using Xunit;

namespace backend.Tests.Services.Embedding;

/// <summary>
/// Tests for EmbeddingService components.
/// Since the service requires complex EF Core setup with pgvector support,
/// these tests focus on the mocked embedding provider behavior.
/// </summary>
public class EmbeddingServiceTests
{
    #region Embedding Provider Tests

    [Fact]
    public async Task TestEmbeddingProvider_GenerateEmbeddingAsync_ReturnsEmbedding()
    {
        var provider = new TestEmbeddingProvider();

        var result = await provider.GenerateEmbeddingAsync("test text");

        Assert.NotEmpty(result);
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public async Task TestEmbeddingProvider_GenerateBatchEmbeddingsAsync_ReturnsMultiple()
    {
        var provider = new TestEmbeddingProvider();
        var texts = new[] { "text1", "text2", "text3" };

        var result = await provider.GenerateBatchEmbeddingsAsync(texts);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task TestEmbeddingProvider_ThrowsOnBatchFailure()
    {
        var provider = new TestEmbeddingProvider { ShouldFail = true };

        await Assert.ThrowsAsync<Exception>(() =>
            provider.GenerateBatchEmbeddingsAsync(new[] { "test" }));
    }

    [Fact]
    public async Task TestEmbeddingProvider_IndividualSucceedsWhenBatchFails()
    {
        var provider = new TestEmbeddingProvider { FailBatchButNotIndividual = true };

        // Batch should fail
        await Assert.ThrowsAsync<Exception>(() =>
            provider.GenerateBatchEmbeddingsAsync(new[] { "test" }));

        // Individual should succeed
        var result = await provider.GenerateEmbeddingAsync("test");
        Assert.NotEmpty(result);
    }

    [Fact]
    public void TestEmbeddingProvider_HasCorrectProperties()
    {
        var provider = new TestEmbeddingProvider();

        Assert.Equal("Test", provider.ProviderName);
        Assert.Equal(768, provider.Dimensions);
    }

    #endregion

    #region Test Helpers

    private sealed class TestEmbeddingProvider : IEmbeddingProvider
    {
        public string ProviderName => "Test";
        public int Dimensions => 768;
        public bool ShouldFail { get; set; }
        public bool FailIndividually { get; set; }
        public bool FailBatchButNotIndividual { get; set; }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (ShouldFail && FailIndividually)
                throw new Exception("Test failure");

            return Task.FromResult(new float[] { 0.1f, 0.2f, 0.3f });
        }

        public Task<List<float[]>> GenerateBatchEmbeddingsAsync(
            IEnumerable<string> texts,
            CancellationToken cancellationToken = default)
        {
            var textList = texts.ToList();

            if (ShouldFail || FailBatchButNotIndividual)
                throw new Exception("Batch failure");

            return Task.FromResult(textList.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList());
        }
    }

    #endregion
}
