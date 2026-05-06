using System.Security.Cryptography;
using backend.Data;
using backend.Dtos.Households;
using backend.Interfaces;
using backend.Models;
using backend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class HouseholdInvitationService : IHouseholdInvitationService
{
    private readonly AppDbContext dbContext;
    private readonly IHouseholdInvitationRepository invitationRepository;
    private readonly IEmailSender emailSender;
    private readonly InvitationOptions invitationOptions;
    private readonly ILogger<HouseholdInvitationService> logger;

    private const int DefaultExpirationDays = 7;

    public HouseholdInvitationService(
        AppDbContext dbContext,
        IHouseholdInvitationRepository invitationRepository,
        IEmailSender emailSender,
        IOptions<InvitationOptions>? optionsAccessor,
        ILogger<HouseholdInvitationService> logger)
    {
        this.dbContext = dbContext;
        this.invitationRepository = invitationRepository;
        this.emailSender = emailSender;
        invitationOptions = optionsAccessor?.Value ?? new InvitationOptions();
        this.logger = logger;
    }

    public async Task<HouseholdInvitationCreateResult> CreateInvitationAsync(Guid householdId, string inviterClerkUserId,
        InviteHouseholdMemberRequest request, CancellationToken cancellationToken = default)
    {
        var inviter = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == inviterClerkUserId,
            cancellationToken);
        if (inviter is null)
        {
            logger.LogWarning("Invitation rejected. Inviter with Clerk ID {ClerkUserId} not found.", inviterClerkUserId);
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InviterNotFound,
                FailureReason: "Inviter not found.");
        }

        var household = await dbContext.Households.SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.HouseholdNotFound,
                FailureReason: "Household not found.");
        }

        var inviterMembership = await dbContext.HouseholdMembers
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == inviter.Id, cancellationToken);

        if (inviterMembership is null)
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InviterNotFound,
                FailureReason: "You are not a member of this household.");
        }

        if (!string.Equals(inviterMembership.Role, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InviterNotOwner,
                FailureReason: "Only household owners can send invitations.");
        }

        var lookedUpByClerkId = !string.IsNullOrWhiteSpace(request.ClerkUserId);
        var (targetEmail, targetUser) = await ResolveInviteeAsync(request, cancellationToken);

        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            var status = lookedUpByClerkId
                ? HouseholdInvitationCreateStatus.TargetUserNotFound
                : HouseholdInvitationCreateStatus.InvalidTarget;

            var message = lookedUpByClerkId
                ? "Target user was not found."
                : "Could not determine invitation target.";

            return new HouseholdInvitationCreateResult(status, FailureReason: message);
        }

        if (string.Equals(inviter.Email, targetEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InvalidTarget,
                FailureReason: "You cannot invite yourself.");
        }

        var alreadyMember = false;
        if (targetUser is not null)
        {
            alreadyMember = await dbContext.HouseholdMembers
                .AnyAsync(m => m.HouseholdId == householdId && m.UserId == targetUser.Id, cancellationToken);
        }

        if (!alreadyMember)
        {
            alreadyMember = await dbContext.HouseholdMembers
                .AnyAsync(m => m.HouseholdId == householdId && EF.Functions.ILike(m.Email, targetEmail),
                    cancellationToken);
        }
        if (alreadyMember)
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.TargetAlreadyMember,
                FailureReason: "The user is already a household member.");
        }

        // Check if target user is an owner of a household with other members (cannot invite them)
        var targetHasInventory = false;
        var targetIsInAnotherFamily = false;
        if (targetUser is not null)
        {
            var targetOwnedHouseholdWithMembers = await dbContext.HouseholdMembers
                .Where(m => m.UserId == targetUser.Id && m.Role == "owner")
                .Select(m => new
                {
                    m.HouseholdId,
                    MemberCount = dbContext.HouseholdMembers.Count(hm => hm.HouseholdId == m.HouseholdId)
                })
                .FirstOrDefaultAsync(h => h.MemberCount > 1, cancellationToken);

            if (targetOwnedHouseholdWithMembers is not null)
            {
                return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.TargetIsOwnerWithMembers,
                    FailureReason: "Cannot invite a user who owns a household with other members.");
            }

            // Check if target user has their own inventory (owns a household)
            var targetOwnership = await dbContext.HouseholdMembers
                .AnyAsync(m => m.UserId == targetUser.Id && m.Role == "owner", cancellationToken);
            targetHasInventory = targetOwnership;

            // Check if target user is a member of another household (not as owner)
            targetIsInAnotherFamily = await dbContext.HouseholdMembers
                .AnyAsync(m => m.UserId == targetUser.Id && m.Role != "owner", cancellationToken);
        }

        var normalizedEmail = targetEmail.Trim();
        var pendingInvitation = await invitationRepository
            .GetPendingByHouseholdAndEmailAsync(householdId, normalizedEmail, cancellationToken);

        var expirationDays = request.ExpirationDays ?? DefaultExpirationDays;
        var expiration = DateTime.UtcNow.AddDays(expirationDays);

        HouseholdInvitation invitation;
        if (pendingInvitation is not null)
        {
            pendingInvitation.ExpiredAt = expiration;
            await invitationRepository.UpdateAsync(pendingInvitation, cancellationToken);
            invitation = pendingInvitation;
            logger.LogInformation(
                "Refreshed existing invitation {InvitationId} for {Email} in household {HouseholdId}.",
                invitation.Id,
                normalizedEmail,
                householdId);
        }
        else
        {
            invitation = new HouseholdInvitation
            {
                HouseholdId = householdId,
                Household = household,
                InviterId = inviter.Id,
                Inviter = inviter,
                Email = normalizedEmail,
                Status = "pending",
                ExpiredAt = expiration
            };

            await invitationRepository.AddAsync(invitation, cancellationToken);
            logger.LogInformation(
                "Created invitation {InvitationId} for house {HouseholdId} targeting {Email}",
                invitation.Id,
                householdId,
                normalizedEmail);
        }

        // Send email but don't fail the invitation if email sending fails
        try
        {
            await SendInvitationEmailAsync(invitation, household, inviter, targetHasInventory, targetIsInAnotherFamily, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send invitation email for {InvitationId} to {Email}, but invitation was created successfully.",
                invitation.Id,
                invitation.Email);
        }

        return new HouseholdInvitationCreateResult(
            HouseholdInvitationCreateStatus.Success,
            ToResponseDto(invitation));
    }

    public async Task<HouseholdInvitationAcceptResult> AcceptInvitationAsync(Guid invitationId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await invitationRepository.GetByIdAsync(invitationId, cancellationToken);
        if (invitation is null)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationNotFound,
                FailureReason: "Invitation not found.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (user is null)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.UserNotFound,
                FailureReason: "User not found.");
        }

        return await AcceptInvitationForUserAsync(invitation, user, enforceEmailMatch: true, cancellationToken);
    }

    public async Task<HouseholdInvitationAcceptResult> AcceptInvitationByEmailAsync(Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await invitationRepository.GetByIdAsync(invitationId, cancellationToken);
        if (invitation is null)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationNotFound,
                FailureReason: "Invitation not found.");
        }

        var normalizedEmail = invitation.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.EmailMismatch,
                FailureReason: "Invitation email address is missing.");
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => EF.Functions.ILike(u.Email, normalizedEmail), cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                Nickname = BuildNicknameFromEmail(normalizedEmail),
                ClerkUserId = $"pending:{Guid.CreateVersion7():N}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
        }
        else if (string.IsNullOrWhiteSpace(user.Nickname))
        {
            user.Nickname = BuildNicknameFromEmail(normalizedEmail);
        }

        return await AcceptInvitationForUserAsync(invitation, user, enforceEmailMatch: false, cancellationToken);
    }


    private static string BuildNicknameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }

    public async Task<HouseholdInvitationListResult> ListInvitationsAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var inviter = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (inviter is null)
        {
            return new HouseholdInvitationListResult(HouseholdInvitationListStatus.InviterNotFound,
                FailureReason: "Inviter not found.");
        }

        var household = await dbContext.Households.SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return new HouseholdInvitationListResult(HouseholdInvitationListStatus.HouseholdNotFound,
                FailureReason: "Household not found.");
        }

        var membership = await dbContext.HouseholdMembers
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == inviter.Id, cancellationToken);

        if (membership is null || !string.Equals(membership.Role, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return new HouseholdInvitationListResult(HouseholdInvitationListStatus.InviterNotOwner,
                FailureReason: "Only household owners can view invitations.");
        }

        var invitations = await invitationRepository.ListByHouseholdAsync(householdId, cancellationToken);
        var dtoList = invitations
            .Select(ToResponseDto)
            .ToList();

        return new HouseholdInvitationListResult(HouseholdInvitationListStatus.Success, dtoList);
    }

    private async Task<(string? Email, User? User)> ResolveInviteeAsync(InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ClerkUserId))
        {
            var target = await dbContext.Users
                .SingleOrDefaultAsync(u => u.ClerkUserId == request.ClerkUserId, cancellationToken);
            if (target is null)
            {
                return (null, null);
            }

            return (target.Email, target);
        }

        return (request.Email?.Trim(), null);
    }

    private async Task SendInvitationEmailAsync(HouseholdInvitation invitation, Household household, User inviter,
        bool targetHasInventory, bool targetIsInAnotherFamily, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invitationOptions.AcceptBaseUrl))
        {
            logger.LogWarning("Invitation AcceptBaseUrl configuration missing; skipping email send.");
            return;
        }

        var acceptLink = $"{invitationOptions.AcceptBaseUrl.TrimEnd('/')}/invite/{invitation.Id}";
        var subject = $"{inviter.Nickname} invited you to join {household.Name}";

        // Build warning message based on user's current situation
        var warningHtml = "";
        if (targetHasInventory || targetIsInAnotherFamily)
        {
            var warnings = new List<string>();
            if (targetHasInventory)
            {
                warnings.Add("Your current inventory will be <strong>permanently deleted</strong>");
            }
            if (targetIsInAnotherFamily)
            {
                warnings.Add("You will <strong>automatically leave</strong> your current household");
            }

            warningHtml = $@"
        <div style='background-color: #fff3cd; border: 1px solid #ffc107; border-radius: 8px; padding: 16px; margin-bottom: 24px;'>
            <p style='color: #856404; font-size: 14px; margin: 0; font-weight: 600;'>⚠️ Important Notice</p>
            <p style='color: #856404; font-size: 14px; margin: 8px 0 0 0;'>
                By accepting this invitation:
            </p>
            <ul style='color: #856404; font-size: 14px; margin: 8px 0 0 0; padding-left: 20px;'>
                {string.Join("\n                ", warnings.Select(w => $"<li>{w}</li>"))}
            </ul>
        </div>";
        }

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif; background-color: #f5f5f5; margin: 0; padding: 20px;'>
    <div style='max-width: 500px; margin: 0 auto; background: white; border-radius: 16px; padding: 40px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
        <div style='text-align: center; margin-bottom: 24px;'>
            <div style='width: 60px; height: 60px; background: linear-gradient(135deg, #5a7872, #4a6862); border-radius: 12px; margin: 0 auto; display: flex; align-items: center; justify-content: center;'>
                <span style='font-size: 30px;'>🍳</span>
            </div>
        </div>
        
        <h1 style='color: #333; font-size: 24px; text-align: center; margin-bottom: 16px;'>You're Invited!</h1>
        
        <p style='color: #666; font-size: 16px; line-height: 1.6; text-align: center; margin-bottom: 32px;'>
            <strong>{inviter.Nickname}</strong> has invited you to join the household <strong>{household.Name}</strong> on PantryTales.
        </p>
        
        {warningHtml}
        
        <div style='text-align: center; margin-bottom: 16px;'>
            <a href='{acceptLink}' style='display: inline-block; background: linear-gradient(135deg, #5a7872, #4a6862); color: white; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>
                Accept & Join Household
            </a>
        </div>
        
        <p style='color: #666; font-size: 13px; text-align: center; margin-bottom: 24px;'>
            Click the button above to instantly join the household. No app installation required!
        </p>
        
        <p style='color: #999; font-size: 14px; text-align: center; margin-bottom: 8px;'>
            This invitation will expire in 7 days.
        </p>
        
        <p style='color: #999; font-size: 12px; text-align: center;'>
            If the button doesn't work, copy and paste this link into your browser:<br>
            <a href='{acceptLink}' style='color: #5a7872;'>{acceptLink}</a>
        </p>
        
        <hr style='border: none; border-top: 1px solid #eee; margin: 32px 0;'>
        
        <p style='color: #bbb; font-size: 12px; text-align: center;'>
            PantryTales - Your Family Kitchen Companion
        </p>
    </div>
