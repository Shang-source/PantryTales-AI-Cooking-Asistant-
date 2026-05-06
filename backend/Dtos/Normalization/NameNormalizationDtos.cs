using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Normalization;

public sealed record NameNormalizationTokenDto(
    long Id,
    string Token,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    NameNormalizationTokenCategory Category,
    bool IsActive,
    bool IsRegex,
    string? Language,
    string? Source,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateTokenRequestDto(
    string Token,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    NameNormalizationTokenCategory Category,
    bool IsRegex = false,
    string? Language = null,
    string? Source = null);

public sealed record UpdateTokenRequestDto(
    string Token,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    NameNormalizationTokenCategory Category,
    bool IsActive,
    bool IsRegex,
    string? Language = null);

public sealed record BulkImportTokensRequestDto(List<CreateTokenRequestDto> Tokens);

public sealed record BulkImportTokensResponseDto(int Created, int Skipped);

public sealed record NormalizationVersionDto(
    long DictionaryVersion,
    int AlgorithmVersion,
    DateTime UpdatedAt);
