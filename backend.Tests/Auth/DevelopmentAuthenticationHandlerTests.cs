using System;
using System.Security.Claims;
using System.Threading.Tasks;
using backend.Auth;
using backend.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace backend.Tests.Auth;

public class DevelopmentAuthenticationHandlerTests
{
    [Fact]
    public async Task HandleAuthenticateAsync_IncludesRoleClaim()
    {
        var devUserOptions = new DevelopmentUserOptions
        {
            ClerkUserId = "test-user",
            Email = "test@example.com",
            Nickname = "Test User",
            BearerToken = "test-token",
            Role = "Admin"
        };

        var handler = await CreateHandlerAsync(devUserOptions, "Bearer test-token");

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);

        var roleClaim = result.Principal.FindFirst(ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal("Admin", roleClaim.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_IncludesAllExpectedClaims()
    {
        var devUserOptions = new DevelopmentUserOptions
        {
            ClerkUserId = "clerk-123",
            Email = "user@example.com",
            Nickname = "Test",
            BearerToken = "valid-token",
            Role = "User"
        };

        var handler = await CreateHandlerAsync(devUserOptions, "Bearer valid-token");

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var principal = result.Principal!;

        Assert.Equal("clerk-123", principal.FindFirst("clerk_user_id")?.Value);
        Assert.Equal("clerk-123", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("user@example.com", principal.FindFirst("email")?.Value);
        Assert.Equal("user@example.com", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Test", principal.FindFirst("nickname")?.Value);
        Assert.Equal("Test", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("User", principal.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_RejectsInvalidToken()
    {
        var devUserOptions = new DevelopmentUserOptions
        {
            BearerToken = "correct-token"
        };

        var handler = await CreateHandlerAsync(devUserOptions, "Bearer wrong-token");

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsNoResult_WhenNoAuthHeader()
    {
        var devUserOptions = new DevelopmentUserOptions();

        var handler = await CreateHandlerAsync(devUserOptions, null);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    private static async Task<DevelopmentAuthenticationHandler> CreateHandlerAsync(
        DevelopmentUserOptions devUserOptions,
        string? authorizationHeader)
    {
        var options = new TestOptionsMonitor(new AuthenticationSchemeOptions());
        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = System.Text.Encodings.Web.UrlEncoder.Default;
        var devUserOptionsWrapper = Microsoft.Extensions.Options.Options.Create(devUserOptions);

        var handler = new DevelopmentAuthenticationHandler(
            options,
            loggerFactory,
            encoder,
            devUserOptionsWrapper);

        var context = new DefaultHttpContext();
        if (authorizationHeader != null)
        {
            context.Request.Headers[HeaderNames.Authorization] = authorizationHeader;
        }

        var scheme = new AuthenticationScheme(
            DevelopmentAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(DevelopmentAuthenticationHandler));

        await handler.InitializeAsync(scheme, context);

        return handler;
    }

    private class TestOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        private readonly AuthenticationSchemeOptions _options;

        public TestOptionsMonitor(AuthenticationSchemeOptions options) => _options = options;

        public AuthenticationSchemeOptions CurrentValue => _options;

        public AuthenticationSchemeOptions Get(string? name) => _options;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
