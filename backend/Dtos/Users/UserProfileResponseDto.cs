using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Users;

public sealed record UserProfileResponseDto(
    Guid Id,
    string ClerkUserId,
    string Email,
    string Nickname,
    string? AvatarUrl,
    int? Age,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    UserGender? Gender,
    decimal? Height,
    decimal? Weight,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<UserPreferenceDto> Preferences
);
