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
public class HouseholdInvitationsController(IHouseholdInvitationService invitationService,
    ILogger<HouseholdInvitationsController> logger) : ControllerBase
{
    [HttpPost("{householdId:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<HouseholdInvitationResponseDto>>> InviteAsync(Guid householdId,
        [FromBody] InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected invitation creation: {Reason}", failureReason ?? "Missing Clerk ID.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await invitationService.CreateInvitationAsync(householdId, clerkUserId!, request, cancellationToken);
        return result.Status switch
        {
            HouseholdInvitationCreateStatus.Success =>
                Ok(ApiResponse<HouseholdInvitationResponseDto>.Success(result.Invitation!)),
            HouseholdInvitationCreateStatus.HouseholdNotFound => NotFound(ApiResponse.Fail(404, "Household not found.")),
            HouseholdInvitationCreateStatus.InviterNotFound => Unauthorized(ApiResponse.Fail(401, result.FailureReason ?? "Inviter not found.")),
            HouseholdInvitationCreateStatus.InviterNotOwner => StatusCode(403,
                ApiResponse.Fail(403, result.FailureReason ?? "Only owners can send invitations.")),
            HouseholdInvitationCreateStatus.TargetUserNotFound => NotFound(ApiResponse.Fail(404,
                result.FailureReason ?? "Target user not found.")),
            HouseholdInvitationCreateStatus.TargetAlreadyMember => Conflict(ApiResponse.Fail(409,
                result.FailureReason ?? "User is already a member.")),
            HouseholdInvitationCreateStatus.InvitationPending => Conflict(ApiResponse.Fail(409,
                result.FailureReason ?? "An invitation is already pending for this user.")),
            HouseholdInvitationCreateStatus.TargetIsOwnerWithMembers => Conflict(ApiResponse.Fail(409,
                result.FailureReason ?? "Cannot invite a user who owns a household with other members.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not create invitation."))
        };
    }

    [HttpGet("{householdId:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HouseholdInvitationResponseDto>>>> ListAsync(Guid householdId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected invitation listing: {Reason}", failureReason ?? "Missing Clerk ID.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await invitationService.ListInvitationsAsync(householdId, clerkUserId!, cancellationToken);
        return result.Status switch
        {
            HouseholdInvitationListStatus.Success => Ok(ApiResponse<IReadOnlyList<HouseholdInvitationResponseDto>>.Success(result.Invitations!)),
            HouseholdInvitationListStatus.HouseholdNotFound => NotFound(ApiResponse.Fail(404, "Household not found.")),
            HouseholdInvitationListStatus.InviterNotFound => Unauthorized(ApiResponse.Fail(401, result.FailureReason ?? "Inviter not found.")),
            HouseholdInvitationListStatus.InviterNotOwner => StatusCode(403,
                ApiResponse.Fail(403, result.FailureReason ?? "Only owners can view invitations.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not load invitations."))
        };
    }

    [HttpPost("invitations/{invitationId:guid}/accept")]
    public async Task<ActionResult<ApiResponse<HouseholdMembershipDto>>> AcceptAsync(Guid invitationId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("AcceptAsync called for invitation {InvitationId}", invitationId);

        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected invitation acceptance: {Reason}", failureReason ?? "Missing Clerk ID.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        logger.LogInformation("Processing invitation acceptance for ClerkUserId: {ClerkUserId}, InvitationId: {InvitationId}",
            clerkUserId, invitationId);

        var result = await invitationService.AcceptInvitationAsync(invitationId, clerkUserId!, cancellationToken);

        logger.LogInformation("Invitation acceptance result: Status={Status}, FailureReason={FailureReason}",
            result.Status, result.FailureReason ?? "None");
        return MapAcceptResult(result);
    }

    [HttpPost("{householdId:guid}/invitations/link")]
    public async Task<ActionResult<ApiResponse<HouseholdInvitationResponseDto>>> CreateLinkInvitationAsync(
        Guid householdId,
        [FromBody] CreateLinkInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected link invitation creation: {Reason}", failureReason ?? "Missing Clerk ID.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await invitationService.CreateLinkInvitationAsync(householdId, clerkUserId!, request, cancellationToken);
        return result.Status switch
        {
            HouseholdInvitationCreateStatus.Success =>
                Ok(ApiResponse<HouseholdInvitationResponseDto>.Success(result.Invitation!)),
            HouseholdInvitationCreateStatus.HouseholdNotFound => NotFound(ApiResponse.Fail(404, "Household not found.")),
            HouseholdInvitationCreateStatus.InviterNotFound => Unauthorized(ApiResponse.Fail(401, result.FailureReason ?? "Inviter not found.")),
            HouseholdInvitationCreateStatus.InviterNotOwner => StatusCode(403,
                ApiResponse.Fail(403, result.FailureReason ?? "Only owners can create link invitations.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not create link invitation."))
        };
    }

    [HttpGet("{householdId:guid}/invitations/link")]
    public async Task<ActionResult<ApiResponse<HouseholdInvitationResponseDto?>>> GetLinkInvitationAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected link invitation retrieval: {Reason}", failureReason ?? "Missing Clerk ID.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await invitationService.GetActiveLinkInvitationAsync(householdId, clerkUserId!, cancellationToken);
        return result.Status switch
        {
            HouseholdInvitationListStatus.Success => Ok(ApiResponse<HouseholdInvitationResponseDto?>.Success(
                result.Invitations?.FirstOrDefault())),
            HouseholdInvitationListStatus.HouseholdNotFound => NotFound(ApiResponse.Fail(404, "Household not found.")),
            HouseholdInvitationListStatus.InviterNotFound => Unauthorized(ApiResponse.Fail(401, result.FailureReason ?? "User not found.")),
            HouseholdInvitationListStatus.InviterNotOwner => StatusCode(403,
                ApiResponse.Fail(403, result.FailureReason ?? "Only owners can view link invitations.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not load link invitation."))
        };
    }

    [HttpPost("invitations/token/{token}/accept")]
    public async Task<ActionResult<ApiResponse<HouseholdMembershipDto>>> AcceptByTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("AcceptByTokenAsync called for token {Token}", token);

        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected token invitation acceptance: {Reason}", failureReason ?? "Missing Clerk ID.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        logger.LogInformation("Processing token invitation acceptance for ClerkUserId: {ClerkUserId}, Token: {Token}",
            clerkUserId, token);

        var result = await invitationService.AcceptInvitationByTokenAsync(token, clerkUserId!, cancellationToken);

        logger.LogInformation("Token invitation acceptance result: Status={Status}, FailureReason={FailureReason}",
            result.Status, result.FailureReason ?? "None");
        return MapAcceptResult(result);
    }

    private ActionResult<ApiResponse<HouseholdMembershipDto>> MapAcceptResult(HouseholdInvitationAcceptResult result)
    {
        return result.Status switch
        {
            HouseholdInvitationAcceptStatus.Success =>
                Ok(ApiResponse<HouseholdMembershipDto>.Success(result.Membership!)),
            HouseholdInvitationAcceptStatus.InvitationNotFound =>
                NotFound(ApiResponse.Fail(404, "Invitation not found.")),
            HouseholdInvitationAcceptStatus.InvitationExpired =>
                BadRequest(ApiResponse.Fail(400, "Invitation has expired.")),
            HouseholdInvitationAcceptStatus.InvitationNotPending =>
                BadRequest(ApiResponse.Fail(400, "Invitation cannot be accepted.")),
            HouseholdInvitationAcceptStatus.UserNotFound =>
                Unauthorized(ApiResponse.Fail(401, "User not found.")),
            HouseholdInvitationAcceptStatus.EmailMismatch =>
                StatusCode(403, ApiResponse.Fail(403, "This invitation does not match your account.")),
            HouseholdInvitationAcceptStatus.AlreadyMember =>
                Ok(ApiResponse<HouseholdMembershipDto>.Success(
                    result.Membership ?? new HouseholdMembershipDto(Guid.Empty, string.Empty, string.Empty, DateTime.UtcNow,
                        Guid.Empty, false),
                    message: result.FailureReason ?? "You are already in this household.")),
            HouseholdInvitationAcceptStatus.OwnerWithMembers =>
                Conflict(ApiResponse.Fail(409, result.FailureReason ?? "You cannot join another household while you have members in your own household.")),
            _ => BadRequest(ApiResponse.Fail(400, result.FailureReason ?? "Could not accept invitation."))
        };
    }
}
