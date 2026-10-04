namespace RoomTrack.Notifications.Worker.Email;

/// <summary>
///     An e-mail ready to be delivered: a subject plus an HTML body and its plain-text alternative.
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body.</param>
/// <param name="TextBody">Plain-text alternative of <paramref name="HtmlBody"/>.</param>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);
