using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Households;
using backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class HouseholdsControllerTests
{
    #region GetMembersAsync Tests

    [Fact]
    public async Task GetMembersAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeHouseholdService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMembersAsync(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsNotFound_WhenHouseholdNotFound()
    {
        var fake = new FakeHouseholdService
        {
            MembersResult = HouseholdMembersResult.HouseholdNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.GetMembersAsync(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsUnauthorized_WhenUserNotFound()
    {
        var fake = new FakeHouseholdService
        {
            MembersResult = HouseholdMembersResult.UserNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.GetMembersAsync(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsForbidden_WhenNotMember()
    {
        var fake = new FakeHouseholdService
        {
            MembersResult = HouseholdMembersResult.NotMember
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.GetMembersAsync(Guid.NewGuid(), CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal(403, response.Code);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsOk_WhenSuccess()
    {
        var householdId = Guid.NewGuid();
        var members = new[]
        {
            new HouseholdMemberDetailDto(Guid.NewGuid(), "Owner", "owner@example.com", null, "owner", DateTime.UtcNow.AddDays(-10)),
            new HouseholdMemberDetailDto(Guid.NewGuid(), "Member", "member@example.com", null, "joined", DateTime.UtcNow.AddDays(-5)),
            new HouseholdMemberDetailDto(Guid.NewGuid(), "pending", "pending@example.com", null, "pending", DateTime.UtcNow)
        };
        var membersListDto = new HouseholdMembersListDto(householdId, "Test Household", 2, 1, members);
        var fake = new FakeHouseholdService
        {
            MembersResult = HouseholdMembersResult.Success(membersListDto)
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.GetMembersAsync(householdId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<HouseholdMembersListDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(householdId, response.Data!.HouseholdId);
        Assert.Equal("Test Household", response.Data.HouseholdName);
        Assert.Equal(2, response.Data.ActiveMemberCount);
        Assert.Equal(1, response.Data.PendingCount);
        Assert.Equal(3, response.Data.Members.Count);
        Assert.Equal("clerk_123", fake.LastClerkUserId);
        Assert.Equal(householdId, fake.LastHouseholdId);
    }

    [Fact]
    public async Task GetMembersAsync_PassesCorrectParametersToService()
    {
        var householdId = Guid.NewGuid();
        var membersListDto = new HouseholdMembersListDto(householdId, "Test", 1, 0,
            new[] { new HouseholdMemberDetailDto(Guid.NewGuid(), "User", "user@test.com", null, "owner", DateTime.UtcNow) });
        var fake = new FakeHouseholdService
        {
            MembersResult = HouseholdMembersResult.Success(membersListDto)
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_456"));

        await controller.GetMembersAsync(householdId, CancellationToken.None);

        Assert.Equal("clerk_456", fake.LastClerkUserId);
        Assert.Equal(householdId, fake.LastHouseholdId);
    }

    #endregion

    #region LeaveAsync Tests

    [Fact]
    public async Task LeaveAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeHouseholdService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.LeaveAsync(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task LeaveAsync_ReturnsNotFound_WhenHouseholdMissing()
    {
        var fake = new FakeHouseholdService
        {
            Result = HouseholdLeaveResult.HouseholdNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.LeaveAsync(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    [Fact]
    public async Task LeaveAsync_ReturnsUnauthorized_WhenUserNotFound()
    {
        var fake = new FakeHouseholdService
        {
            Result = HouseholdLeaveResult.UserNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.LeaveAsync(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task LeaveAsync_ReturnsForbidden_WhenNotMember()
    {
        var fake = new FakeHouseholdService
        {
            Result = HouseholdLeaveResult.NotMember
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.LeaveAsync(Guid.NewGuid(), CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal(403, response.Code);
    }

    [Fact]
    public async Task LeaveAsync_ReturnsBadRequest_WhenOwnerCannotLeave()
    {
        var fake = new FakeHouseholdService
        {
            Result = HouseholdLeaveResult.OwnerCannotLeave
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.LeaveAsync(Guid.NewGuid(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task LeaveAsync_ReturnsOk_WhenSuccess()
    {
        var activeId = Guid.NewGuid();
        var fake = new FakeHouseholdService
        {
            Result = HouseholdLeaveResult.Success(activeId, "Test Household", true)
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));
        var householdId = Guid.NewGuid();

        var result = await controller.LeaveAsync(householdId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<LeaveHouseholdResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(activeId, response.Data!.ActiveHouseholdId);
        Assert.Equal("Test Household", response.Data.ActiveHouseholdName);
        Assert.True(response.Data.IsNewlyCreatedDefault);
        Assert.Equal(householdId, fake.LastHouseholdId);
        Assert.Equal("clerk_123", fake.LastClerkUserId);
    }

    #endregion

    #region GetMembershipsAsync Tests

    [Fact]
    public async Task GetMembershipsAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeHouseholdService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMembershipsAsync(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetMembershipsAsync_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fake = new FakeHouseholdService
        {
            MembershipResult = null
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.GetMembershipsAsync(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("clerk_123", fake.LastClerkUserId);
    }

    [Fact]
    public async Task GetMembershipsAsync_ReturnsOk_WhenServiceReturnsData()
    {
        var membership = new HouseholdMembershipDto(Guid.NewGuid(), "Main", "owner", DateTime.UtcNow, Guid.NewGuid(), true);
        var fake = new FakeHouseholdService
        {
            MembershipResult = new HouseholdMembershipListDto(membership.HouseholdId, membership.HouseholdName,
                new[] { membership })
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.GetMembershipsAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<HouseholdMembershipListDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(membership.HouseholdId, response.Data!.ActiveHouseholdId);
        Assert.Single(response.Data.Memberships);
        Assert.Equal("clerk_123", fake.LastClerkUserId);
    }

    #endregion

    #region RemoveMemberAsync Tests

    [Fact]
    public async Task RemoveMemberAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeHouseholdService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsNotFound_WhenHouseholdNotFound()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.HouseholdNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsUnauthorized_WhenUserNotFound()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.UserNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsForbidden_WhenNotOwner()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.NotOwner
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal(403, response.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsNotFound_WhenMemberNotFound()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.MemberNotFound
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsBadRequest_WhenCannotRemoveOwner()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.CannotRemoveOwner
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsOk_WhenSuccess()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.Success
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var result = await controller.RemoveMemberAsync(householdId, memberId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal(householdId, fake.LastHouseholdId);
        Assert.Equal(memberId, fake.LastMemberId);
        Assert.Equal("clerk_123", fake.LastClerkUserId);
    }

    [Fact]
    public async Task RemoveMemberAsync_PassesCorrectParametersToService()
    {
        var fake = new FakeHouseholdService
        {
            RemoveResult = RemoveMemberResult.Success
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_456"));
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await controller.RemoveMemberAsync(householdId, memberId, CancellationToken.None);

        Assert.Equal("clerk_456", fake.LastClerkUserId);
        Assert.Equal(householdId, fake.LastHouseholdId);
        Assert.Equal(memberId, fake.LastMemberId);
    }

    #endregion

    private static HouseholdsController CreateController(IHouseholdService service, ClaimsPrincipal user)
    {
        return new HouseholdsController(service, NullLogger<HouseholdsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId)
    {
        var identity = new ClaimsIdentity([new Claim("clerk_user_id", clerkUserId)], "mock");
        return new ClaimsPrincipal(identity);
    }

    private sealed class FakeHouseholdService : IHouseholdService
    {
        public HouseholdLeaveResult Result { get; set; } = HouseholdLeaveResult.HouseholdNotFound;
        public HouseholdMembershipListDto? MembershipResult { get; set; } = null;
        public HouseholdMembersResult MembersResult { get; set; } = HouseholdMembersResult.HouseholdNotFound;
        public RemoveMemberResult RemoveResult { get; set; } = RemoveMemberResult.HouseholdNotFound;
        public Guid? LastHouseholdId { get; private set; }
        public Guid? LastMemberId { get; private set; }
        public string? LastClerkUserId { get; private set; }

        public Task<HouseholdLeaveResult> LeaveHouseholdAsync(Guid householdId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastHouseholdId = householdId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(Result);
        }

        public Task<HouseholdMembershipListDto?> GetMembershipsAsync(string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(MembershipResult);
        }

        public Task<Guid?> GetActiveHouseholdIdAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Guid?>(null);
        }

        public Task<HouseholdMembersResult> GetHouseholdMembersAsync(Guid householdId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastHouseholdId = householdId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(MembersResult);
        }

        public Task<RemoveMemberResult> RemoveMemberAsync(Guid householdId, Guid memberId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastHouseholdId = householdId;
            LastMemberId = memberId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(RemoveResult);
        }
    }
}
