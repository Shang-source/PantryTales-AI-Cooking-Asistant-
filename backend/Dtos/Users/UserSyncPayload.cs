namespace backend.Dtos.Users;

public sealed record UserSyncPayload(
    string ClerkUserId,
    string Email,
    string Nickname
);
