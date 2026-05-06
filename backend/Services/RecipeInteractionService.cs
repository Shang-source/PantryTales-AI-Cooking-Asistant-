using backend.Dtos.Interactions;
using backend.Interfaces;
using backend.Models;

namespace backend.Services;

public class RecipeInteractionService(
    IRecipeInteractionRepository interactionRepository,
    IUserRepository userRepository,
    IRecipeRepository recipeRepository,
    ILogger<RecipeInteractionService> logger) : IRecipeInteractionService
{
    public async Task<bool> LogInteractionAsync(
        string clerkUserId,
        Guid recipeId,
        RecipeInteractionEventType eventType,
        string? source = null,
        string? sessionId = null,
        int? dwellSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Log interaction rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return false;
        }

        // Optionally validate recipe exists (skip for impressions to reduce DB load)
        if (eventType != RecipeInteractionEventType.Impression)
        {
            var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
            if (recipe is null)
            {
                logger.LogWarning("Log interaction rejected: Recipe {RecipeId} not found.", recipeId);
                return false;
            }
        }

        var interaction = new RecipeInteraction
        {
            UserId = user.Id,
            RecipeId = recipeId,
            EventType = eventType,
            Source = source,
            SessionId = sessionId,
            DwellSeconds = dwellSeconds
        };

        await interactionRepository.AddAsync(interaction, cancellationToken);

        logger.LogDebug("Logged {EventType} for user {UserId} on recipe {RecipeId}.",
            eventType, user.Id, recipeId);

        return true;
    }

    public async Task<int> LogImpressionsAsync(
        string clerkUserId,
        IEnumerable<Guid> recipeIds,
        string? source = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Log impressions rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return 0;
        }

        var recipeIdList = recipeIds.ToList();
        if (recipeIdList.Count == 0)
            return 0;

        // Intentionally skip recipe existence validation for batch impressions.
        // This reduces DB load when logging many impressions at once (e.g., feed loads).
        // Invalid recipe IDs will have FK violations at the DB level, which is acceptable
        // since impressions are best-effort tracking signals.

        var interactions = recipeIdList.Select(recipeId => new RecipeInteraction
        {
            UserId = user.Id,
            RecipeId = recipeId,
            EventType = RecipeInteractionEventType.Impression,
            Source = source,
            SessionId = sessionId
        }).ToList();

        await interactionRepository.AddRangeAsync(interactions, cancellationToken);

        logger.LogDebug("Logged {Count} impressions for user {UserId}.", interactions.Count, user.Id);

        return interactions.Count;
    }

    public async Task<RecipeInteractionStatsDto?> GetRecipeStatsAsync(
        Guid recipeId,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null)
            return null;

        var since = DateTime.UtcNow.AddDays(-days);
        var counts = await interactionRepository.GetEventCountsForRecipeAsync(recipeId, since, cancellationToken);

        int impressions = counts.GetValueOrDefault(RecipeInteractionEventType.Impression);
        int clicks = counts.GetValueOrDefault(RecipeInteractionEventType.Click);
        int opens = counts.GetValueOrDefault(RecipeInteractionEventType.Open);
        int cooks = counts.GetValueOrDefault(RecipeInteractionEventType.Cook);
        int shares = counts.GetValueOrDefault(RecipeInteractionEventType.Share);

        // Net saves/likes within the time window (could be negative if more unsaves than saves).
        // We clamp to 0 for display purposes. For absolute current counts, use recipe.SavedCount/LikesCount.
        var saves = Math.Max(0, counts.GetValueOrDefault(RecipeInteractionEventType.Save)
                                - counts.GetValueOrDefault(RecipeInteractionEventType.Unsave));
        var likes = Math.Max(0, counts.GetValueOrDefault(RecipeInteractionEventType.Like)
                                - counts.GetValueOrDefault(RecipeInteractionEventType.Unlike));

        double ctr = impressions > 0 ? (double)clicks / impressions : 0;

        return new RecipeInteractionStatsDto(
            RecipeId: recipeId,
            Impressions: impressions,
            Clicks: clicks,
            Opens: opens,
            Saves: saves,
            Likes: likes,
            Cooks: cooks,
            Shares: shares,
            ClickThroughRate: Math.Round(ctr, 4),
            DaysIncluded: days);
    }
}