using backend.Dtos.Households;

namespace backend.Interfaces;

public interface IHouseholdService
{
    Task<HouseholdLeaveResult> LeaveHouseholdAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default);
    Task<HouseholdMembershipListDto?> GetMembershipsAsync(string clerkUserId,
        CancellationToken cancellationToken = default);
    Task<Guid?> GetActiveHouseholdIdAsync(string clerkUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all members and pending invitations for a household.
    /// </summary>
    Task<HouseholdMembersResult> GetHouseholdMembersAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a member from the household. Only the owner can remove members.
    /// </summary>
    Task<RemoveMemberResult> RemoveMemberAsync(Guid householdId, Guid memberId, string clerkUserId,
        CancellationToken cancellationToken = default);
}

public enum HouseholdMembersResultStatus
{
    Success,
    HouseholdNotFound,
    UserNotFound,
    NotMember
}

public sealed record HouseholdMembersResult(
    HouseholdMembersResultStatus Status,
    HouseholdMembersListDto? Data = null,
    string? FailureReason = null)
{
    public static HouseholdMembersResult Success(HouseholdMembersListDto data) =>
        new(HouseholdMembersResultStatus.Success, data);
    public static HouseholdMembersResult HouseholdNotFound =>
        new(HouseholdMembersResultStatus.HouseholdNotFound, FailureReason: "Household not found.");
    public static HouseholdMembersResult UserNotFound =>
        new(HouseholdMembersResultStatus.UserNotFound, FailureReason: "User not found.");
    public static HouseholdMembersResult NotMember =>
        new(HouseholdMembersResultStatus.NotMember, FailureReason: "You are not a member of this household.");
}

public enum RemoveMemberResultStatus
{
    Success,
    HouseholdNotFound,
    UserNotFound,
    NotOwner,
    MemberNotFound,
    CannotRemoveOwner
}

public sealed record RemoveMemberResult(
    RemoveMemberResultStatus Status,
    string? FailureReason = null)
{
    public static RemoveMemberResult Success => new(RemoveMemberResultStatus.Success);
    public static RemoveMemberResult HouseholdNotFound =>
        new(RemoveMemberResultStatus.HouseholdNotFound, "Household not found.");
    public static RemoveMemberResult UserNotFound =>
        new(RemoveMemberResultStatus.UserNotFound, "User not found.");
    public static RemoveMemberResult NotOwner =>
        new(RemoveMemberResultStatus.NotOwner, "Only the owner can remove members.");
    public static RemoveMemberResult MemberNotFound =>
        new(RemoveMemberResultStatus.MemberNotFound, "Member or invitation not found.");
    public static RemoveMemberResult CannotRemoveOwner =>
        new(RemoveMemberResultStatus.CannotRemoveOwner, "Cannot remove the owner from the household.");
}