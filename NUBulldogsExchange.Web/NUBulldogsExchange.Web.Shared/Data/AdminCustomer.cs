namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminCustomer
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public DateTime DateJoined { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public string Status { get; set; } = "Active";
    public int TotalOrders { get; set; }
    /// <summary>Non-cancelled orders used for New / Returning / Frequent Buyer.</summary>
    public int QualifyingOrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public string ProfileImage { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string College { get; set; } = string.Empty;
    /// <summary>Profile / default address text from the user record (not historical order shipping).</summary>
    public string DefaultAddress { get; set; } = string.Empty;

    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    public string StatusKey => Status.ToLowerInvariant() switch
    {
        "active" => "active",
        "suspended" => "suspended",
        _ => "inactive"
    };

    public string CustomerType => QualifyingOrderCount switch
    {
        >= 5 => "Frequent Buyer",
        >= 2 => "Returning",
        _ => "New"
    };

    public string CustomerTypeKey => CustomerType.ToLowerInvariant() switch
    {
        "frequent buyer" => "frequent",
        "returning" => "returning",
        _ => "new"
    };

    public string Initial =>
        string.IsNullOrWhiteSpace(Name) ? "?" : char.ToUpperInvariant(Name.Trim()[0]).ToString();

    public string ShortId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Id)) return "—";
            return Id.Length <= 8 ? Id : $"...{Id[^8..]}";
        }
    }

    public bool HasProfileImage =>
        !string.IsNullOrWhiteSpace(ProfileImage)
        && (ProfileImage.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || ProfileImage.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || ProfileImage.StartsWith("/"));

    public string ContactDisplay =>
        string.IsNullOrWhiteSpace(Contact) ? "—" : Contact.Trim();

    public string DateJoinedLabel => DateJoined.ToString("MMM d, yyyy");

    public string LastLoginLabel => LastLoginAt is null
        ? "—"
        : LastLoginAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

    public string LastOrderLabel => LastOrderAt is null
        ? "—"
        : LastOrderAt.Value.ToLocalTime().ToString("MMM d, yyyy");

    public string TotalSpentLabel => $"₱{TotalSpent:N0}";

    public string StudentIdDisplay =>
        string.IsNullOrWhiteSpace(StudentId) ? "Not provided" : StudentId.Trim();

    public string CollegeDisplay =>
        string.IsNullOrWhiteSpace(College) ? "Not provided" : College.Trim();

    public string DefaultAddressDisplay =>
        string.IsNullOrWhiteSpace(DefaultAddress) ? "Not provided" : DefaultAddress.Trim();
}
