using backend.Interfaces;

namespace backend.Dtos.SmartRecipes;

/// <summary>
/// SSE event types for smart recipe streaming.
/// </summary>
public enum SmartRecipeSseEventType
{
    /// <summary>Generation started, includes total expected count.</summary>
    Start,
    /// <summary>Individual recipe generated successfully.</summary>
    Recipe,
    /// <summary>All recipes generated successfully.</summary>
    Complete,
    /// <summary>Error occurred during generation.</summary>
    Error
}

/// <summary>
/// SSE event payload for smart recipe streaming.
/// </summary>
public record SmartRecipeSseEvent(
    SmartRecipeSseEventType Type,
    SmartRecipeDto? Recipe = null,
    int? TotalExpected = null,
    int? CurrentIndex = null,
    string? ErrorMessage = null);
