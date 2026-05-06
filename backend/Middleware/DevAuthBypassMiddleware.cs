using System.Security.Claims;

namespace backend.Middleware;

public class DevAuthBypassMiddleware(RequestDelegate next)
{
    public const string AuthenticationType = "DevBypass";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "DevUser")};
            var identity = new ClaimsIdentity(claims, AuthenticationType);
            context.User = new ClaimsPrincipal(identity);
        }

        await next(context);
    }
}
