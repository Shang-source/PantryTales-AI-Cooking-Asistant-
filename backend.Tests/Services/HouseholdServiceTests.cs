using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos.Households;
using backend.Interfaces;
using backend.Models;
using backend.Repository;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Services;

public class HouseholdServiceTests
{
    [Fact]
    public async Task GetMembershipsAsync_ReturnsNull_WhenUserNotFound()
    {
        using var context = CreateInMemoryContext();
        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("nonexistent_clerk_id", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMembershipsAsync_CreatesDefaultHousehold_WhenUserHasNoMemberships()
    {
        using var context = CreateInMemoryContext();
        var user = new User { Id = Guid.NewGuid(), ClerkUserId = "clerk_123", Email = "test@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("clerk_123", CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result!.ActiveHouseholdId);
        Assert.Single(result.Memberships);
        var membership = result.Memberships.First();
        Assert.Equal("owner", membership.Role);
    }

    [Fact]
    public async Task GetMembershipsAsync_ReturnsMemberships_WhenUserHasExistingMemberships()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com" };
        var extraUserId = Guid.NewGuid();
        var extraUser = new User { Id = extraUserId, ClerkUserId = "clerk_extra", Email = "extra@example.com" };
        var household1 = new Household { Id = Guid.NewGuid(), Name = "Household 1", OwnerId = userId };
        var household2 = new Household { Id = Guid.NewGuid(), Name = "Household 2", OwnerId = Guid.NewGuid() };

        var member1 = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = household1.Id,
            Household = household1,
            User = user,
            Role = "owner",
            JoinedAt = DateTime.UtcNow.AddDays(-10)
        };

        var member2 = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = household2.Id,
            Household = household2,
            User = user,
            Role = "member",
            JoinedAt = DateTime.UtcNow.AddDays(-5)
        };

        var extraMember = new HouseholdMember
        {
            UserId = extraUserId,
            HouseholdId = household1.Id,
            Household = household1,
            User = extraUser,
            Role = "member",
            JoinedAt = DateTime.UtcNow.AddDays(-8)
        };

        context.Users.AddRange(user, extraUser);
        context.Households.AddRange(household1, household2);
        context.HouseholdMembers.AddRange(member1, member2, extraMember);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("clerk_123", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Memberships.Count);
        // Owner household should be first
        Assert.Equal("owner", result.Memberships.First().Role);
        Assert.Equal(household1.Id, result.ActiveHouseholdId);
    }

    [Fact]
    public async Task GetMembershipsAsync_OrdersMembershipsByRole_ThenByJoinedAt()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com" };
        var extraUserId = Guid.NewGuid();
        var extraUser = new User { Id = extraUserId, ClerkUserId = "clerk_extra", Email = "extra@example.com" };
        var baseTime = DateTime.UtcNow;

        // Create multiple households with different roles and join dates
        var households = Enumerable.Range(1, 4).Select(i => new Household
        {
            Id = Guid.NewGuid(),
            Name = $"Household {i}",
            OwnerId = i == 1 ? userId : Guid.NewGuid()
        }).ToList();

        var members = new[]
        {
            new HouseholdMember // Owner, oldest
            {
                UserId = userId,
                HouseholdId = households[0].Id,
                Household = households[0],
                User = user,
                Role = "owner",
                JoinedAt = baseTime.AddDays(-20)
            },
            new HouseholdMember // Member, newest
            {
                UserId = userId,
                HouseholdId = households[1].Id,
                Household = households[1],
                User = user,
                Role = "member",
                JoinedAt = baseTime.AddDays(-1)
            },
            new HouseholdMember // Member, middle
            {
                UserId = userId,
                HouseholdId = households[2].Id,
                Household = households[2],
                User = user,
                Role = "member",
                JoinedAt = baseTime.AddDays(-10)
            },
            new HouseholdMember // Member, oldest
            {
                UserId = userId,
                HouseholdId = households[3].Id,
                Household = households[3],
                User = user,
                Role = "member",
                JoinedAt = baseTime.AddDays(-30)
            }
        };

