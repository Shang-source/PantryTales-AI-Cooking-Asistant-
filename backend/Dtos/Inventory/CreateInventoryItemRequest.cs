using System.ComponentModel.DataAnnotations;
using backend.Models;

namespace backend.Dtos.Inventory;

public class CreateInventoryItemRequestDto
{

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name length must be less than or equal to 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    [StringLength(20, ErrorMessage = "Unit length must be less than or equal to 20 characters.")]
    public string Unit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Storage method is required.")]
    [EnumDataType(typeof(InventoryStorageMethod), ErrorMessage = "Invalid storage method.")]
    public InventoryStorageMethod StorageMethod { get; set; }

    [Range(-365, 365, ErrorMessage = "Expiration days must be between -365 and 365.")]
    public int? ExpirationDays { get; set; }
}