namespace backend.Dtos.Households;

public sealed record HouseholdLeaveResult(
    HouseholdLeaveResultStatus Status,
    Guid? ActiveHouseholdId = null,
    string? ActiveHouseholdName = null,
    bool IsNewlyCreatedDefault = false)
{
    public static HouseholdLeaveResult Success(Guid householdId, string householdName, bool isNewDefault) =>
        new(HouseholdLeaveResultStatus.Success, householdId, householdName, isNewDefault);

    public static HouseholdLeaveResult HouseholdNotFound => new(HouseholdLeaveResultStatus.HouseholdNotFound);
    public static HouseholdLeaveResult UserNotFound => new(HouseholdLeaveResultStatus.UserNotFound);
    public static HouseholdLeaveResult OwnerCannotLeave => new(HouseholdLeaveResultStatus.OwnerCannotLeave);
    public static HouseholdLeaveResult NotMember => new(HouseholdLeaveResultStatus.NotMember);
}

public enum HouseholdLeaveResultStatus
{
    Success = 1,
    HouseholdNotFound = 2,
    UserNotFound = 3,
    NotMember = 4,
    OwnerCannotLeave = 5
}
