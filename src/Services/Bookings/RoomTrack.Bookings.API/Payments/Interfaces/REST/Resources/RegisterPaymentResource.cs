using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Validation;
using System.ComponentModel.DataAnnotations;
using RoomTrack.Payments.Domain.Model.ValueObjects;

namespace RoomTrack.Payments.Interfaces.REST.Resources;

/// <summary>A payment received by the hotel for a booking. There is no amount: it is the booking total.</summary>
public record RegisterPaymentResource : IValidatableObject
{
    /// <summary>Yape, Plin, BankTransfer, Cash or CardAtFrontDesk.</summary>
    /// <example>Yape</example>
    [Required]
    public string? Method { get; init; }

    /// <summary>Operation number of the Yape/Plin payment, the transfer or the POS voucher. Not needed for Cash.</summary>
    /// <example>00123456</example>
    [MaxLength(50)]
    public string? OperationNumber { get; init; }

    /// <summary>Optional note.</summary>
    [MaxLength(300)]
    public string? Note { get; init; }

    /// <summary>The validated method (a method, so model validation never evaluates it).</summary>
    public PaymentMethod ToMethod() => Enum.Parse<PaymentMethod>(Method!, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Method)
            && (int.TryParse(Method, out _) || !Enum.TryParse<PaymentMethod>(Method, true, out var parsed) || !Enum.IsDefined(parsed)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"Method must be one of: {string.Join(", ", Enum.GetNames<PaymentMethod>())}.", ["method"],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<PaymentMethod>() });
    }
}

/// <summary>A card payment from the app (US-15): only the token the card gateway issued for the card.</summary>
public record CardPaymentResource
{
    /// <summary>Token of the card, issued by the card gateway's SDK in the app. Sandbox: tok_visa, tok_mastercard, tok_declined_*.</summary>
    /// <example>tok_visa</example>
    [Required(ErrorMessage = "Send the token of the card issued by the card gateway.")]
    [MaxLength(100)]
    public string? CardToken { get; init; }
}
