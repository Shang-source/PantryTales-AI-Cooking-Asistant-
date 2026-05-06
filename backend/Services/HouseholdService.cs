using backend.Data;
using backend.Dtos.Households;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class HouseholdService(AppDbContext dbContext, IHouseholdMembershipRepository householdMembershipRepository,
    ILogger<HouseholdService> logger) : IHouseholdService
{
    public async Task<HouseholdLeaveResult> LeaveHouseholdAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (user is null)
        {
            return HouseholdLeaveResult.UserNotFound;
        }

        var household = await dbContext.Households.AsNoTracking()
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return HouseholdLeaveResult.HouseholdNotFound;
        }

        if (household.OwnerId == user.Id)
        {
            return HouseholdLeaveResult.OwnerCannotLeave;
        }

        var membership = await dbContext.HouseholdMembers
            .Include(m => m.Household)
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == user.Id, cancellationToken);

        if (membership is null)
        {
            return HouseholdLeaveResult.NotMember;
        }

        dbContext.HouseholdMembers.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);

        var remainingMemberships = await dbContext.HouseholdMembers
            .Include(m => m.Household)
            .Where(m => m.UserId == user.Id)
            .OrderBy(m => m.Role == "owner" ? 0 : 1)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

        if (remainingMemberships.Count > 0)
        {
            var activeMembership = remainingMemberships[0];
            logger.LogInformation("User {UserId} left household {HouseholdId}. Active household: {ActiveHouseholdId}",
                user.Id, householdId, activeMembership.HouseholdId);

            return HouseholdLeaveResult.Success(activeMembership.HouseholdId,
                activeMembership.Household.Name,
                false);
        }

        var defaultMembership = await CreateDefaultHouseholdForUserAsync(user, cancellationToken);
        logger.LogInformation(
            "User {UserId} left household {HouseholdId} and was assigned a new default household {DefaultHouseholdId}.",
            user.Id,
            householdId,
            defaultMembership.HouseholdId);

        return HouseholdLeaveResult.Success(defaultMembership.HouseholdId, defaultMembership.Household.Name, true);
    }

    public async Task<HouseholdMembershipListDto?> GetMembershipsAsync(string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var memberships = await householdMembershipRepository
            .GetMembershipsByClerkUserIdAsync(clerkUserId, cancellationToken);

        IReadOnlyList<HouseholdMember> ensuredMemberships = memberships;

        // If user has multiple memberships, clean up any single-member households they own
        if (ensuredMemberships.Count > 1)
        {
            var cleanedUp = await CleanupSingleMemberHouseholdsForUserAsync(user.Id, cancellationToken);
            if (cleanedUp)
            {
                // Re-fetch memberships after cleanup
                memberships = await householdMembershipRepository
                    .GetMembershipsByClerkUserIdAsync(clerkUserId, cancellationToken);
                ensuredMemberships = memberships;
            }
        }

        if (ensuredMemberships.Count == 0)
        {
            var defaultMembership = await CreateDefaultHouseholdForUserAsync(user, cancellationToken);
            ensuredMemberships = new List<HouseholdMember> { defaultMembership };
        }

        var membershipDtos = ensuredMemberships
            .Select(m => new HouseholdMembershipDto(
                m.HouseholdId,
                m.Household.Name,
                m.Role,
                m.JoinedAt,
                m.Household.OwnerId,
                m.Role == "owner"))
            .ToList();

        var activeMembership = ensuredMemberships.FirstOrDefault();

        return new HouseholdMembershipListDto(
            activeMembership?.HouseholdId,
            activeMembership?.Household.Name,
            membershipDtos);
    }

    public async Task<Guid?> GetActiveHouseholdIdAsync(
    string clerkUserId,
    CancellationToken cancellationToken = default)
    {
        var memberships = await GetMembershipsAsync(clerkUserId, cancellationToken);
        return memberships?.ActiveHouseholdId;
    }

    public async Task<HouseholdMembersResult> GetHouseholdMembersAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify user exists
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (user is null)
        {
            return HouseholdMembersResult.UserNotFound;
        }

        // 2. Verify household exists
        var household = await dbContext.Households
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return HouseholdMembersResult.HouseholdNotFound;
        }

        // 3. Verify user is a member of this household
        var userMembership = await dbContext.HouseholdMembers
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == user.Id, cancellationToken);
        if (userMembership is null)
        {
            return HouseholdMembersResult.NotMember;
        }

        // 4. Get all members
        var members = await householdMembershipRepository
            .GetMembersByHouseholdIdAsync(householdId, cancellationToken);

        // 5. Get pending invitations
        var pendingInvitations = await dbContext.HouseholdInvitations
            .Where(i => i.HouseholdId == householdId && i.Status == "pending")
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        // 6. Build member details list
        var memberDetails = new List<HouseholdMemberDetailDto>();

        // Add actual members (owner first, then others)
        foreach (var member in members)
        {
            // Check if user is a "pending" user (accepted via email but not registered in APP)
            // These users have ClerkUserId starting with "pending:"
            var isPendingUser = member.User?.ClerkUserId?.StartsWith("pending:") == true;

            string status;
            if (member.Role == "owner")
            {
                status = "owner";
            }
            else if (isPendingUser)
            {
                status = "pending";  // Show as pending until they register the APP
            }
            else
            {
                status = "joined";
            }

            memberDetails.Add(new HouseholdMemberDetailDto(
                member.UserId,
                member.DisplayName,
                member.Email,
                isPendingUser ? null : member.User?.AvatarUrl,  // No avatar for pending users
                status,
                member.JoinedAt));
        }

        // Add pending email invitations (not yet accepted)
        // Note: Link/QR code invitations are NOT shown in the member list since they're
        // not tied to any specific person until someone actually accepts them
        foreach (var invitation in pendingInvitations)
        {
            // Skip link/QR code invitations - they don't belong in the member list
            if (invitation.InvitationType == "link")
            {
                continue;
            }
            
            // Only show email invitations (targeted at specific people)
            var displayName = !string.IsNullOrEmpty(invitation.Email)
                ? (invitation.Email.Contains("@") ? invitation.Email.Split('@')[0] : invitation.Email)
                : "Pending Invitation";
            
            memberDetails.Add(new HouseholdMemberDetailDto(
                invitation.Id,  // Use invitation ID as the identifier
                displayName,
                invitation.Email ?? "No email",
                null,  // No avatar for pending invitations
                "pending",
                invitation.CreatedAt));
        }

        // Count: active members are those who are NOT pending users
        var activeMemberCount = members.Count(m => m.User?.ClerkUserId?.StartsWith("pending:") != true);
        var pendingCount = pendingInvitations.Count(i => i.InvitationType != "link") + members.Count(m => m.User?.ClerkUserId?.StartsWith("pending:") == true);

        var result = new HouseholdMembersListDto(
            householdId,
            household.Name,
            activeMemberCount,
            pendingCount,
            memberDetails);

        return HouseholdMembersResult.Success(result);
    }

    private async Task<bool> CleanupSingleMemberHouseholdsForUserAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        // Find all households where this user is the only member and is the owner
        var singleMemberHouseholds = await dbContext.HouseholdMembers
            .Where(m => m.UserId == userId && m.Role == "owner")
            .Select(m => new
            {
                m.HouseholdId,
                m.Household,
                MemberCount = dbContext.HouseholdMembers.Count(hm => hm.HouseholdId == m.HouseholdId)
            })
            .Where(h => h.MemberCount == 1)
            .ToListAsync(cancellationToken);

        if (singleMemberHouseholds.Count == 0)
        {
            return false;
        }

        foreach (var h in singleMemberHouseholds)
        {
            // Delete all related data
            var inventoryItems = await dbContext.InventoryItems
                .Where(i => i.HouseholdId == h.HouseholdId)
                .ToListAsync(cancellationToken);
            dbContext.InventoryItems.RemoveRange(inventoryItems);

            var checklistItems = await dbContext.ChecklistItems
                .Where(c => c.HouseholdId == h.HouseholdId)
                .ToListAsync(cancellationToken);
            dbContext.ChecklistItems.RemoveRange(checklistItems);

            var invitations = await dbContext.HouseholdInvitations
                .Where(i => i.HouseholdId == h.HouseholdId)
                .ToListAsync(cancellationToken);
            dbContext.HouseholdInvitations.RemoveRange(invitations);

            var memberships = await dbContext.HouseholdMembers
                .Where(m => m.HouseholdId == h.HouseholdId)
                .ToListAsync(cancellationToken);
            dbContext.HouseholdMembers.RemoveRange(memberships);

            dbContext.Households.Remove(h.Household);

            logger.LogInformation(
                "Cleaned up single-member household {HouseholdId} for user {UserId}.",
                h.HouseholdId, userId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<HouseholdMember> CreateDefaultHouseholdForUserAsync(User user,
        CancellationToken cancellationToken)
    {
        var defaultHousehold = new Household
        {
            Owner = user,
            Name = $"{user.Nickname}'s household"
        };
        dbContext.Households.Add(defaultHousehold);

        var defaultMembership = new HouseholdMember
        {
            Household = defaultHousehold,
            User = user,
            Role = "owner",
            DisplayName = user.Nickname,
            Email = user.Email
        };
        dbContext.HouseholdMembers.Add(defaultMembership);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created default household {HouseholdId} for user {UserId}.",
            defaultHousehold.Id,
            user.Id);

        return defaultMembership;
    }

    public async Task<RemoveMemberResult> RemoveMemberAsync(Guid householdId, Guid memberId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify the requesting user exists
        var requestingUser = await dbContext.Users
            .SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (requestingUser is null)
        {
            return RemoveMemberResult.UserNotFound;
        }

        // 2. Verify household exists
        var household = await dbContext.Households
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return RemoveMemberResult.HouseholdNotFound;
        }

        // 3. Verify the requesting user is the owner
        if (household.OwnerId != requestingUser.Id)
        {
            return RemoveMemberResult.NotOwner;
        }

        // 4. Try to find a member with this ID (memberId is the UserId for joined members)
        var memberToRemove = await dbContext.HouseholdMembers
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == memberId, cancellationToken);

        if (memberToRemove is not null)
        {
            // Cannot remove the owner
            if (memberToRemove.Role == "owner")
            {
                return RemoveMemberResult.CannotRemoveOwner;
            }

            // Get the removed user's info before removal
            var removedUser = await dbContext.Users
                .SingleOrDefaultAsync(u => u.Id == memberId, cancellationToken);

            dbContext.HouseholdMembers.Remove(memberToRemove);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Create a new default household for the removed user if they have no remaining memberships
            if (removedUser is not null)
            {
                var remainingMemberships = await dbContext.HouseholdMembers
                    .AnyAsync(m => m.UserId == removedUser.Id, cancellationToken);

                if (!remainingMemberships)
                {
                    await CreateDefaultHouseholdForUserAsync(removedUser, cancellationToken);
                    logger.LogInformation(
                        "Created new default household for removed user {UserId}.",
                        removedUser.Id);
                }
            }

            logger.LogInformation(
                "Owner {OwnerId} removed member {MemberId} from household {HouseholdId}.",
                requestingUser.Id, memberId, householdId);

            return RemoveMemberResult.Success;
        }

        // 5. Try to find a pending invitation with this ID
        var invitationToRemove = await dbContext.HouseholdInvitations
            .SingleOrDefaultAsync(i => i.Id == memberId && i.HouseholdId == householdId && i.Status == "pending", cancellationToken);

        if (invitationToRemove is not null)
        {
            dbContext.HouseholdInvitations.Remove(invitationToRemove);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Owner {OwnerId} removed pending invitation {InvitationId} from household {HouseholdId}.",
                requestingUser.Id, memberId, householdId);

            return RemoveMemberResult.Success;
        }

        return RemoveMemberResult.MemberNotFound;
    }
}
