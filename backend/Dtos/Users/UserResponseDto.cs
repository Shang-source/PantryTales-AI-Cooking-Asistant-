namespace backend.Dtos.Users;

public sealed record UserResponseDto(
    Guid Id,
    string ClerkUserId,
    string Email,
    string Nickname
);
