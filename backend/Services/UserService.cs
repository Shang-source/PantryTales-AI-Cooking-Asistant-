using backend.Data;
using backend.Dtos.Users;
using backend.Extensions;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Services;

public class UserService(AppDbContext dbContext, ILogger<UserService> logger) : IUserService
{
    private const string UniqueViolationSqlState = "23505";

    public async Task<UserResponseDto> GetOrCreateAsync(UserSyncPayload payload,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.HouseholdMemberships)
            .SingleOrDefaultAsync(u => u.ClerkUserId == payload.ClerkUserId, cancellationToken);

        if (user is null)
        {
            user = await dbContext.Users
                .Include(u => u.HouseholdMemberships)
                .SingleOrDefaultAsync(u => EF.Functions.ILike(u.Email, payload.Email), cancellationToken);

            if (user is not null)
            {
                user.ClerkUserId = payload.ClerkUserId;
            }
        }

        if (user is not null)
        {
            var updated = ApplyUserSyncUpdates(user, payload);
            var ensuredHousehold = EnsureDefaultHouseholdIfMissing(user, payload);

            if (updated || ensuredHousehold)
            {
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    logger.LogWarning(ex,
                        "Unique constraint violation while syncing existing Clerk user {ClerkUserId}; returning current record.",
                        payload.ClerkUserId);
                    dbContext.ChangeTracker.Clear();

                    var current = await dbContext.Users
                        .AsNoTracking()
                        .SingleOrDefaultAsync(u => u.ClerkUserId == payload.ClerkUserId, cancellationToken);
                    if (current is not null)
                    {
                        return current.ToDto();
                    }
                }
            }

            return user.ToDto();
        }

        logger.LogInformation("User with Clerk ID {ClerkUserId} was not found locally. Creating from claims.",
            payload.ClerkUserId);

        user = new User
        {
            ClerkUserId = payload.ClerkUserId,
            Email = payload.Email,
            Nickname = payload.Nickname,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);

        var now = DateTime.UtcNow;
        var pendingInvites = await dbContext.HouseholdInvitations
            .Where(i =>
                i.Email == payload.Email &&
                i.Status == "pending" &&
                i.ExpiredAt > now)
            .ToListAsync(cancellationToken);

        if (pendingInvites.Count != 0)
        {
            foreach (var invite in pendingInvites)
            {
                dbContext.HouseholdMembers.Add(new HouseholdMember
                {
                    HouseholdId = invite.HouseholdId,
                    User = user,
                    Role = "member",
                    DisplayName = payload.Nickname,
                    Email = payload.Email
                });

                invite.Status = "accepted";
            }

            logger.LogInformation(
                "Created local user {UserId} for Clerk ID {ClerkUserId} and accepted {InvitationCount} pending invitations.",
                user.Id,
                payload.ClerkUserId,
                pendingInvites.Count);
        }
        else
        {
            CreateDefaultHouseholdForUser(user, payload);
            logger.LogInformation("Created local user {UserId} for Clerk ID {ClerkUserId} with default household.",
                user.Id,
                payload.ClerkUserId);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(ex,
                "Unique constraint violation while creating Clerk user {ClerkUserId}; another request likely created it first.",
                payload.ClerkUserId);
            dbContext.ChangeTracker.Clear();

            var existing = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.ClerkUserId == payload.ClerkUserId, cancellationToken);

            if (existing is not null)
            {
                return existing.ToDto();
            }

            throw;
        }

        return user.ToDto();
    }

    public async Task<UserResponseDto?> GetByClerkUserIdAsync(string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        return user?.ToDto();
    }

    public async Task<UserProfileResponseDto?> GetProfileAsync(string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(u => u.Preferences)
            .ThenInclude(p => p.Tag)
            .SingleOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        return user?.ToDetailDto();
    }

    public async Task<UserProfileResponseDto?> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Preferences)
            .ThenInclude(p => p.Tag)
            .Include(u => u.HouseholdMemberships)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Attempted to update profile for non-existent User ID {UserId}.", userId);
            return null;
        }

        bool isIdentityModified = false;

        if (request.Nickname is not null && user.Nickname != request.Nickname)
        {
            user.Nickname = request.Nickname;
            isIdentityModified = true;
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl;
        }

        user.Age = request.Age;
        user.Gender = request.Gender;
        user.Height = request.Height;
        user.Weight = request.Weight;

        if (request.Preferences is not null)
        {
            ApplyPreferenceUpdates(user, request.Preferences);
        }

        if (isIdentityModified && user.HouseholdMemberships.Count != 0)
        {
            foreach (var member in user.HouseholdMemberships)
            {
                member.DisplayName = user.Nickname;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated profile for user {UserId} (Clerk {ClerkUserId}).", user.Id, user.ClerkUserId);

        return user.ToDetailDto();
    }

    private static void ApplyPreferenceUpdates(User user, IEnumerable<UpdateUserPreferenceDto> preferences)
    {
        var desired = preferences
            .GroupBy(p => p.TagId)
            .Select(g => g.First())
            .ToDictionary(p => p.TagId, p => p.Relation);

        // 1. Remove
        var removable = user.Preferences.Where(p => !desired.ContainsKey(p.TagId)).ToList();
        foreach (var preference in removable)
        {
            user.Preferences.Remove(preference);
        }

        var currentByTag = user.Preferences.ToDictionary(p => p.TagId);

        // 2. Add or Update
        foreach (var (tagId, relation) in desired)
        {
            if (currentByTag.TryGetValue(tagId, out var existing))
            {
                if (existing.Relation != relation) 
                {
                    existing.Relation = relation;
                }
                continue;
            }

            user.Preferences.Add(new UserPreference
            {
                UserId = user.Id,
                TagId = tagId,
                Relation = relation
            });
        }
    }
    private bool ApplyUserSyncUpdates(User existingUser, UserSyncPayload payload)
    {
        var shouldUpdate = false;

        if (!string.Equals(existingUser.Email, payload.Email, StringComparison.OrdinalIgnoreCase))
        {
            existingUser.Email = payload.Email;
            shouldUpdate = true;
        }

        // Only sync nickname from token when we do not already have one locally.
        if (string.IsNullOrWhiteSpace(existingUser.Nickname))
        {
            existingUser.Nickname = payload.Nickname;
            shouldUpdate = true;
        }

        if (!shouldUpdate) return shouldUpdate;
        existingUser.UpdatedAt = DateTime.UtcNow;
        logger.LogInformation("Updated local user {UserId} to reflect latest Clerk token claims.", existingUser.Id);

        return shouldUpdate;
    }

    private bool EnsureDefaultHouseholdIfMissing(User user, UserSyncPayload payload)
    {
        if (user.HouseholdMemberships.Count != 0)
        {
            return false;
        }

        CreateDefaultHouseholdForUser(user, payload);
        logger.LogInformation("Added default household for existing user {UserId}.", user.Id);
        return true;
    }

    private void CreateDefaultHouseholdForUser(User user, UserSyncPayload payload)
    {
        var household = new Household
        {
            Owner = user,
            Name = $"{payload.Nickname}'s household"
        };

        dbContext.Households.Add(household);

        dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Household = household,
            User = user,
            Role = "owner",
            DisplayName = payload.Nickname,
            Email = payload.Email
        });
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };
}
