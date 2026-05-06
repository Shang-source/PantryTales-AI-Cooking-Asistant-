using backend.Dtos;
using backend.Dtos.Users;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService userService, ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserProfileResponseDto>>> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        logger.LogInformation("UsersController.GetCurrentAsync for ClerkId {ClerkUserId}. AuthType={AuthType}",
            clerkUserId, User.Identity?.AuthenticationType ?? "unknown");

        var profile = await userService.GetProfileAsync(clerkUserId!, cancellationToken);
        if (profile is null && User.TryBuildUserSyncPayload(out var payload, out var syncFailureReason))
        {
            logger.LogInformation("Profile missing for ClerkId {ClerkUserId}; attempting on-demand sync. Reason={Reason}",
                clerkUserId, syncFailureReason ?? "missing local user");
            await userService.GetOrCreateAsync(payload, cancellationToken);
            profile = await userService.GetProfileAsync(clerkUserId!, cancellationToken);
        }

        if (profile is null)
        {
            return NotFound(ApiResponse.Fail(404, "User not found."));
        }

        return Ok(ApiResponse<UserProfileResponseDto>.Success(profile));
    }

    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<UserProfileResponseDto>>> UpdateCurrentAsync(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile update: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }
        var currentUser = await userService.GetByClerkUserIdAsync(clerkUserId!, cancellationToken);

        if (currentUser is null)
        {
            if (User.TryBuildUserSyncPayload(out var payload, out _))
            {
                currentUser = await userService.GetOrCreateAsync(payload, cancellationToken);
            }
            else
            {
                return NotFound(ApiResponse.Fail(404, "User not found."));
            }
        }

        var updated = await userService.UpdateProfileAsync(currentUser.Id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound(ApiResponse.Fail(404, "User not found."));
        }

        return Ok(ApiResponse<UserProfileResponseDto>.Success(updated));
    }
}
