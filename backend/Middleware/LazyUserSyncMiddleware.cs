using System.Linq;
using backend.Extensions;
using backend.Interfaces;

namespace backend.Middleware;

public class LazyUserSyncMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserService userService, ILogger<LazyUserSyncMiddleware> logger)
    {
        logger.LogDebug("Lazy user sync middleware invoked. Authenticated={Authenticated}, AuthType={AuthType}",
            context.User.Identity?.IsAuthenticated, context.User.Identity?.AuthenticationType);
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.User.TryBuildUserSyncPayload(out var payload, out var failureReason))
            {
                try
                {
                    logger.LogInformation("Lazy sync start for Clerk user {ClerkUserId} ({Email}).", payload.ClerkUserId,
                        payload.Email);
                    await userService.GetOrCreateAsync(payload, context.RequestAborted);
                    logger.LogInformation("Lazy sync completed for Clerk user {ClerkUserId}.", payload.ClerkUserId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to lazy sync Clerk user {ClerkUserId}. Continuing request.", payload.ClerkUserId);
                }
            }
            else
            {
                var claimTypes = string.Join(", ", context.User.Claims.Select(c => c.Type));
                logger.LogWarning("Authenticated request {TraceIdentifier} is missing required Clerk claims. Reason: {Reason}. Claims: {Claims}",
                    context.TraceIdentifier,
                    failureReason ?? "Unknown",
                    claimTypes);
            }
        }

        await next(context);
    }
}
