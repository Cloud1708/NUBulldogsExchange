namespace NUBulldogsExchange.Web.Shared.Data;

/// <summary>Saved delivery address for a customer (public.user_addresses).</summary>
public class UserAddress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Label { get; set; } = "Home";
    public string RecipientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string Barangay { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string SummaryLine =>
        string.Join(", ", new[] { Barangay, City, Province, PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
