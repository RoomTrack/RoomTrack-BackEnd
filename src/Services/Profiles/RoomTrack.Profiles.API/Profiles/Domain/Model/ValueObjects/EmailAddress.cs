using RoomTrack.Profiles.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Exceptions;
using System.Text.RegularExpressions;

namespace RoomTrack.Profiles.Domain.Model.ValueObjects;

public partial record EmailAddress
{
    public string Address { get; }

    public EmailAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new DomainValidationException(ProfileErrorCodes.EmailRequired, "Email address cannot be empty.");

        var trimmed = address.Trim().ToLowerInvariant();
        if (!EmailRegex().IsMatch(trimmed))
            throw new DomainValidationException(ProfileErrorCodes.EmailInvalid, "Invalid email address format.");

        Address = trimmed;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}