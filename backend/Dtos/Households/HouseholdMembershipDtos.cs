namespace backend.Dtos.Households;

public sealed record HouseholdMembershipDto(
    Guid HouseholdId,
    string HouseholdName,
    string Role,
    DateTime JoinedAt,
    Guid OwnerId,
    bool IsOwner);

public sealed record HouseholdMembershipListDto(
    Guid? ActiveHouseholdId,
    string? ActiveHouseholdName,
    IReadOnlyList<HouseholdMembershipDto> Memberships);

/// <summary>
/// Represents a member or pending invitation in a household.
/// Status can be: "owner", "joined", or "pending"
/// </summary>
public sealed record HouseholdMemberDetailDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string Status,   // "owner" | "joined" | "pending"
    DateTime JoinedAt);

/// <summary>
/// Response containing all members and pending invitations for a household.
/// </summary>
public sealed record HouseholdMembersListDto(
    Guid HouseholdId,
    string HouseholdName,
    int ActiveMemberCount,
    int PendingCount,
    IReadOnlyList<HouseholdMemberDetailDto> Members);
