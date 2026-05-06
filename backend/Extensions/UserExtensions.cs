using System;
using backend.Dtos.Users;
using backend.Models;

namespace backend.Extensions;

public static class UserExtensions
{
    public static UserResponseDto ToDto(this User user)
        => new(user.Id, user.ClerkUserId, user.Email, user.Nickname);
    public static UserProfileResponseDto ToDetailDto(this User user)
    {
        var preferences = user.Preferences
            .OrderBy(p => p.Relation)
            .ThenBy(p => p.Tag?.DisplayName)
            .Select(p => p.ToPreferenceDto())
            .ToList();

        return new UserProfileResponseDto(
            user.Id,
            user.ClerkUserId,
            user.Email,
            user.Nickname,
            user.AvatarUrl,
            user.Age,
            user.Gender,
            user.Height,
            user.Weight,
            user.CreatedAt,
            user.UpdatedAt,
            preferences);
    }

    public static UserPreferenceDto ToPreferenceDto(this UserPreference preference)
    {
        var tag = preference.Tag;
        return new UserPreferenceDto(
            preference.Relation,
            preference.TagId,
            tag?.Name ?? string.Empty,
            tag?.DisplayName ?? string.Empty,
            tag?.Type ?? string.Empty,
            tag?.Icon,
            tag?.Color);
    }
}
