using backend.Models;
using Microsoft.AspNetCore.Authorization;

namespace backend.Auth;

/// <summary>
/// Authorization attribute that requires the user to have the Admin role.
/// Apply to controllers or actions that should only be accessible to administrators.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : AuthorizeAttribute
{
    public RequireAdminAttribute() => Roles = nameof(UserRole.Admin);
}
