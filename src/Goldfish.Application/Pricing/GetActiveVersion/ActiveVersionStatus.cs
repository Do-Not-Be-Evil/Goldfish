namespace Goldfish.Application.Pricing.GetActiveVersion;

public sealed record ActiveVersionStatus(int VersionNumber, string ChangeTicketRef, DateTime PublishedOnUtc);
