using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class HouseholdMembershipRepository(AppDbContext context) : IHouseholdMembershipRepository
{
    public async Task<IReadOnlyList<HouseholdMember>> GetMembershipsByClerkUserIdAsync(string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        return await context.HouseholdMembers
            .Include(m => m.Household)
            .Include(m => m.User)
            .Where(m => m.User.ClerkUserId == clerkUserId)
            .OrderBy(m => m.Role == "owner" ? 0 : 1)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdMember>> GetMembersByHouseholdIdAsync(Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await context.HouseholdMembers
            .Include(m => m.User)
            .Include(m => m.Household)
            .Where(m => m.HouseholdId == householdId)
            .OrderBy(m => m.Role == "owner" ? 0 : 1)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
    }
}
