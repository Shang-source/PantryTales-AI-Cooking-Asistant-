using System.Security.Claims;
using backend.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace backend.Auth;

/// <summary>
/// Claims transformation that adds the user's role from the database to the claims principal.
/// This enables role-based authorization in production when using Clerk JWT authentication.
/// </summary>
public class RoleClaimsTransformation(IServiceProvider serviceProvider) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Skip if already has role claim (e.g., from development auth handler)
        if (principal.HasClaim(c => c.Type == ClaimTypes.Role))
        {
            return principal;
        }

        // Get Clerk user ID from claims
        var clerkUserId = principal.FindFirstValue("clerk_user_id")
                       ?? principal.FindFirstValue("sub")
                       ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(clerkUserId))
        {
            return principal;
        }

        // Look up user role from database
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.ClerkUserId == clerkUserId)
            .Select(u => new { u.Role })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return principal;
        }

        // Add role claim to identity
        var identity = principal.Identity as ClaimsIdentity;
        identity?.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        return principal;
    }
}
