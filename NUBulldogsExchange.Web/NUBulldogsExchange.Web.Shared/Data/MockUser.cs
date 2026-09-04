namespace NUBulldogsExchange.Web.Shared.Data;

public class MockUser
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string College { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Role { get; set; } = "customer";
    public string Status { get; set; } = "Active";
    public string ProfileImage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public bool RememberMe { get; set; }

    public string MemberSinceLabel =>
        CreatedAt == default ? "—" : CreatedAt.ToLocalTime().ToString("MMM d, yyyy");

    public string Initial
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(Name) ? Email : Name;
            var letter = source.Trim().FirstOrDefault(char.IsLetter);
            return letter == default ? "?" : char.ToUpperInvariant(letter).ToString();
        }
    }

    public bool IsCustomer =>
        Role.Equals("customer", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("user", StringComparison.OrdinalIgnoreCase);

    public bool IsAdmin => Role.Equals("admin", StringComparison.OrdinalIgnoreCase);
}
