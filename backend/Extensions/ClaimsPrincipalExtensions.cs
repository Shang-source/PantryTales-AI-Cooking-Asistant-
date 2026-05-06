using System.Security.Claims;
using System.Text.Json;
using backend.Dtos.Users;

namespace backend.Extensions;

public static class ClaimsPrincipalExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildUserSyncPayload(this ClaimsPrincipal principal, out UserSyncPayload payload, out string? failureReason)
    {
        payload = default!;
        failureReason = null;

        if (!principal.TryGetClerkUserId(out var clerkUserId, out failureReason))
        {
            return false;
        }

        var email = ResolveEmail(principal, out var emailFailureReason);
        if (string.IsNullOrWhiteSpace(email))
        {
            failureReason = emailFailureReason ?? "Missing email claims.";
            return false;
        }

        var nickname = ResolveNickname(principal, email);
        payload = new UserSyncPayload(clerkUserId!, email, nickname);
        return true;
    }

    public static bool TryGetClerkUserId(this ClaimsPrincipal principal, out string? clerkUserId,
        out string? failureReason)
    {
        failureReason = null;
        clerkUserId = GetFirstClaimValue(principal,
            "clerk_user_id",
            "user_id",
            "https://clerk.dev/user_id",
            "https://www.clerk.dev/user_id",
            "sub",
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            failureReason = "Missing Clerk user id claims.";
            return false;
        }

        return true;
    }

    public static bool TryGetClerkUserId(this ClaimsPrincipal principal, out string clerkUserId)
    {
        var success = principal.TryGetClerkUserId(out var value, out _);
        clerkUserId = value ?? string.Empty;
        return success;
    }

    private static string? ResolveEmail(ClaimsPrincipal principal, out string? failureReason)
    {
        failureReason = null;

        var directEmail = GetFirstClaimValue(principal,
            ClaimTypes.Email,
            "email",
            "email_address",
            "https://clerk.dev/email_address",
            "https://www.clerk.dev/email_address");

        if (!string.IsNullOrWhiteSpace(directEmail))
        {
            return directEmail;
        }

        var primaryEmailId = GetFirstClaimValue(principal,
            "primary_email_address_id",
            "https://clerk.dev/primary_email_address_id",
            "https://www.clerk.dev/primary_email_address_id");

        var addressesClaim = GetFirstClaimValue(principal,
            "email_addresses",
            "https://clerk.dev/email_addresses",
            "https://www.clerk.dev/email_addresses");
        if (string.IsNullOrWhiteSpace(addressesClaim))
        {
            failureReason = "Email addresses claim is missing.";
            return null;
        }

        try
        {
            var addresses = JsonSerializer.Deserialize<List<ClerkEmailAddressClaim>>(addressesClaim, JsonOptions);
            if (addresses is null || addresses.Count == 0)
            {
                failureReason = "Email addresses claim could not be parsed.";
                return null;
            }

            var selected = !string.IsNullOrWhiteSpace(primaryEmailId)
                ? addresses.FirstOrDefault(address => address.Id == primaryEmailId)
                : null;

            return (selected ?? addresses.First()).EmailAddress;
        }
        catch (JsonException)
        {
            failureReason = "Email addresses claim is not valid JSON.";
            return null;
        }
    }

    private static string ResolveNickname(ClaimsPrincipal principal, string fallbackEmail)
    {
        var nickname = GetFirstClaimValue(principal,
            ClaimTypes.Name,
            "preferred_username",
            "username",
            "nickname");

        if (!string.IsNullOrWhiteSpace(nickname))
        {
            return nickname!;
        }

        var fullName = ResolveFullName(principal);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName!;
        }

        var atIndex = fallbackEmail.IndexOf('@');
        return atIndex > 0 ? fallbackEmail[..atIndex] : fallbackEmail;
    }

    private static string? ResolveFullName(ClaimsPrincipal principal)
    {
        var firstName = GetFirstClaimValue(principal, ClaimTypes.GivenName, "first_name");
        var lastName = GetFirstClaimValue(principal, ClaimTypes.Surname, "last_name");

        var parts = new[] { firstName, lastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    private static string? GetFirstClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private sealed record ClerkEmailAddressClaim(string Id, string EmailAddress);
}
