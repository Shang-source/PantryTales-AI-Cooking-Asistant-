using backend.Auth;
using backend.Dtos;
using backend.Dtos.Normalization;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin/name-normalization")]
[RequireAdmin]
public class NameNormalizationAdminController(
    INameNormalizationRepository repository,
    ILogger<NameNormalizationAdminController> logger) : ControllerBase
{
    /// <summary>
    /// List all normalization tokens, optionally filtered by category and active status.
    /// </summary>
    [HttpGet("tokens")]
    public async Task<ActionResult<ApiResponse<List<NameNormalizationTokenDto>>>> ListTokensAsync(
        [FromQuery] NameNormalizationTokenCategory? category,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var tokens = await repository.GetTokensAsync(category, isActive, cancellationToken);
        var dtos = tokens.Select(ToDto).ToList();
        return Ok(ApiResponse<List<NameNormalizationTokenDto>>.Success(dtos));
    }

    /// <summary>
    /// Get a single token by ID.
    /// </summary>
    [HttpGet("tokens/{id:long}")]
    public async Task<ActionResult<ApiResponse<NameNormalizationTokenDto>>> GetTokenAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var token = await repository.GetTokenByIdAsync(id, cancellationToken);
        if (token is null)
        {
            return NotFound(ApiResponse.Fail(404, "Token not found."));
        }

        return Ok(ApiResponse<NameNormalizationTokenDto>.Success(ToDto(token)));
    }

    /// <summary>
    /// Create a new normalization token.
    /// </summary>
    [HttpPost("tokens")]
    public async Task<ActionResult<ApiResponse<NameNormalizationTokenDto>>> CreateTokenAsync(
        [FromBody] CreateTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedToken = request.Token.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return BadRequest(ApiResponse.Fail(400, "Token cannot be empty."));
        }

        // Validate regex pattern if IsRegex is true
        if (request.IsRegex && !IsValidRegex(normalizedToken))
        {
            return BadRequest(ApiResponse.Fail(400, "Invalid regex pattern."));
        }

        var exists = await repository.TokenExistsAsync(normalizedToken, cancellationToken);
        if (exists)
        {
            return Conflict(ApiResponse.Fail(409, $"Token '{normalizedToken}' already exists."));
        }

        var token = new NameNormalizationToken
        {
            Token = normalizedToken,
            Category = request.Category,
            IsRegex = request.IsRegex,
            Language = request.Language?.Trim(),
            Source = request.Source?.Trim()
        };

        await repository.AddTokenAsync(token, cancellationToken);
        await repository.IncrementDictionaryVersionAsync(cancellationToken);

        logger.LogInformation("Created normalization token {TokenId}: '{Token}' ({Category}).",
            token.Id, token.Token, token.Category);

        return CreatedAtAction("GetToken", new { id = token.Id },
            ApiResponse<NameNormalizationTokenDto>.Success(ToDto(token), code: 201, message: "Created"));
    }

    /// <summary>
    /// Update an existing normalization token.
    /// </summary>
    [HttpPut("tokens/{id:long}")]
    public async Task<ActionResult<ApiResponse<NameNormalizationTokenDto>>> UpdateTokenAsync(
        long id,
        [FromBody] UpdateTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        var token = await repository.GetTokenByIdAsync(id, cancellationToken);
        if (token is null)
        {
            return NotFound(ApiResponse.Fail(404, "Token not found."));
        }

        var normalizedToken = request.Token.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return BadRequest(ApiResponse.Fail(400, "Token cannot be empty."));
        }

        // Validate regex pattern if IsRegex is true
        if (request.IsRegex && !IsValidRegex(normalizedToken))
        {
            return BadRequest(ApiResponse.Fail(400, "Invalid regex pattern."));
        }

        // Check for duplicate if token value changed
        if (!string.Equals(token.Token, normalizedToken, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await repository.TokenExistsAsync(normalizedToken, cancellationToken);
            if (exists)
            {
                return Conflict(ApiResponse.Fail(409, $"Token '{normalizedToken}' already exists."));
            }
        }

        token.Token = normalizedToken;
        token.Category = request.Category;
        token.IsActive = request.IsActive;
        token.IsRegex = request.IsRegex;
        token.Language = request.Language?.Trim();

        await repository.UpdateTokenAsync(token, cancellationToken);
        await repository.IncrementDictionaryVersionAsync(cancellationToken);

        logger.LogInformation("Updated normalization token {TokenId}.", token.Id);

        return Ok(ApiResponse<NameNormalizationTokenDto>.Success(ToDto(token)));
    }

    /// <summary>
    /// Delete a normalization token.
    /// </summary>
    [HttpDelete("tokens/{id:long}")]
    public async Task<IActionResult> DeleteTokenAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var token = await repository.GetTokenByIdAsync(id, cancellationToken);
        if (token is null)
        {
            return NotFound(ApiResponse.Fail(404, "Token not found."));
        }

        await repository.DeleteTokenAsync(token, cancellationToken);
        await repository.IncrementDictionaryVersionAsync(cancellationToken);

        logger.LogInformation("Deleted normalization token {TokenId}: '{Token}'.", token.Id, token.Token);

        return NoContent();
    }

    /// <summary>
    /// Bulk import normalization tokens.
    /// </summary>
    [HttpPost("tokens/bulk")]
    public async Task<ActionResult<ApiResponse<BulkImportTokensResponseDto>>> BulkImportTokensAsync(
        [FromBody] BulkImportTokensRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Tokens.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "Tokens list cannot be empty."));
        }

        if (request.Tokens.Count > 1000)
        {
            return BadRequest(ApiResponse.Fail(400, "Cannot import more than 1000 tokens at once."));
        }

        var tokens = request.Tokens.Select(t => new NameNormalizationToken
        {
            Token = t.Token.Trim().ToLowerInvariant(),
            Category = t.Category,
            IsRegex = t.IsRegex,
            Language = t.Language?.Trim(),
            Source = t.Source?.Trim()
        }).ToList();

        var (created, skipped) = await repository.AddTokensAsync(tokens, cancellationToken);

        if (created > 0)
        {
            await repository.IncrementDictionaryVersionAsync(cancellationToken);
        }

        logger.LogInformation("Bulk imported {Created} tokens, skipped {Skipped} duplicates.", created, skipped);

        return Ok(ApiResponse<BulkImportTokensResponseDto>.Success(
            new BulkImportTokensResponseDto(created, skipped)));
    }

    /// <summary>
    /// Get the current dictionary and algorithm versions.
    /// </summary>
    [HttpGet("version")]
    public async Task<ActionResult<ApiResponse<NormalizationVersionDto>>> GetVersionAsync(
        CancellationToken cancellationToken)
    {
        var version = await repository.GetVersionAsync(cancellationToken);
        var dto = new NormalizationVersionDto(
            version.DictionaryVersion,
            version.AlgorithmVersion,
            version.UpdatedAt);
        return Ok(ApiResponse<NormalizationVersionDto>.Success(dto));
    }

    /// <summary>
    /// Manually bump the dictionary version (forces re-normalization of all items).
    /// </summary>
    [HttpPost("version/bump")]
    public async Task<ActionResult<ApiResponse<NormalizationVersionDto>>> BumpVersionAsync(
        CancellationToken cancellationToken)
    {
        await repository.IncrementDictionaryVersionAsync(cancellationToken);
        var version = await repository.GetVersionAsync(cancellationToken);

        logger.LogInformation("Dictionary version bumped to {Version}.", version.DictionaryVersion);

        var dto = new NormalizationVersionDto(
            version.DictionaryVersion,
            version.AlgorithmVersion,
            version.UpdatedAt);
        return Ok(ApiResponse<NormalizationVersionDto>.Success(dto, message: "Version bumped."));
    }

    private static NameNormalizationTokenDto ToDto(NameNormalizationToken token) =>
        new(token.Id, token.Token, token.Category, token.IsActive, token.IsRegex,
            token.Language, token.Source, token.CreatedAt, token.UpdatedAt);

    private static bool IsValidRegex(string pattern)
    {
        try
        {
            _ = new System.Text.RegularExpressions.Regex(pattern,
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromMilliseconds(100));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