</body>
</html>";

        if (string.IsNullOrWhiteSpace(invitation.Email))
        {
            logger.LogWarning("Invitation email is null or empty; skipping email send for invitation {InvitationId}.", invitation.Id);
            return;
        }

        await emailSender.SendInvitationAsync(invitation.Email, subject, body, cancellationToken);
    }

    private async Task<HouseholdInvitationAcceptResult> AcceptInvitationForUserAsync(HouseholdInvitation invitation,
        User user,
        bool enforceEmailMatch,
        CancellationToken cancellationToken)
    {
        // For email invitations, email must be present
        if (invitation.InvitationType == "email" && string.IsNullOrWhiteSpace(invitation.Email))
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationNotFound,
                FailureReason: "Invitation email is missing.");
        }

        if (enforceEmailMatch && !string.IsNullOrWhiteSpace(invitation.Email) &&
            !string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.EmailMismatch,
                FailureReason: "This invitation was addressed to a different email address.");
        }

        var invitationIsPending = string.Equals(invitation.Status, "pending", StringComparison.OrdinalIgnoreCase);
        if (!invitationIsPending)
        {
            var existingMembership = await dbContext.HouseholdMembers
                .Include(m => m.Household)
                .SingleOrDefaultAsync(m => m.HouseholdId == invitation.HouseholdId && m.UserId == user.Id,
                    cancellationToken);

            if (existingMembership is not null)
            {
                var dto = BuildMembershipDto(existingMembership, invitation);
                return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.Success, dto);
            }

            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationNotPending,
                FailureReason: "Invitation is no longer pending.");
        }

        if (invitation.ExpiredAt <= DateTime.UtcNow)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationExpired,
                FailureReason: "Invitation has expired.");
        }

        var pendingMembership = await dbContext.HouseholdMembers
            .Include(m => m.Household)
            .SingleOrDefaultAsync(m => m.HouseholdId == invitation.HouseholdId && m.UserId == user.Id,
                cancellationToken);

        if (pendingMembership is not null)
        {
            invitation.Status = "accepted";
            await invitationRepository.UpdateAsync(invitation, cancellationToken);
            var dto = BuildMembershipDto(pendingMembership, invitation);
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.Success, dto);
        }

        var userOwnedHouseholdWithMembers = await dbContext.HouseholdMembers
            .Where(m => m.UserId == user.Id && m.Role == "owner")
            .Select(m => new
            {
                m.HouseholdId,
                MemberCount = dbContext.HouseholdMembers.Count(hm => hm.HouseholdId == m.HouseholdId)
            })
            .FirstOrDefaultAsync(h => h.MemberCount > 1, cancellationToken);

        if (userOwnedHouseholdWithMembers is not null)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.OwnerWithMembers,
                FailureReason: "You cannot join another household while you have members in your own household. Please remove all members first.");
        }

        var currentMemberships = await dbContext.HouseholdMembers
            .Include(m => m.Household)
            .Where(m => m.UserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var currentMembership in currentMemberships)
        {
            if (currentMembership.Role == "owner")
            {
                var inventoryItems = await dbContext.InventoryItems
                    .Where(i => i.HouseholdId == currentMembership.HouseholdId)
                    .ToListAsync(cancellationToken);
                dbContext.InventoryItems.RemoveRange(inventoryItems);

                var checklistItems = await dbContext.ChecklistItems
                    .Where(c => c.HouseholdId == currentMembership.HouseholdId)
                    .ToListAsync(cancellationToken);
                dbContext.ChecklistItems.RemoveRange(checklistItems);

                var invitations = await dbContext.HouseholdInvitations
                    .Where(i => i.HouseholdId == currentMembership.HouseholdId)
                    .ToListAsync(cancellationToken);
                dbContext.HouseholdInvitations.RemoveRange(invitations);

                var allMemberships = await dbContext.HouseholdMembers
                    .Where(m => m.HouseholdId == currentMembership.HouseholdId)
                    .ToListAsync(cancellationToken);
                dbContext.HouseholdMembers.RemoveRange(allMemberships);

                dbContext.Households.Remove(currentMembership.Household);

                logger.LogInformation(
                    "Deleted household {HouseholdId} and all its data as user {UserId} is joining another household.",
                    currentMembership.HouseholdId, user.Id);
            }
            else
            {
                dbContext.HouseholdMembers.Remove(currentMembership);
                logger.LogInformation(
                    "Removed user {UserId} from household {HouseholdId} as they are joining another household.",
                    user.Id, currentMembership.HouseholdId);
            }
        }

        var membership = new HouseholdMember
        {
            HouseholdId = invitation.HouseholdId,
            Household = invitation.Household!,
            UserId = user.Id,
            User = user,
            Role = "member",
            DisplayName = user.Nickname,
            Email = user.Email
        };

        await dbContext.HouseholdMembers.AddAsync(membership, cancellationToken);
        invitation.Status = "accepted";
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtoResult = BuildMembershipDto(membership, invitation);

        logger.LogInformation("User {UserId} accepted invitation {InvitationId}.", user.Id, invitation.Id);

        return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.Success, dtoResult);
    }

    private static HouseholdMembershipDto BuildMembershipDto(HouseholdMember membership,
        HouseholdInvitation invitation)
    {
        var householdName = membership.Household?.Name ?? invitation.Household?.Name ?? string.Empty;
        var ownerId = membership.Household?.OwnerId ?? invitation.Household?.OwnerId ?? Guid.Empty;

        return new HouseholdMembershipDto(
            membership.HouseholdId,
            householdName,
            membership.Role,
            membership.JoinedAt,
            ownerId,
            string.Equals(membership.Role, "owner", StringComparison.OrdinalIgnoreCase));
    }

    private static HouseholdInvitationResponseDto ToResponseDto(HouseholdInvitation invitation)
    {
        return new HouseholdInvitationResponseDto(
            invitation.Id,
            invitation.HouseholdId,
            invitation.Email,
            invitation.Status,
            invitation.ExpiredAt,
            invitation.CreatedAt,
            invitation.InvitationType,
            invitation.InvitationType == "link" ? invitation.Token : null);
    }

    public async Task<HouseholdInvitationCreateResult> CreateLinkInvitationAsync(Guid householdId,
        string inviterClerkUserId,
        CreateLinkInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var inviter = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == inviterClerkUserId,
            cancellationToken);
        if (inviter is null)
        {
            logger.LogWarning("Link invitation rejected. Inviter with Clerk ID {ClerkUserId} not found.",
                inviterClerkUserId);
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InviterNotFound,
                FailureReason: "Inviter not found.");
        }

        var household = await dbContext.Households.SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.HouseholdNotFound,
                FailureReason: "Household not found.");
        }

        var inviterMembership = await dbContext.HouseholdMembers
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == inviter.Id, cancellationToken);

        if (inviterMembership is null)
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InviterNotFound,
                FailureReason: "You are not a member of this household.");
        }

        if (!string.Equals(inviterMembership.Role, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.InviterNotOwner,
                FailureReason: "Only household owners can create link invitations.");
        }

        // Check for existing active link invitation
        var existingLink = await dbContext.HouseholdInvitations
            .FirstOrDefaultAsync(i =>
                    i.HouseholdId == householdId &&
                    i.InvitationType == "link" &&
                    i.Status == "pending" &&
                    i.ExpiredAt > DateTime.UtcNow,
                cancellationToken);

        var expirationDays = request.ExpirationDays ?? DefaultExpirationDays;
        var expiration = DateTime.UtcNow.AddDays(expirationDays);

        HouseholdInvitation invitation;
        if (existingLink is not null)
        {
            // Refresh expiration of existing link
            existingLink.ExpiredAt = expiration;
            await invitationRepository.UpdateAsync(existingLink, cancellationToken);
            invitation = existingLink;
            logger.LogInformation(
                "Refreshed existing link invitation {InvitationId} for household {HouseholdId}.",
                invitation.Id,
                householdId);
        }
        else
        {
            // Generate a new token
            var token = GenerateToken();

            invitation = new HouseholdInvitation
            {
                HouseholdId = householdId,
                Household = household,
                InviterId = inviter.Id,
                Inviter = inviter,
                Email = null,
                InvitationType = "link",
                Token = token,
                Status = "pending",
                ExpiredAt = expiration
            };

            await invitationRepository.AddAsync(invitation, cancellationToken);
            logger.LogInformation(
                "Created link invitation {InvitationId} for household {HouseholdId}",
                invitation.Id,
                householdId);
        }

        return new HouseholdInvitationCreateResult(
            HouseholdInvitationCreateStatus.Success,
            ToResponseDto(invitation));
    }

    public async Task<HouseholdInvitationAcceptResult> AcceptInvitationByTokenAsync(string token, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await dbContext.HouseholdInvitations
            .Include(i => i.Household)
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

        if (invitation is null)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationNotFound,
                FailureReason: "Invitation not found.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (user is null)
        {
            return new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.UserNotFound,
                FailureReason: "User not found.");
        }

        // For link invitations, skip email matching
        return await AcceptInvitationForUserAsync(invitation, user, enforceEmailMatch: false, cancellationToken);
    }

    public async Task<HouseholdInvitationListResult> GetActiveLinkInvitationAsync(Guid householdId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        if (user is null)
        {
            return new HouseholdInvitationListResult(HouseholdInvitationListStatus.InviterNotFound,
                FailureReason: "User not found.");
        }

        var household = await dbContext.Households.SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);
        if (household is null)
        {
            return new HouseholdInvitationListResult(HouseholdInvitationListStatus.HouseholdNotFound,
                FailureReason: "Household not found.");
        }

        var membership = await dbContext.HouseholdMembers
            .SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == user.Id, cancellationToken);

        if (membership is null || !string.Equals(membership.Role, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return new HouseholdInvitationListResult(HouseholdInvitationListStatus.InviterNotOwner,
                FailureReason: "Only household owners can view link invitations.");
        }

        var invitation = await dbContext.HouseholdInvitations
            .FirstOrDefaultAsync(i =>
                    i.HouseholdId == householdId &&
                    i.InvitationType == "link" &&
                    i.Status == "pending" &&
                    i.ExpiredAt > DateTime.UtcNow,
                cancellationToken);

        var invitations = invitation is not null
            ? new List<HouseholdInvitationResponseDto> { ToResponseDto(invitation) }
            : new List<HouseholdInvitationResponseDto>();

        return new HouseholdInvitationListResult(HouseholdInvitationListStatus.Success, invitations);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
