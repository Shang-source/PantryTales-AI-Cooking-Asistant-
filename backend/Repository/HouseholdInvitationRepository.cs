using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class HouseholdInvitationRepository(AppDbContext dbContext) : IHouseholdInvitationRepository
{
    public async Task<HouseholdInvitation?> GetByIdAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return await dbContext.HouseholdInvitations
            .Include(i => i.Household)
            .SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
    }

    public async Task<HouseholdInvitation?> GetPendingByHouseholdAndEmailAsync(Guid householdId, string email,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.HouseholdInvitations
            .Where(i => i.HouseholdId == householdId && i.Status == "pending" &&
                        i.Email != null && EF.Functions.ILike(i.Email, email))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<HouseholdInvitation> AddAsync(HouseholdInvitation invitation,
        CancellationToken cancellationToken = default)
    {
        await dbContext.HouseholdInvitations.AddAsync(invitation, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return invitation;
    }

    public async Task UpdateAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default)
    {
        dbContext.HouseholdInvitations.Update(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdInvitation>> ListByHouseholdAsync(Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.HouseholdInvitations
            .AsNoTracking()
            .Where(i => i.HouseholdId == householdId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
