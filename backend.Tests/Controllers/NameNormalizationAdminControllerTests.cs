using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers.Admin;
using backend.Dtos;
using backend.Dtos.Normalization;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class NameNormalizationAdminControllerTests
{
    private readonly TestRepository _repository;
    private readonly NameNormalizationAdminController _controller;

    public NameNormalizationAdminControllerTests()
    {
        _repository = new TestRepository();
        _controller = new NameNormalizationAdminController(
            _repository,
            NullLogger<NameNormalizationAdminController>.Instance);
    }

    #region ListTokens Tests

    [Fact]
    public async Task ListTokens_ReturnsAllTokens()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "test1" });
        _repository.Tokens.Add(new NameNormalizationToken { Id = 2, Token = "test2" });

        var result = await _controller.ListTokensAsync(null, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<NameNormalizationTokenDto>>>(okResult.Value);
        Assert.Equal(2, response.Data!.Count);
    }

    [Fact]
    public async Task ListTokens_FiltersByCategory()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "brand1", Category = NameNormalizationTokenCategory.Brand });
        _repository.Tokens.Add(new NameNormalizationToken { Id = 2, Token = "unit1", Category = NameNormalizationTokenCategory.Unit });

        var result = await _controller.ListTokensAsync(NameNormalizationTokenCategory.Brand, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<NameNormalizationTokenDto>>>(okResult.Value);
        Assert.Single(response.Data!);
        Assert.Equal("brand1", response.Data![0].Token);
    }

    #endregion

    #region GetToken Tests

    [Fact]
    public async Task GetToken_ReturnsToken_WhenExists()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "test" });

        var result = await _controller.GetTokenAsync(1, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NameNormalizationTokenDto>>(okResult.Value);
        Assert.Equal("test", response.Data!.Token);
    }

    [Fact]
    public async Task GetToken_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.GetTokenAsync(999, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFoundResult.Value);
        Assert.Equal(404, response.Code);
    }

    #endregion

    #region CreateToken Tests

    [Fact]
    public async Task CreateToken_CreatesNewToken()
    {
        var request = new CreateTokenRequestDto("NewToken", NameNormalizationTokenCategory.Brand, false, null, null);

        var result = await _controller.CreateTokenAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NameNormalizationTokenDto>>(createdResult.Value);
        Assert.Equal("newtoken", response.Data!.Token); // Lowercased and trimmed
        Assert.Single(_repository.Tokens);
    }

    [Fact]
    public async Task CreateToken_ReturnsBadRequest_WhenTokenEmpty()
    {
        var request = new CreateTokenRequestDto("   ", NameNormalizationTokenCategory.Brand, false, null, null);

        var result = await _controller.CreateTokenAsync(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequestResult.Value);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task CreateToken_ReturnsConflict_WhenDuplicate()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "existing" });

        var request = new CreateTokenRequestDto("Existing", NameNormalizationTokenCategory.Brand, false, null, null);

        var result = await _controller.CreateTokenAsync(request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(conflictResult.Value);
        Assert.Equal(409, response.Code);
    }

    [Fact]
    public async Task CreateToken_IncrementsVersion()
    {
        var initialVersion = _repository.Version.DictionaryVersion;
        var request = new CreateTokenRequestDto("NewToken", NameNormalizationTokenCategory.Brand, false, null, null);

        await _controller.CreateTokenAsync(request, CancellationToken.None);

        Assert.Equal(initialVersion + 1, _repository.Version.DictionaryVersion);
    }

    [Fact]
    public async Task CreateToken_ReturnsBadRequest_WhenInvalidRegex()
    {
        // Invalid regex pattern (unbalanced parenthesis)
        var request = new CreateTokenRequestDto("(invalid[", NameNormalizationTokenCategory.Brand, true, null, null);

        var result = await _controller.CreateTokenAsync(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequestResult.Value);
        Assert.Equal(400, response.Code);
        Assert.Contains("regex", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateToken_AcceptsValidRegex()
    {
        var request = new CreateTokenRequestDto(@"\d+\s*(g|ml|kg)\b", NameNormalizationTokenCategory.Unit, true, null, null);

        var result = await _controller.CreateTokenAsync(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    #endregion

    #region UpdateToken Tests

    [Fact]
    public async Task UpdateToken_UpdatesExistingToken()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "old", Category = NameNormalizationTokenCategory.Brand });

        var request = new UpdateTokenRequestDto("updated", NameNormalizationTokenCategory.Unit, true, false, null);

        var result = await _controller.UpdateTokenAsync(1, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NameNormalizationTokenDto>>(okResult.Value);
        Assert.Equal("updated", response.Data!.Token);
        Assert.Equal(NameNormalizationTokenCategory.Unit, response.Data!.Category);
    }

    [Fact]
    public async Task UpdateToken_ReturnsNotFound_WhenNotExists()
    {
        var request = new UpdateTokenRequestDto("updated", NameNormalizationTokenCategory.Brand, true, false, null);

        var result = await _controller.UpdateTokenAsync(999, request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateToken_ReturnsConflict_WhenNewTokenAlreadyExists()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "token1" });
        _repository.Tokens.Add(new NameNormalizationToken { Id = 2, Token = "token2" });

        var request = new UpdateTokenRequestDto("token2", NameNormalizationTokenCategory.Brand, true, false, null);

        var result = await _controller.UpdateTokenAsync(1, request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(409, ((ApiResponse)conflictResult.Value!).Code);
    }

    #endregion

    #region DeleteToken Tests

    [Fact]
    public async Task DeleteToken_DeletesExistingToken()
    {
        _repository.Tokens.Add(new NameNormalizationToken { Id = 1, Token = "test" });

        var result = await _controller.DeleteTokenAsync(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_repository.Tokens);
    }

    [Fact]
    public async Task DeleteToken_ReturnsNotFound_WhenNotExists()
    {
        var result = await _controller.DeleteTokenAsync(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    #endregion

    #region BulkImport Tests

    [Fact]
    public async Task BulkImport_CreatesMultipleTokens()
    {
        var request = new BulkImportTokensRequestDto(new List<CreateTokenRequestDto>
        {
            new("token1", NameNormalizationTokenCategory.Brand, false, null, null),
            new("token2", NameNormalizationTokenCategory.Unit, false, null, null)
        });

        var result = await _controller.BulkImportTokensAsync(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<BulkImportTokensResponseDto>>(okResult.Value);
        Assert.Equal(2, response.Data!.Created);
        Assert.Equal(0, response.Data!.Skipped);
    }

    [Fact]
    public async Task BulkImport_ReturnsBadRequest_WhenEmpty()
    {
        var request = new BulkImportTokensRequestDto(new List<CreateTokenRequestDto>());

        var result = await _controller.BulkImportTokensAsync(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, ((ApiResponse)badRequestResult.Value!).Code);
    }

    [Fact]
    public async Task BulkImport_ReturnsBadRequest_WhenTooMany()
    {
        var tokens = Enumerable.Range(0, 1001)
            .Select(i => new CreateTokenRequestDto($"token{i}", NameNormalizationTokenCategory.Brand, false, null, null))
            .ToList();
        var request = new BulkImportTokensRequestDto(tokens);

        var result = await _controller.BulkImportTokensAsync(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, ((ApiResponse)badRequestResult.Value!).Code);
    }

    #endregion

    #region Version Tests

    [Fact]
    public async Task GetVersion_ReturnsCurrentVersion()
    {
        _repository.Version.DictionaryVersion = 5;
        _repository.Version.AlgorithmVersion = 2;

        var result = await _controller.GetVersionAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NormalizationVersionDto>>(okResult.Value);
        Assert.Equal(5, response.Data!.DictionaryVersion);
        Assert.Equal(2, response.Data!.AlgorithmVersion);
    }

    [Fact]
    public async Task BumpVersion_IncrementsVersion()
    {
        _repository.Version.DictionaryVersion = 5;

        var result = await _controller.BumpVersionAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NormalizationVersionDto>>(okResult.Value);
        Assert.Equal(6, response.Data!.DictionaryVersion);
    }

    #endregion

    #region Test Repository

    private sealed class TestRepository : INameNormalizationRepository
    {
        public List<NameNormalizationToken> Tokens { get; } = new();
        public NameNormalizationDictionaryVersion Version { get; } = new() { Id = 1, DictionaryVersion = 1, AlgorithmVersion = 1 };

        public Task<List<NameNormalizationToken>> GetTokensAsync(
            NameNormalizationTokenCategory? category = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            var query = Tokens.AsEnumerable();
            if (category.HasValue) query = query.Where(t => t.Category == category.Value);
            if (isActive.HasValue) query = query.Where(t => t.IsActive == isActive.Value);
            return Task.FromResult(query.ToList());
        }

        public Task<NameNormalizationToken?> GetTokenByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens.FirstOrDefault(t => t.Id == id));

        public Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens.Any(t => t.Token.Equals(token, StringComparison.OrdinalIgnoreCase)));

        public Task<NameNormalizationToken> AddTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
        {
            token.Id = Tokens.Count + 1;
            Tokens.Add(token);
            return Task.FromResult(token);
        }

        public Task UpdateTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
        {
            Tokens.Remove(token);
            return Task.CompletedTask;
        }

        public Task<(int Created, int Skipped)> AddTokensAsync(IEnumerable<NameNormalizationToken> tokens, CancellationToken cancellationToken = default)
        {
            var list = tokens.ToList();
            Tokens.AddRange(list);
            return Task.FromResult((list.Count, 0));
        }

        public Task<NameNormalizationDictionaryVersion> GetVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Version);

        public Task IncrementDictionaryVersionAsync(CancellationToken cancellationToken = default)
        {
            Version.DictionaryVersion++;
            return Task.CompletedTask;
        }
    }

    #endregion
}
