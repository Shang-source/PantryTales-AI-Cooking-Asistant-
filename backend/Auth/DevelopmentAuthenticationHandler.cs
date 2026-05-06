using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using backend.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace backend.Auth;

public class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<DevelopmentUserOptions> devUserOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private readonly DevelopmentUserOptions _devUser = devUserOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = headerValue["Bearer ".Length..].Trim();
        var expectedToken = string.IsNullOrWhiteSpace(_devUser.BearerToken)
            ? DevelopmentAuthenticationDefaults.DefaultBearerToken
            : _devUser.BearerToken;

        if (!string.Equals(token, expectedToken, StringComparison.Ordinal))
        {
            Logger.LogWarning("Rejected development auth request due to token mismatch.");
            return Task.FromResult(AuthenticateResult.Fail("Invalid development bearer token."));
        }

        var claims = BuildClaims();
        var identity = new ClaimsIdentity(claims, DevelopmentAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DevelopmentAuthenticationDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private IEnumerable<Claim> BuildClaims()
    {
        yield return new Claim("clerk_user_id", _devUser.ClerkUserId);
        yield return new Claim(ClaimTypes.NameIdentifier, _devUser.ClerkUserId);
        yield return new Claim("email", _devUser.Email);
        yield return new Claim(ClaimTypes.Email, _devUser.Email);
        yield return new Claim("preferred_username", _devUser.Nickname);
        yield return new Claim("nickname", _devUser.Nickname);
        yield return new Claim(ClaimTypes.Name, _devUser.Nickname);
        yield return new Claim(ClaimTypes.Role, _devUser.Role);
    }
}
