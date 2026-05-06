using backend.Dtos.Checklist;

namespace backend.Interfaces;

public enum ChecklistError
{
    Success,
    NoActiveHousehold,
    ItemNotFound,
    HouseholdMismatch,
    InvalidRequest,
    NoCheckedItems
}

public record ChecklistResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public ChecklistError Error { get; init; }

    public static ChecklistResult<T> Ok(T data) => new() { IsSuccess = true, Data = data, Error = ChecklistError.Success };
    public static ChecklistResult<T> Fail(ChecklistError error) => new() { IsSuccess = false, Error = error };
}

public record ChecklistActionResult
{
    public bool IsSuccess { get; init; }
    public ChecklistError Error { get; init; }

    public static ChecklistActionResult Ok() => new() { IsSuccess = true, Error = ChecklistError.Success };
    public static ChecklistActionResult Fail(ChecklistError error) => new() { IsSuccess = false, Error = error };
}

public interface IChecklistService
{
    Task<ChecklistResult<ChecklistListDto>> GetItemsAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<ChecklistItemDto>> AddItemAsync(
        string clerkUserId,
        CreateChecklistItemDto dto,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<List<ChecklistItemDto>>> AddBatchAsync(
        string clerkUserId,
        BatchCreateChecklistItemsDto dto,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<ChecklistItemDto>> UpdateItemAsync(
        Guid id,
        string clerkUserId,
        UpdateChecklistItemDto dto,
        CancellationToken cancellationToken = default);

    Task<ChecklistActionResult> DeleteItemAsync(
        Guid id,
        string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<int>> ClearCheckedAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<int>> ClearAllAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<ChecklistStatsDto>> GetStatsAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<ChecklistResult<MoveToInventoryResultDto>> MoveCheckedToInventoryAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default);
}
