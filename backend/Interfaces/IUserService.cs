using backend.Dtos.Users;

namespace backend.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> GetOrCreateAsync(UserSyncPayload payload, CancellationToken cancellationToken = default);
    Task<UserResponseDto?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default);
    Task<UserProfileResponseDto?> GetProfileAsync(string clerkUserId, CancellationToken cancellationToken = default);
    Task<UserProfileResponseDto?> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default);
}
