namespace backend.Dtos.Inventory;

public sealed record InventoryResult<T>(
    InventoryError Result,
    T? Data
)
{
    public bool IsSuccess => Result == InventoryError.None;

    public static InventoryResult<T> Ok(T data)
    => new(InventoryError.None, data);

    public static InventoryResult<T> Fail(InventoryError error)
    {
        if (error == InventoryError.None)
            throw new ArgumentException("Fail result cannot have InventoryError.None", nameof(error));

        return new InventoryResult<T>(error, default);
    }
}

public sealed record InventoryActionResult(
    InventoryError Result
)
{
    public static InventoryActionResult Ok()
        => new(InventoryError.None);

    public static InventoryActionResult Fail(InventoryError error)
    {
        if (error == InventoryError.None)
            throw new ArgumentException(
                "Fail result cannot have InventoryError.None",
                nameof(error));

        return new InventoryActionResult(error);
    }
}

public enum InventoryError
{
    None = 0,

    // -------- Authorization / Context --------
    NoActiveHousehold,
    HouseholdMismatch,

    // -------- Inventory Item --------
    ItemNotFound,

    // -------- Ingredient --------
    IngredientNotFound
}
