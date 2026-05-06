using backend.Dtos;
using backend.Dtos.Households;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/households")]
[Authorize]
public class HouseholdsController(IHouseholdService householdService, ILogger<HouseholdsController> logger)
    : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<HouseholdMembershipListDto>>> GetMembershipsAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected household membership lookup: {Reason}",
                failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await householdService.GetMembershipsAsync(clerkUserId!, cancellationToken);
        if (result is null)
        {
            return Unauthorized(ApiResponse.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<HouseholdMembershipListDto>.Success(result));
    }

    /// <summary>
    /// Get all members and pending invitations for a household.
    /// </summary>
    [HttpGet("{householdId:guid}/members")]
    public async Task<ActionResult<ApiResponse<HouseholdMembersListDto>>> GetMembersAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected household members lookup: {Reason}",
                failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await householdService.GetHouseholdMembersAsync(householdId, clerkUserId!, cancellationToken);
        return result.Status switch
        {
            HouseholdMembersResultStatus.Success =>
                Ok(ApiResponse<HouseholdMembersListDto>.Success(result.Data!)),
            HouseholdMembersResultStatus.HouseholdNotFound =>
                NotFound(ApiResponse.Fail(404, "Household not found.")),
            HouseholdMembersResultStatus.UserNotFound =>
                Unauthorized(ApiResponse.Fail(401, "User not found.")),
            HouseholdMembersResultStatus.NotMember =>
                StatusCode(403, ApiResponse.Fail(403, "You are not a member of this household.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not load members."))
        };
    }

    [HttpDelete("{householdId:guid}/members/me")]
    public async Task<ActionResult<ApiResponse<LeaveHouseholdResponseDto>>> LeaveAsync(Guid householdId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected leave household request: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await householdService.LeaveHouseholdAsync(householdId, clerkUserId!, cancellationToken);
        return result.Status switch
        {
            HouseholdLeaveResultStatus.HouseholdNotFound => NotFound(ApiResponse.Fail(404, "Household not found.")),
            HouseholdLeaveResultStatus.UserNotFound => Unauthorized(ApiResponse.Fail(401, "User not found.")),
            HouseholdLeaveResultStatus.NotMember => StatusCode(403,
                ApiResponse.Fail(403, "You are not a member of this household.")),
            HouseholdLeaveResultStatus.OwnerCannotLeave => BadRequest(ApiResponse.Fail(400, "Owner cannot leave household.")),
            _ => Ok(ApiResponse<LeaveHouseholdResponseDto>.Success(new LeaveHouseholdResponseDto(
                result.ActiveHouseholdId!.Value,
                result.ActiveHouseholdName!,
                result.IsNewlyCreatedDefault)))
        };
    }

    /// <summary>
    /// Remove a member or cancel a pending invitation from the household.
    /// Only the household owner can perform this action.
    /// </summary>
    [HttpDelete("{householdId:guid}/members/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMemberAsync(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected remove member request: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await householdService.RemoveMemberAsync(householdId, memberId, clerkUserId!, cancellationToken);
        return result.Status switch
        {
            RemoveMemberResultStatus.Success => Ok(ApiResponse.Success()),
            RemoveMemberResultStatus.HouseholdNotFound => NotFound(ApiResponse.Fail(404, "Household not found.")),
            RemoveMemberResultStatus.UserNotFound => Unauthorized(ApiResponse.Fail(401, "User not found.")),
            RemoveMemberResultStatus.NotOwner => StatusCode(403, ApiResponse.Fail(403, "Only the owner can remove members.")),
            RemoveMemberResultStatus.MemberNotFound => NotFound(ApiResponse.Fail(404, "Member or invitation not found.")),
            RemoveMemberResultStatus.CannotRemoveOwner => BadRequest(ApiResponse.Fail(400, "Cannot remove the owner from the household.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not remove member."))
        };
    }
}
