namespace backend.Dtos.Inventory;

/// <summary>
/// Request for deducting inventory items based on a recipe's ingredients.
/// </summary>
public record DeductForRecipeRequest(
    Guid RecipeId,
    int Servings
);

/// <summary>
/// Result of inventory deduction for a recipe.
/// </summary>
public record DeductionResult(
    int TotalDeducted,
    int ItemsFullyDeducted,
    int ItemsPartiallyDeducted,
    int ItemsNotFound,
    IReadOnlyList<DeductionItemResult> Items
);

/// <summary>
/// Result for a single ingredient deduction.
/// </summary>
public record DeductionItemResult(
    string IngredientName,
    string? MatchedInventoryItem,
    decimal RequestedAmount,
    decimal DeductedAmount,
    string Unit,
    DeductionStatus Status
);

/// <summary>
/// Status of a single deduction operation.
/// </summary>
public enum DeductionStatus
{
    /// <summary>Full amount was deducted.</summary>
    FullyDeducted,
    /// <summary>Partial amount deducted (inventory had less than requested).</summary>
    PartiallyDeducted,
    /// <summary>No matching inventory item found.</summary>
    NotFound,
    /// <summary>Item was removed from inventory (reached 0).</summary>
    RemovedFromInventory
}
