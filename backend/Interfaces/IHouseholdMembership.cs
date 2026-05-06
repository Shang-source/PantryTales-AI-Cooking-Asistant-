using backend.Models;

namespace backend.Interfaces;

public interface IHouseholdMembershipRepository
{
    Task<IReadOnlyList<HouseholdMember>> GetMembershipsByClerkUserIdAsync(string clerkUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all members of a specific household.
    /// </summary>
    Task<IReadOnlyList<HouseholdMember>> GetMembersByHouseholdIdAsync(Guid householdId,
        CancellationToken cancellationToken = default);
}
