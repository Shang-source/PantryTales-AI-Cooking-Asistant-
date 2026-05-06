using backend.Models;

namespace backend.Interfaces;

public interface IHouseholdInvitationRepository
{
    Task<HouseholdInvitation?> GetByIdAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<HouseholdInvitation?> GetPendingByHouseholdAndEmailAsync(Guid householdId, string email,
        CancellationToken cancellationToken = default);
    Task<HouseholdInvitation> AddAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default);
    Task UpdateAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HouseholdInvitation>> ListByHouseholdAsync(Guid householdId,
        CancellationToken cancellationToken = default);
}
