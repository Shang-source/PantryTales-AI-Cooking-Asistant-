namespace backend.Dtos.Checklist;

/// <summary>
/// DTO for a checklist item response.
/// </summary>
public record ChecklistItemDto(
    Guid Id,
    string Name,
    decimal Amount,
    string Unit,
    string? Category,
    bool IsChecked,
    Guid? FromRecipeId,
    string? FromRecipeName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// DTO for creating a checklist item.
/// </summary>
public record CreateChecklistItemDto(
    string Name,
    decimal Amount,
    string Unit,
    string? Category = null,
    Guid? FromRecipeId = null
);

/// <summary>
/// DTO for updating a checklist item.
/// </summary>
public record UpdateChecklistItemDto(
    string? Name = null,
    decimal? Amount = null,
    string? Unit = null,
    string? Category = null,
    bool? IsChecked = null
);

/// <summary>
/// DTO for batch creating checklist items (e.g., from recipe missing ingredients).
/// </summary>
public record BatchCreateChecklistItemsDto(
    List<CreateChecklistItemDto> Items,
    Guid? FromRecipeId = null
);

/// <summary>
/// Response for checklist list endpoint.
/// </summary>
public record ChecklistListDto(
    List<ChecklistItemDto> Items,
    int TotalCount
);

/// <summary>
/// Response for checklist stats endpoint.
/// </summary>
public record ChecklistStatsDto(
    int TotalCount,
    int PurchasedCount,
    int RemainingCount
);

/// <summary>
/// Response for the move-to-inventory operation.
/// </summary>
public record MoveToInventoryResultDto(
    int ItemsMoved
);
