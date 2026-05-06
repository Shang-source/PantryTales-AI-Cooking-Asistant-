using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Users;

public sealed record UpdateUserPreferenceDto(
    [property: Range(1, int.MaxValue)]
    int TagId,

    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    UserPreferenceRelation Relation
);
