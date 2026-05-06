using backend.Dtos.Households;

namespace backend.Interfaces;

public interface IHouseholdInvitationService
{
    Task<HouseholdInvitationCreateResult> CreateInvitationAsync(Guid householdId, string inviterClerkUserId,
        InviteHouseholdMemberRequest request, CancellationToken cancellationToken = default);

    Task<HouseholdInvitationCreateResult> CreateLinkInvitationAsync(Guid householdId, string inviterClerkUserId,
        CreateLinkInvitationRequest request, CancellationToken cancellationToken = default);

    Task<HouseholdInvitationAcceptResult> AcceptInvitationAsync(Guid invitationId, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvitationAcceptResult> AcceptInvitationByEmailAsync(Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvitationAcceptResult> AcceptInvitationByTokenAsync(string token, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvitationListResult> ListInvitationsAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvitationListResult> GetActiveLinkInvitationAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default);
}
