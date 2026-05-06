using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Interactions;

public sealed record LogInteractionRequestDto(
    Guid RecipeId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RecipeInteractionEventType EventType,
    string? Source = null,
    string? SessionId = null,
    int? DwellSeconds = null);

public sealed record LogImpressionsRequestDto(
    List<Guid> RecipeIds,
    string? Source = null,
    string? SessionId = null);

public sealed record RecipeInteractionStatsDto(
    Guid RecipeId,
    int Impressions,
    int Clicks,
    int Opens,
    int Saves,
    int Likes,
    int Cooks,
    int Shares,
    double ClickThroughRate,
    int DaysIncluded);
