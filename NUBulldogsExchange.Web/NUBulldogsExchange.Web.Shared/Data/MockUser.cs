namespace NUBulldogsExchange.Web.Shared.Data;

public class MockUser
{
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = "+63 912 345 6789";
    public string StudentId { get; set; } = "2021-12345";
    public string College { get; set; } = "College of Business & Accountancy";
    public string Address { get; set; } = "123 Sampaloc, Manila";
    public string Role { get; set; } = "customer";
    public bool RememberMe { get; set; }

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

    public static MockUser FromEmail(string email, bool rememberMe = false)
    {
        var trimmed = email.Trim();
        var isAdmin = trimmed.Equals("admin@nu.edu", StringComparison.OrdinalIgnoreCase);

        var name = ResolveName(trimmed, isAdmin);
        var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        return new MockUser
        {
            Email = trimmed,
            Name = name,
            FirstName = parts.Length > 0 ? parts[0] : name,
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            Role = isAdmin ? "admin" : "customer",
            RememberMe = rememberMe
        };
    }

    private static string ResolveName(string email, bool isAdmin)
    {
        if (isAdmin)
            return "Admin User";

        var local = email.Split('@')[0];
        var key = local.Replace(".", " ").Replace("_", " ").Replace("-", " ").Trim().ToLowerInvariant();

        if (key is "dsd" || key.Contains("maria"))
            return "Maria Santos";
        if (key.Contains("juan"))
            return "Juan Dela Cruz";
        if (key.Contains("anna") || key.StartsWith("ana"))
            return "Anna Reyes";

        var parts = local.Split(['.', '_', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "Customer";

        return string.Join(" ", parts.Select(TitleCase));
    }

    private static string TitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
