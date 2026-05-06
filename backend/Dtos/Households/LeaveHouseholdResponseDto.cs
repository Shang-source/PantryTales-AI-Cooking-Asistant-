namespace backend.Dtos.Households;

public sealed record LeaveHouseholdResponseDto(
    Guid ActiveHouseholdId,
    string ActiveHouseholdName,
    bool IsNewlyCreatedDefault);
