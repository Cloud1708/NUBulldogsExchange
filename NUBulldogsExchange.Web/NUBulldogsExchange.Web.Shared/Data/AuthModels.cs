namespace NUBulldogsExchange.Web.Shared.Data;

public sealed class RegisterRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}

public sealed class UpdateProfileRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string ProfileImage { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string College { get; set; } = "";
    public string Address { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmNewPassword { get; set; } = "";
}

public sealed class ForcedPasswordChangeRequest
{
    public string NewPassword { get; set; } = "";
    public string ConfirmNewPassword { get; set; } = "";
}

public sealed class CreateStaffAccountRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string TemporaryPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public string Status { get; set; } = "Active";
    public bool MustChangePassword { get; set; } = true;
}

public sealed class AuthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public MockUser? User { get; set; }
    public string? SessionToken { get; set; }
}

public static class AuthValidation
{
    public const int MinPasswordLength = 8;

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim();
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
            return false;

        var domain = email[(at + 1)..];
        return domain.Contains('.') && !email.Contains(' ');
    }

    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true;

        var trimmed = phone.Trim();
        if (trimmed.Length is < 7 or > 20)
            return false;

        return trimmed.All(ch => char.IsDigit(ch) || ch is '+' or '-' or ' ' or '(' or ')');
    }

    /// <summary>Customer registration phone: exactly 11 digits (e.g. 09XXXXXXXXX).</summary>
    public const int MobilePhoneLength = 11;

    public static bool IsValidMobilePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        var digits = DigitsOnly(phone);
        return digits.Length == MobilePhoneLength;
    }

    public static string DigitsOnly(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
