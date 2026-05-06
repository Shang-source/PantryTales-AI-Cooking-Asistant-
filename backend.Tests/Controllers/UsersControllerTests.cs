using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Users;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task GetCurrentAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeUserService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetCurrentAsync(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsNotFound_WhenUserNotInStore()
    {
        var fakeService = new FakeUserService { ProfileToReturn = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetCurrentAsync(CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsProfile_WhenFound()
    {
        var profile = NewProfile();
        var fakeService = new FakeUserService { ProfileToReturn = profile };
        var controller = CreateController(fakeService, BuildPrincipal(profile.ClerkUserId));

        var result = await controller.GetCurrentAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<ApiResponse<UserProfileResponseDto>>(ok.Value);
        Assert.Equal(0, envelope.Code);
        Assert.Equal("Ok", envelope.Message);
        Assert.Same(profile, envelope.Data);
    }

    [Fact]
    public async Task UpdateCurrentAsync_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var profile = NewProfile();
        var fakeService = new FakeUserService
        {
            UserLookupReturn = NewUser(profile),
            UpdateProfileReturn = null
        };
        var controller = CreateController(fakeService, BuildPrincipal(profile.ClerkUserId));
        var request = NewRequest();

        var result = await controller.UpdateCurrentAsync(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task UpdateCurrentAsync_PassesThroughRequest_AndReturnsUpdatedProfile()
    {
        var profile = NewProfile();
        var fakeService = new FakeUserService
        {
            UserLookupReturn = NewUser(profile),
            UpdateProfileReturn = profile
        };
        var controller = CreateController(fakeService, BuildPrincipal(profile.ClerkUserId));
        var request = NewRequest();

        var result = await controller.UpdateCurrentAsync(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<ApiResponse<UserProfileResponseDto>>(ok.Value);
        Assert.Equal(0, envelope.Code);
        Assert.Same(profile, envelope.Data);
        Assert.Equal(profile.Id, fakeService.LastUpdateUserId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    private static UsersController CreateController(IUserService service, ClaimsPrincipal user)
    {
        return new UsersController(service, NullLogger<UsersController>.Instance)
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

    private static UpdateUserProfileRequest NewRequest() =>
        new(
            Nickname: "UpdatedNickname",
            AvatarUrl: "https://example.com/new-avatar.png",
            Age: 25,
            Gender: UserGender.Female,
            Height: 170m,
            Weight: 60m,
            Preferences: new List<UpdateUserPreferenceDto>()
        );

    private static UserProfileResponseDto NewProfile() =>
        new(
            Id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ClerkUserId: "clerk_123",
            Email: "123@mail.com",
            Nickname: "ImmersiveDeviledEggs",
            AvatarUrl: "https://example.com/avatar.png",
            Age: 25,
            Gender: UserGender.Female,
            Height: 170m,
            Weight: 60m,
            CreatedAt: DateTime.Parse("2025-01-01T00:00:00Z"),
            UpdatedAt: DateTime.Parse("2025-01-01T00:00:00Z"),
            Preferences: new List<UserPreferenceDto>()
        );

    private static UserResponseDto NewUser(UserProfileResponseDto profile) =>
        new(profile.Id, profile.ClerkUserId, profile.Email, profile.Nickname);

    private sealed class FakeUserService : IUserService
    {
        public UserResponseDto? UserLookupReturn { get; set; }
        public UserProfileResponseDto? ProfileToReturn { get; set; }
        public UserProfileResponseDto? UpdateProfileReturn { get; set; }
        public Guid? LastUpdateUserId { get; private set; }
        public UpdateUserProfileRequest? LastUpdateRequest { get; private set; }

        public Task<UserResponseDto> GetOrCreateAsync(UserSyncPayload payload,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new UserResponseDto(Guid.Empty, payload.ClerkUserId, payload.Email, payload.Nickname));

        public Task<UserResponseDto?> GetByClerkUserIdAsync(string clerkUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(UserLookupReturn);

        public Task<UserProfileResponseDto?> GetProfileAsync(string clerkUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ProfileToReturn);

        public Task<UserProfileResponseDto?> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            LastUpdateUserId = userId;
            LastUpdateRequest = request;
            return Task.FromResult(UpdateProfileReturn);
        }
    }
}
