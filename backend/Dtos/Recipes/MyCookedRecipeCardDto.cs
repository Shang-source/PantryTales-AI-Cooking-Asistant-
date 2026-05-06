namespace backend.Dtos.Recipes;

/// <summary>
/// A recipe card in the user's cooking history, with cook count and timestamps
/// </summary>
public sealed record MyCookedRecipeCardDto(
    Guid CookId,           // The ID of the most recent cook entry (for deletion)
    Guid Id,               // Recipe ID
    string Title,
    string? Description,
    string? CoverImageUrl,
    Guid? AuthorId,
    string AuthorName,
    int CookCount,         // Number of times user cooked this recipe
    DateTime LastCookedAt, // Most recent cook time
    DateTime FirstCookedAt // First cook time
);
