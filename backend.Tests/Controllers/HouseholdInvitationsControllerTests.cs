using System;
using System.Collections.Generic;
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

public class HouseholdInvitationsControllerTests
{
    [Fact]
    public async Task InviteAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeInvitationService(), new ClaimsPrincipal(new ClaimsIdentity()));
        var result = await controller.InviteAsync(Guid.NewGuid(), new InviteHouseholdMemberRequest
        {
            Email = "friend@example.com"
        }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task InviteAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var invitationDto = new HouseholdInvitationResponseDto(Guid.NewGuid(), Guid.NewGuid(), "friend@example.com",
            "pending", DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        var fake = new FakeInvitationService
        {
            CreateResult = new HouseholdInvitationCreateResult(HouseholdInvitationCreateStatus.Success, invitationDto)
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_123"));

        var result = await controller.InviteAsync(Guid.NewGuid(), new InviteHouseholdMemberRequest
        {
            Email = "friend@example.com"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<HouseholdInvitationResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Same(invitationDto, response.Data);
        Assert.Equal("clerk_123", fake.LastInviterClerkUserId);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsUnauthorized_WhenMissingClerkId()
    {
        var controller = CreateController(new FakeInvitationService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.AcceptAsync(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsOk_WhenSuccess()
    {
        var membership = new HouseholdMembershipDto(Guid.NewGuid(), "My House", "member", DateTime.UtcNow, Guid.NewGuid(), false);
        var fake = new FakeInvitationService
        {
            AcceptResult = new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.Success, membership)
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_456"));

        var result = await controller.AcceptAsync(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<HouseholdMembershipDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Same(membership, response.Data);
        Assert.Equal("clerk_456", fake.LastAcceptClerkUserId);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsNotFound_WhenServiceReturnsMissing()
    {
        var fake = new FakeInvitationService
        {
            AcceptResult = new HouseholdInvitationAcceptResult(HouseholdInvitationAcceptStatus.InvitationNotFound,
                FailureReason: "missing")
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_456"));

        var result = await controller.AcceptAsync(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    private static HouseholdInvitationsController CreateController(IHouseholdInvitationService service, ClaimsPrincipal user)
    {
        return new HouseholdInvitationsController(service, NullLogger<HouseholdInvitationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    [Fact]
    public async Task ListAsync_ReturnsUnauthorized_WhenMissingClerkId()
    {
        var controller = CreateController(new FakeInvitationService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ListAsync(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var invitations = new[]
        {
            new HouseholdInvitationResponseDto(Guid.NewGuid(), Guid.NewGuid(), "friend@example.com", "pending",
                DateTime.UtcNow.AddDays(7), DateTime.UtcNow)
        };
        var fake = new FakeInvitationService
        {
            ListResult = new HouseholdInvitationListResult(HouseholdInvitationListStatus.Success, invitations)
        };
        var controller = CreateController(fake, BuildPrincipal("clerk_999"));

        var result = await controller.ListAsync(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<HouseholdInvitationResponseDto>>>(ok.Value);
        Assert.Equal(0, response.Code);
        var data = Assert.IsAssignableFrom<IReadOnlyList<HouseholdInvitationResponseDto>>(response.Data);
        Assert.Single(data);
        Assert.Equal("clerk_999", fake.LastInviterClerkUserId);
    }

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("clerk_user_id", clerkUserId) }, "mock");
        return new ClaimsPrincipal(identity);
    }

    private sealed class FakeInvitationService : IHouseholdInvitationService
    {
        public HouseholdInvitationCreateResult CreateResult { get; set; } =
            new(HouseholdInvitationCreateStatus.HouseholdNotFound);

        public HouseholdInvitationAcceptResult AcceptResult { get; set; } =
            new(HouseholdInvitationAcceptStatus.InvitationNotFound);

        public HouseholdInvitationListResult ListResult { get; set; } =
            new(HouseholdInvitationListStatus.HouseholdNotFound);

        public string? LastInviterClerkUserId { get; private set; }
        public string? LastAcceptClerkUserId { get; private set; }

        public Task<HouseholdInvitationCreateResult> CreateInvitationAsync(Guid householdId, string inviterClerkUserId,
            InviteHouseholdMemberRequest request, CancellationToken cancellationToken = default)
        {
            LastInviterClerkUserId = inviterClerkUserId;
            return Task.FromResult(CreateResult);
        }

        public Task<HouseholdInvitationCreateResult> CreateLinkInvitationAsync(Guid householdId, string inviterClerkUserId,
            CreateLinkInvitationRequest request, CancellationToken cancellationToken = default)
        {
            LastInviterClerkUserId = inviterClerkUserId;
            return Task.FromResult(CreateResult);
        }

        public Task<HouseholdInvitationAcceptResult> AcceptInvitationAsync(Guid invitationId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastAcceptClerkUserId = clerkUserId;
            return Task.FromResult(AcceptResult);
        }

        public Task<HouseholdInvitationAcceptResult> AcceptInvitationByEmailAsync(Guid invitationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AcceptResult);
        }

        public Task<HouseholdInvitationAcceptResult> AcceptInvitationByTokenAsync(string token, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastAcceptClerkUserId = clerkUserId;
            return Task.FromResult(AcceptResult);
        }

        public Task<HouseholdInvitationListResult> ListInvitationsAsync(Guid householdId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastInviterClerkUserId = clerkUserId;
            return Task.FromResult(ListResult);
        }

        public Task<HouseholdInvitationListResult> GetActiveLinkInvitationAsync(Guid householdId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastInviterClerkUserId = clerkUserId;
            return Task.FromResult(ListResult);
        }
    }
}
