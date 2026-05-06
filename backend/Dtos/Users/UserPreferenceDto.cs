using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Users;

public sealed record UserPreferenceDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    UserPreferenceRelation Relation,

    int TagId,
    string TagName,
    string TagDisplayName,
    string TagType,
    string? TagIcon,
    string? TagColor
);