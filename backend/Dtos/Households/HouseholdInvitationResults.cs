namespace backend.Dtos.Households;

public enum HouseholdInvitationCreateStatus
{
    Success = 1,
    HouseholdNotFound,
    InviterNotFound,
    InviterNotOwner,
    TargetUserNotFound,
    TargetAlreadyMember,
    InvitationPending,
    InvalidTarget,
    TargetIsOwnerWithMembers
}

public sealed record HouseholdInvitationCreateResult(
    HouseholdInvitationCreateStatus Status,
    HouseholdInvitationResponseDto? Invitation = null,
    string? FailureReason = null);

public enum HouseholdInvitationAcceptStatus
{
    Success = 1,
    InvitationNotFound,
    InvitationExpired,
    InvitationNotPending,
    UserNotFound,
    EmailMismatch,
    AlreadyMember,
    OwnerWithMembers
}

public sealed record HouseholdInvitationAcceptResult(
    HouseholdInvitationAcceptStatus Status,
    HouseholdMembershipDto? Membership = null,
    string? FailureReason = null);

public enum HouseholdInvitationListStatus
{
    Success = 1,
    HouseholdNotFound,
    InviterNotFound,
    InviterNotOwner
}

public sealed record HouseholdInvitationListResult(
    HouseholdInvitationListStatus Status,
    IReadOnlyList<HouseholdInvitationResponseDto>? Invitations = null,
    string? FailureReason = null);
