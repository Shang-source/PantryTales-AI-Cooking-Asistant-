using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Users;

public sealed record UpdateUserProfileRequest(
    [MaxLength(64)]
    string? Nickname,

    [Url]
    [MaxLength(512)]
    string? AvatarUrl,

    [Range(0, 120, ErrorMessage = "Age must be between 0 and 120.")]
    int? Age,

    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    UserGender? Gender,

    [Range(typeof(decimal), "0", "300", ErrorMessage = "Height must be between 0 and 300 cm.")]
    decimal? Height,

    [Range(typeof(decimal), "0", "500", ErrorMessage = "Weight must be between 0 and 500 kg.")]
    decimal? Weight,

    IEnumerable<UpdateUserPreferenceDto>? Preferences
);
