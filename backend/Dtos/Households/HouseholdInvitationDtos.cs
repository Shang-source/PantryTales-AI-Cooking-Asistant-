using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Households;

public sealed class InviteHouseholdMemberRequest : IValidatableObject
{
    [MaxLength(128)]
    public string? ClerkUserId { get; init; }

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; init; }

    [Range(1, 30, ErrorMessage = "ExpirationDays must be between 1 and 30.")]
    public int? ExpirationDays { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ClerkUserId) && string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult(
                "Either ClerkUserId or Email must be provided.",
                [nameof(ClerkUserId), nameof(Email)]);
        }
    }
}

public sealed record HouseholdInvitationResponseDto(
    Guid Id,
    Guid HouseholdId,
    string? Email,
    string Status,
    DateTime ExpiredAt,
    DateTime CreatedAt,
    string InvitationType = "email",
    string? Token = null);

public sealed class CreateLinkInvitationRequest
{
    [Range(1, 30, ErrorMessage = "ExpirationDays must be between 1 and 30.")]
    public int? ExpirationDays { get; init; }
}
