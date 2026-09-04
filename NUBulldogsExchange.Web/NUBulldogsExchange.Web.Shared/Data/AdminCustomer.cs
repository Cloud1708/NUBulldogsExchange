namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminCustomer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public DateTime DateJoined { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string Status { get; set; } = "Active";
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }

    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    public string StatusKey => Status.ToLowerInvariant() switch
    {
        "active" => "active",
        "suspended" => "suspended",
        _ => "inactive"
    };

    public string Initial =>
        string.IsNullOrWhiteSpace(Name) ? "?" : char.ToUpperInvariant(Name.Trim()[0]).ToString();

    public string DateJoinedLabel => DateJoined.ToString("MMM d, yyyy");

    public string LastLoginLabel => LastLoginAt is null
        ? "—"
        : LastLoginAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

    public string TotalSpentLabel => $"₱{TotalSpent:N0}";
}