        var extraMember = new HouseholdMember
        {
            UserId = extraUserId,
            HouseholdId = households[0].Id,
            Household = households[0],
            User = extraUser,
            Role = "member",
            JoinedAt = baseTime.AddDays(-15)
        };

        context.Users.AddRange(user, extraUser);
        context.Households.AddRange(households);
        context.HouseholdMembers.AddRange(members);
        context.HouseholdMembers.Add(extraMember);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("clerk_123", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4, result!.Memberships.Count);

        // First should be owner
        Assert.Equal("owner", result.Memberships[0].Role);

        // Remaining should be members sorted by JoinedAt
        var memberMemberships = result.Memberships.Skip(1).ToList();
        Assert.All(memberMemberships, m => Assert.Equal("member", m.Role));

        var joinDates = memberMemberships.Select(m => m.JoinedAt).ToList();
        var sortedJoinDates = joinDates.OrderBy(d => d).ToList();
        Assert.Equal(sortedJoinDates, joinDates);
    }

    [Fact]
    public async Task GetMembershipsAsync_IncludesHouseholdOwnerIdInDto()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        var member = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = household.Id,
            Household = household,
            User = user,
            Role = "member",
            JoinedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.Households.Add(household);
        context.HouseholdMembers.Add(member);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("clerk_123", CancellationToken.None);

        Assert.NotNull(result);
        var membership = result!.Memberships.First();
        Assert.Equal(ownerId, membership.OwnerId);
    }

    [Fact]
    public async Task GetMembershipsAsync_SetsIsOwnerFlag_Correctly()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com" };
        var extraUserId = Guid.NewGuid();
        var extraUser = new User { Id = extraUserId, ClerkUserId = "clerk_extra", Email = "extra@example.com" };
        var household1 = new Household { Id = Guid.NewGuid(), Name = "Owned", OwnerId = userId };
        var household2 = new Household { Id = Guid.NewGuid(), Name = "Not Owned", OwnerId = Guid.NewGuid() };

        var member1 = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = household1.Id,
            Household = household1,
            User = user,
            Role = "owner",
            JoinedAt = DateTime.UtcNow
        };

        var member2 = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = household2.Id,
            Household = household2,
            User = user,
            Role = "member",
            JoinedAt = DateTime.UtcNow
        };

        var extraMember = new HouseholdMember
        {
            UserId = extraUserId,
            HouseholdId = household1.Id,
            Household = household1,
            User = extraUser,
            Role = "member",
            JoinedAt = DateTime.UtcNow
        };

        context.Users.AddRange(user, extraUser);
        context.Households.AddRange(household1, household2);
        context.HouseholdMembers.AddRange(member1, member2, extraMember);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("clerk_123", CancellationToken.None);

        Assert.NotNull(result);
        var ownedMembership = result!.Memberships.First(m => m.HouseholdName == "Owned");
        var notOwnedMembership = result.Memberships.First(m => m.HouseholdName == "Not Owned");

        Assert.True(ownedMembership.IsOwner);
        Assert.False(notOwnedMembership.IsOwner);
    }

    [Fact]
    public async Task GetMembershipsAsync_SetActiveHouseholdToFirstOwner()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com" };
        var ownerHousehold = new Household { Id = Guid.NewGuid(), Name = "Owner Household", OwnerId = userId };

        var member = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = ownerHousehold.Id,
            Household = ownerHousehold,
            User = user,
            Role = "owner",
            JoinedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.Households.Add(ownerHousehold);
        context.HouseholdMembers.Add(member);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetMembershipsAsync("clerk_123", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ownerHousehold.Id, result!.ActiveHouseholdId);
        Assert.Equal("Owner Household", result.ActiveHouseholdName);
    }

    #region GetHouseholdMembersAsync Tests

    [Fact]
    public async Task GetHouseholdMembersAsync_ReturnsUserNotFound_WhenUserDoesNotExist()
    {
        using var context = CreateInMemoryContext();
        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(Guid.NewGuid(), "nonexistent_clerk_id", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.UserNotFound, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_ReturnsHouseholdNotFound_WhenHouseholdDoesNotExist()
    {
        using var context = CreateInMemoryContext();
        var user = new User { Id = Guid.NewGuid(), ClerkUserId = "clerk_123", Email = "test@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(Guid.NewGuid(), "clerk_123", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.HouseholdNotFound, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_ReturnsNotMember_WhenUserIsNotMember()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com" };
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        context.Users.AddRange(user, owner);
        context.Households.Add(household);
        // Note: user is NOT a member of this household
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(household.Id, "clerk_123", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.NotMember, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_ReturnsSuccess_WithOwnerMember()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, ClerkUserId = "clerk_123", Email = "test@example.com", Nickname = "Test User" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = userId };

        var member = new HouseholdMember
        {
            UserId = userId,
            HouseholdId = household.Id,
            Household = household,
            User = user,
            Role = "owner",
            JoinedAt = DateTime.UtcNow.AddDays(-10)
        };

        context.Users.Add(user);
        context.Households.Add(household);
        context.HouseholdMembers.Add(member);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(household.Id, "clerk_123", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(household.Id, result.Data!.HouseholdId);
        Assert.Equal("Test Household", result.Data.HouseholdName);
        Assert.Equal(1, result.Data.ActiveMemberCount);
        Assert.Equal(0, result.Data.PendingCount);
        Assert.Single(result.Data.Members);

        var ownerMember = result.Data.Members.First();
        Assert.Equal(userId, ownerMember.Id);
        Assert.Equal("owner", ownerMember.Status);
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_ReturnsSuccess_WithMultipleMembers()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com", Nickname = "Owner" };
        var memberUser = new User { Id = memberId, ClerkUserId = "clerk_member", Email = "member@example.com", Nickname = "Member" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Family Home", OwnerId = ownerId };

        var ownerMember = new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Household = household,
            User = owner,
            Role = "owner",
            JoinedAt = DateTime.UtcNow.AddDays(-30)
        };

        var regularMember = new HouseholdMember
        {
            UserId = memberId,
            HouseholdId = household.Id,
            Household = household,
            User = memberUser,
            Role = "member",
            JoinedAt = DateTime.UtcNow.AddDays(-5)
        };

        context.Users.AddRange(owner, memberUser);
        context.Households.Add(household);
        context.HouseholdMembers.AddRange(ownerMember, regularMember);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(household.Id, "clerk_owner", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.ActiveMemberCount);
        Assert.Equal(0, result.Data.PendingCount);
        Assert.Equal(2, result.Data.Members.Count);

        // Check statuses
        Assert.Contains(result.Data.Members, m => m.Status == "owner");
        Assert.Contains(result.Data.Members, m => m.Status == "joined");
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_IncludesPendingInvitations()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com", Nickname = "Owner" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Home", OwnerId = ownerId };

        var ownerMember = new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Household = household,
            User = owner,
            Role = "owner",
            JoinedAt = DateTime.UtcNow.AddDays(-30)
        };

        // Add a pending invitation
        var invitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Email = "pending@example.com",
            Status = "pending",
            ExpiredAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(owner);
        context.Households.Add(household);
        context.HouseholdMembers.Add(ownerMember);
        context.HouseholdInvitations.Add(invitation);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(household.Id, "clerk_owner", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.ActiveMemberCount);
        Assert.Equal(1, result.Data.PendingCount);
        Assert.Equal(2, result.Data.Members.Count);

        // Verify pending invitation is included
        var pendingMember = result.Data.Members.FirstOrDefault(m => m.Status == "pending");
        Assert.NotNull(pendingMember);
        Assert.Equal("pending@example.com", pendingMember!.Email);
        Assert.Equal(invitation.Id, pendingMember.Id);
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_DoesNotIncludeNonPendingInvitations()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com", Nickname = "Owner" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Home", OwnerId = ownerId };

        var ownerMember = new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Household = household,
            User = owner,
            Role = "owner",
            JoinedAt = DateTime.UtcNow.AddDays(-30)
        };

        // Add accepted and expired invitations (should NOT be included)
        var acceptedInvitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Email = "accepted@example.com",
            Status = "accepted",
            ExpiredAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var expiredInvitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Email = "expired@example.com",
            Status = "expired",
            ExpiredAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        context.Users.Add(owner);
        context.Households.Add(household);
        context.HouseholdMembers.Add(ownerMember);
        context.HouseholdInvitations.AddRange(acceptedInvitation, expiredInvitation);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.GetHouseholdMembersAsync(household.Id, "clerk_owner", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.ActiveMemberCount);
        Assert.Equal(0, result.Data.PendingCount);
        Assert.Single(result.Data.Members);
        Assert.DoesNotContain(result.Data.Members, m => m.Status == "pending");
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_RegularMemberCanViewMembers()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com", Nickname = "Owner" };
        var memberUser = new User { Id = memberId, ClerkUserId = "clerk_member", Email = "member@example.com", Nickname = "Member" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Family Home", OwnerId = ownerId };

        var ownerMember = new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Household = household,
            User = owner,
            Role = "owner",
            JoinedAt = DateTime.UtcNow.AddDays(-30)
        };

        var regularMember = new HouseholdMember
        {
            UserId = memberId,
            HouseholdId = household.Id,
            Household = household,
            User = memberUser,
            Role = "member",
            JoinedAt = DateTime.UtcNow.AddDays(-5)
        };

        context.Users.AddRange(owner, memberUser);
        context.Households.Add(household);
        context.HouseholdMembers.AddRange(ownerMember, regularMember);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        // Regular member requests the members list
        var result = await service.GetHouseholdMembersAsync(household.Id, "clerk_member", CancellationToken.None);

        Assert.Equal(HouseholdMembersResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.ActiveMemberCount);
    }

    #endregion

    #region RemoveMemberAsync Tests

    [Fact]
    public async Task RemoveMemberAsync_ReturnsUserNotFound_WhenRequestingUserDoesNotExist()
    {
        using var context = CreateInMemoryContext();
        var service = CreateHouseholdService(context);

        var result = await service.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), "nonexistent_clerk_id", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.UserNotFound, result.Status);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsHouseholdNotFound_WhenHouseholdDoesNotExist()
    {
        using var context = CreateInMemoryContext();
        var user = new User { Id = Guid.NewGuid(), ClerkUserId = "clerk_123", Email = "test@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), "clerk_123", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.HouseholdNotFound, result.Status);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsNotOwner_WhenRequestingUserIsNotOwner()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var member = new User { Id = memberId, ClerkUserId = "clerk_member", Email = "member@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        context.Users.AddRange(owner, member);
        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = memberId,
            HouseholdId = household.Id,
            Role = "member"
        });
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        // Non-owner tries to remove someone
        var result = await service.RemoveMemberAsync(household.Id, ownerId, "clerk_member", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.NotOwner, result.Status);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsCannotRemoveOwner_WhenTryingToRemoveOwner()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        context.Users.Add(owner);
        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Role = "owner"
        });
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        // Owner tries to remove themselves
        var result = await service.RemoveMemberAsync(household.Id, ownerId, "clerk_owner", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.CannotRemoveOwner, result.Status);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsMemberNotFound_WhenMemberDoesNotExist()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        context.Users.Add(owner);
        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Role = "owner"
        });
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        // Try to remove non-existent member
        var result = await service.RemoveMemberAsync(household.Id, Guid.NewGuid(), "clerk_owner", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.MemberNotFound, result.Status);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsSuccess_WhenRemovingMember()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var member = new User { Id = memberId, ClerkUserId = "clerk_member", Email = "member@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        context.Users.AddRange(owner, member);
        context.Households.Add(household);
        context.HouseholdMembers.AddRange(
            new HouseholdMember { UserId = ownerId, HouseholdId = household.Id, Role = "owner" },
            new HouseholdMember { UserId = memberId, HouseholdId = household.Id, Role = "member" }
        );
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.RemoveMemberAsync(household.Id, memberId, "clerk_owner", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.Success, result.Status);

        // Verify member was removed
        var remainingMembers = await context.HouseholdMembers
            .Where(m => m.HouseholdId == household.Id)
            .ToListAsync();
        Assert.Single(remainingMembers);
        Assert.Equal(ownerId, remainingMembers[0].UserId);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsSuccess_WhenCancellingPendingInvitation()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        var invitation = new HouseholdInvitation
        {
            Id = invitationId,
            HouseholdId = household.Id,
            Email = "invited@example.com",
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = DateTime.UtcNow.AddDays(7)
        };

        context.Users.Add(owner);
        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Role = "owner"
        });
        context.HouseholdInvitations.Add(invitation);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        var result = await service.RemoveMemberAsync(household.Id, invitationId, "clerk_owner", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.Success, result.Status);

        // Verify invitation was removed
        var remainingInvitations = await context.HouseholdInvitations
            .Where(i => i.HouseholdId == household.Id)
            .ToListAsync();
        Assert.Empty(remainingInvitations);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsMemberNotFound_WhenInvitationIsNotPending()
    {
        using var context = CreateInMemoryContext();
        var ownerId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var owner = new User { Id = ownerId, ClerkUserId = "clerk_owner", Email = "owner@example.com" };
        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household", OwnerId = ownerId };

        var invitation = new HouseholdInvitation
        {
            Id = invitationId,
            HouseholdId = household.Id,
            Email = "invited@example.com",
            Status = "accepted", // Not pending
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = DateTime.UtcNow.AddDays(7)
        };

        context.Users.Add(owner);
        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = ownerId,
            HouseholdId = household.Id,
            Role = "owner"
        });
        context.HouseholdInvitations.Add(invitation);
        await context.SaveChangesAsync();

        var service = CreateHouseholdService(context);

        // Try to cancel an already accepted invitation
        var result = await service.RemoveMemberAsync(household.Id, invitationId, "clerk_owner", CancellationToken.None);

        Assert.Equal(RemoveMemberResultStatus.MemberNotFound, result.Status);
    }

    #endregion

    private static AppDbContext CreateInMemoryContext()
    {
        // Use EF Core InMemory provider for tests to avoid applying PostgreSQL migrations
        var dbName = "TestDb_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // InMemory provider doesn't use a physical connection, pass null for connection
        var context = new TestAppDbContext(options, null);
        // InMemory provider does not require EnsureCreated; skip to avoid provider-specific behavior
        return context;
    }
    private static HouseholdService CreateHouseholdService(AppDbContext context)
    {
        var repository = new HouseholdMembershipRepository(context);
        return new HouseholdService(context, repository, NullLogger<HouseholdService>.Instance);
    }

    private sealed class TestAppDbContext : AppDbContext
    {
        private readonly SqliteConnection? connection;

        public TestAppDbContext(DbContextOptions<AppDbContext> options, SqliteConnection? connection)
            : base(options)
        {
            this.connection = connection;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<backend.Models.User>().Ignore(u => u.Embedding);
            modelBuilder.Entity<backend.Models.Ingredient>().Ignore(i => i.Embedding);
            modelBuilder.Entity<backend.Models.Recipe>().Ignore(r => r.Embedding);
        }
        public override void Dispose()
        {
            base.Dispose();
            if (connection != null)
            {
                connection.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (connection != null)
            {
                await connection.DisposeAsync();
            }
        }
    }
}
