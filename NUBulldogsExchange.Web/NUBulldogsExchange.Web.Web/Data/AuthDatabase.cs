using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Web.Web.Data;

public sealed partial class DatabaseService
{
    private static readonly PasswordHasher<object> PasswordHasher = new();

    public async Task<AuthResult> RegisterCustomerAsync(RegisterRequest request)
    {
        await EnsureReadyAsync();

        var firstName = (request.FirstName ?? "").Trim();
        var lastName = (request.LastName ?? "").Trim();
        var email = AuthValidation.NormalizeEmail(request.Email ?? "");
        var phone = (request.PhoneNumber ?? "").Trim();
        var password = request.Password ?? "";
        var confirm = request.ConfirmPassword ?? "";

        if (string.IsNullOrWhiteSpace(firstName))
            return Fail("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            return Fail("Last name is required.");
        if (!AuthValidation.IsValidEmail(email))
            return Fail("Please enter a valid email address.");
        if (!AuthValidation.IsValidPhone(phone))
            return Fail("Please enter a valid phone number.");
        if (string.IsNullOrWhiteSpace(password))
            return Fail("Password is required.");
        if (password.Length < AuthValidation.MinPasswordLength)
            return Fail($"Password must be at least {AuthValidation.MinPasswordLength} characters.");
        if (password != confirm)
            return Fail("Password and confirm password must match.");

        await using var connection = await OpenAsync();

        if (await EmailExistsAsync(connection, email))
            return Fail("An account with this email already exists.");

        var roleId = await GetRoleIdAsync(connection, "Customer");
        if (roleId is null)
            return Fail("Unable to create your account. Please try again.");

        var now = DateTime.UtcNow;
        var hash = PasswordHasher.HashPassword(new object(), password);

        try
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Users (
                    FirstName, LastName, Email, PasswordHash, PhoneNumber, RoleId,
                    ProfileImage, Status, EmailVerified, CreatedAt, UpdatedAt
                ) VALUES (
                    $firstName, $lastName, $email, $passwordHash, $phone, $roleId,
                    '', 'Active', 0, $createdAt, $updatedAt
                );
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$firstName", firstName);
            insert.Parameters.AddWithValue("$lastName", lastName);
            insert.Parameters.AddWithValue("$email", email);
            insert.Parameters.AddWithValue("$passwordHash", hash);
            insert.Parameters.AddWithValue("$phone", phone);
            insert.Parameters.AddWithValue("$roleId", roleId.Value);
            insert.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            insert.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            var userId = Convert.ToInt32(await insert.ExecuteScalarAsync());

            await using var profile = connection.CreateCommand();
            profile.CommandText = """
                INSERT INTO CustomerProfiles (UserId, CustomerType, CreatedAt, UpdatedAt)
                VALUES ($userId, 'Regular', $createdAt, $updatedAt);
                """;
            profile.Parameters.AddWithValue("$userId", userId);
            profile.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            profile.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            await profile.ExecuteNonQueryAsync();

            var (token, user) = await CreateSessionAsync(connection, userId, rememberMe: false);
            return new AuthResult
            {
                Success = true,
                User = user,
                SessionToken = token
            };
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Fail("An account with this email already exists.");
        }
        catch
        {
            return Fail("Unable to create your account. Please try again.");
        }
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        await EnsureReadyAsync();

        var email = AuthValidation.NormalizeEmail(request.Email ?? "");
        var password = request.Password ?? "";

        if (!AuthValidation.IsValidEmail(email) || string.IsNullOrWhiteSpace(password))
            return Fail("Invalid email or password.");

        await using var connection = await OpenAsync();
        var row = await FindUserByEmailAsync(connection, email);
        if (row is null)
            return Fail("Invalid email or password.");

        if (!IsActiveStatus(row.Status))
            return Fail("Your account is currently unavailable. Please contact support.");

        if (string.IsNullOrWhiteSpace(row.PasswordHash))
            return Fail("Invalid email or password.");

        var verify = PasswordHasher.VerifyHashedPassword(new object(), row.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
            return Fail("Invalid email or password.");

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await using var rehash = connection.CreateCommand();
            rehash.CommandText = "UPDATE Users SET PasswordHash = $hash, UpdatedAt = $updatedAt WHERE Id = $id;";
            rehash.Parameters.AddWithValue("$hash", PasswordHasher.HashPassword(new object(), password));
            rehash.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
            rehash.Parameters.AddWithValue("$id", row.Id);
            await rehash.ExecuteNonQueryAsync();
        }

        var now = DateTime.UtcNow.ToString("O");
        await using (var update = connection.CreateCommand())
        {
            update.CommandText = "UPDATE Users SET LastLoginAt = $lastLogin, UpdatedAt = $updatedAt WHERE Id = $id;";
            update.Parameters.AddWithValue("$lastLogin", now);
            update.Parameters.AddWithValue("$updatedAt", now);
            update.Parameters.AddWithValue("$id", row.Id);
            await update.ExecuteNonQueryAsync();
        }

        var (token, user) = await CreateSessionAsync(connection, row.Id, request.RememberMe);
        return new AuthResult
        {
            Success = true,
            User = user,
            SessionToken = token
        };
    }

    public async Task LogoutSessionAsync(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            return;

        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AuthSessions WHERE TokenHash = $hash;";
        command.Parameters.AddWithValue("$hash", HashToken(sessionToken));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<AuthResult> ValidateSessionAsync(string sessionToken)
    {
        await EnsureReadyAsync();
        if (string.IsNullOrWhiteSpace(sessionToken))
            return Fail("Your session has expired. Please sign in again.");

        await using var connection = await OpenAsync();
        var userId = await GetSessionUserIdAsync(connection, sessionToken);
        if (userId is null)
            return Fail("Your session has expired. Please sign in again.");

        var user = await LoadUserAsync(connection, userId.Value);
        if (user is null)
            return Fail("Your session has expired. Please sign in again.");

        if (!IsActiveStatus(user.Status) && !user.IsAdmin)
            return Fail("Your account is currently unavailable. Please contact support.");

        user.SessionToken = sessionToken;
        return new AuthResult { Success = true, User = user, SessionToken = sessionToken };
    }

    public async Task<AuthResult> UpdateCustomerProfileAsync(string sessionToken, UpdateProfileRequest request)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        var userId = await GetSessionUserIdAsync(connection, sessionToken);
        if (userId is null)
            return Fail("Please sign in to update your profile.");

        var firstName = (request.FirstName ?? "").Trim();
        var lastName = (request.LastName ?? "").Trim();
        var phone = (request.PhoneNumber ?? "").Trim();
        var profileImage = (request.ProfileImage ?? "").Trim();
        var studentId = (request.StudentId ?? "").Trim();
        var address = (request.Address ?? "").Trim();

        if (string.IsNullOrWhiteSpace(firstName))
            return Fail("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            return Fail("Last name is required.");
        if (!AuthValidation.IsValidPhone(phone))
            return Fail("Please enter a valid phone number.");

        var now = DateTime.UtcNow.ToString("O");
        await using (var update = connection.CreateCommand())
        {
            update.CommandText = """
                UPDATE Users
                SET FirstName = $firstName,
                    LastName = $lastName,
                    PhoneNumber = $phone,
                    ProfileImage = $profileImage,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            update.Parameters.AddWithValue("$firstName", firstName);
            update.Parameters.AddWithValue("$lastName", lastName);
            update.Parameters.AddWithValue("$phone", phone);
            update.Parameters.AddWithValue("$profileImage", profileImage);
            update.Parameters.AddWithValue("$updatedAt", now);
            update.Parameters.AddWithValue("$id", userId.Value);
            await update.ExecuteNonQueryAsync();
        }

        await using (var profile = connection.CreateCommand())
        {
            profile.CommandText = """
                INSERT INTO CustomerProfiles (UserId, StudentNumber, CustomerType, CreatedAt, UpdatedAt)
                VALUES ($userId, $studentId, 'Regular', $createdAt, $updatedAt)
                ON CONFLICT(UserId) DO UPDATE SET
                    StudentNumber = excluded.StudentNumber,
                    UpdatedAt = excluded.UpdatedAt;
                """;
            profile.Parameters.AddWithValue("$userId", userId.Value);
            profile.Parameters.AddWithValue("$studentId", studentId);
            profile.Parameters.AddWithValue("$createdAt", now);
            profile.Parameters.AddWithValue("$updatedAt", now);
            try
            {
                await profile.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = """
                    UPDATE CustomerProfiles
                    SET StudentNumber = $studentId, UpdatedAt = $updatedAt
                    WHERE UserId = $userId;
                    """;
                fallback.Parameters.AddWithValue("$studentId", studentId);
                fallback.Parameters.AddWithValue("$updatedAt", now);
                fallback.Parameters.AddWithValue("$userId", userId.Value);
                var updated = await fallback.ExecuteNonQueryAsync();
                if (updated == 0)
                {
                    await using var insert = connection.CreateCommand();
                    insert.CommandText = """
                        INSERT INTO CustomerProfiles (UserId, StudentNumber, CustomerType, CreatedAt, UpdatedAt)
                        VALUES ($userId, $studentId, 'Regular', $createdAt, $updatedAt);
                        """;
                    insert.Parameters.AddWithValue("$userId", userId.Value);
                    insert.Parameters.AddWithValue("$studentId", studentId);
                    insert.Parameters.AddWithValue("$createdAt", now);
                    insert.Parameters.AddWithValue("$updatedAt", now);
                    await insert.ExecuteNonQueryAsync();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            await using var addr = connection.CreateCommand();
            addr.CommandText = """
                UPDATE Addresses
                SET AddressLine1 = $line, RecipientName = $name, PhoneNumber = $phone, UpdatedAt = $updatedAt
                WHERE UserId = $userId AND IsDefault = 1;
                """;
            addr.Parameters.AddWithValue("$line", address);
            addr.Parameters.AddWithValue("$name", $"{firstName} {lastName}".Trim());
            addr.Parameters.AddWithValue("$phone", phone);
            addr.Parameters.AddWithValue("$updatedAt", now);
            addr.Parameters.AddWithValue("$userId", userId.Value);
            var changed = await addr.ExecuteNonQueryAsync();
            if (changed == 0)
            {
                await using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO Addresses (
                        UserId, Label, RecipientName, PhoneNumber, AddressLine1, IsDefault, CreatedAt, UpdatedAt
                    ) VALUES (
                        $userId, 'Primary', $name, $phone, $line, 1, $createdAt, $updatedAt
                    );
                    """;
                insert.Parameters.AddWithValue("$userId", userId.Value);
                insert.Parameters.AddWithValue("$name", $"{firstName} {lastName}".Trim());
                insert.Parameters.AddWithValue("$phone", phone);
                insert.Parameters.AddWithValue("$line", address);
                insert.Parameters.AddWithValue("$createdAt", now);
                insert.Parameters.AddWithValue("$updatedAt", now);
                await insert.ExecuteNonQueryAsync();
            }
        }

        var user = await LoadUserAsync(connection, userId.Value);
        if (user is not null)
            user.SessionToken = sessionToken;

        return new AuthResult { Success = true, User = user, SessionToken = sessionToken };
    }

    public async Task<AuthResult> ChangePasswordAsync(string sessionToken, ChangePasswordRequest request)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        var userId = await GetSessionUserIdAsync(connection, sessionToken);
        if (userId is null)
            return Fail("Please sign in to change your password.");

        var current = request.CurrentPassword ?? "";
        var next = request.NewPassword ?? "";
        var confirm = request.ConfirmNewPassword ?? "";

        if (string.IsNullOrWhiteSpace(current))
            return Fail("Current password is required.");
        if (string.IsNullOrWhiteSpace(next))
            return Fail("New password is required.");
        if (next.Length < AuthValidation.MinPasswordLength)
            return Fail($"New password must be at least {AuthValidation.MinPasswordLength} characters.");
        if (next != confirm)
            return Fail("New password and confirm password must match.");

        string? existingHash;
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT PasswordHash FROM Users WHERE Id = $id;";
            read.Parameters.AddWithValue("$id", userId.Value);
            existingHash = await read.ExecuteScalarAsync() as string;
        }

        if (string.IsNullOrWhiteSpace(existingHash) ||
            PasswordHasher.VerifyHashedPassword(new object(), existingHash, current) == PasswordVerificationResult.Failed)
        {
            return Fail("Current password is incorrect.");
        }

        await using (var update = connection.CreateCommand())
        {
            update.CommandText = "UPDATE Users SET PasswordHash = $hash, UpdatedAt = $updatedAt WHERE Id = $id;";
            update.Parameters.AddWithValue("$hash", PasswordHasher.HashPassword(new object(), next));
            update.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$id", userId.Value);
            await update.ExecuteNonQueryAsync();
        }

        var user = await LoadUserAsync(connection, userId.Value);
        if (user is not null)
            user.SessionToken = sessionToken;

        return new AuthResult { Success = true, User = user, SessionToken = sessionToken };
    }

    public async Task<bool> SetCustomerStatusAsync(string customerId, string status, int? actorUserId)
    {
        await EnsureReadyAsync();
        if (!int.TryParse(customerId, out var userId))
            return false;

        var normalized = status.Trim() switch
        {
            var s when s.Equals("Active", StringComparison.OrdinalIgnoreCase) => "Active",
            var s when s.Equals("Suspended", StringComparison.OrdinalIgnoreCase) => "Suspended",
            var s when s.Equals("Inactive", StringComparison.OrdinalIgnoreCase) => "Inactive",
            _ => null
        };
        if (normalized is null)
            return false;

        await using var connection = await OpenAsync();
        await using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE Users
            SET Status = $status, UpdatedAt = $updatedAt
            WHERE Id = $id AND RoleId = (SELECT Id FROM Roles WHERE Name = 'Customer' LIMIT 1);
            """;
        update.Parameters.AddWithValue("$status", normalized);
        update.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", userId);
        var changed = await update.ExecuteNonQueryAsync();
        if (changed == 0)
            return false;

        var action = normalized switch
        {
            "Active" => "Activated Customer",
            "Suspended" => "Suspended Customer",
            _ => "Deactivated Customer"
        };
        await RecordAuditCoreAsync(connection, null, actorUserId, action, "User", userId.ToString(),
            $"{action} #{userId}", null);

        if (!normalized.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            await using var revoke = connection.CreateCommand();
            revoke.CommandText = "DELETE FROM AuthSessions WHERE UserId = $userId;";
            revoke.Parameters.AddWithValue("$userId", userId);
            await revoke.ExecuteNonQueryAsync();
        }

        return true;
    }

    public async Task<decimal> GetCompletedOrderRevenueAsync()
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(Total), 0)
            FROM Orders
            WHERE Status = 'Completed';
            """;
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToDecimal(result);
    }

    private async Task<(string Token, MockUser User)> CreateSessionAsync(
        SqliteConnection connection, int userId, bool rememberMe)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(tokenBytes);
        var now = DateTime.UtcNow;
        var expires = rememberMe ? now.AddDays(30) : now.AddHours(12);

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO AuthSessions (UserId, TokenHash, CreatedAt, ExpiresAt, RememberMe)
            VALUES ($userId, $hash, $createdAt, $expiresAt, $rememberMe);
            """;
        insert.Parameters.AddWithValue("$userId", userId);
        insert.Parameters.AddWithValue("$hash", HashToken(token));
        insert.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        insert.Parameters.AddWithValue("$expiresAt", expires.ToString("O"));
        insert.Parameters.AddWithValue("$rememberMe", rememberMe ? 1 : 0);
        await insert.ExecuteNonQueryAsync();

        var user = await LoadUserAsync(connection, userId)
            ?? throw new InvalidOperationException("User was not found after authentication.");
        user.RememberMe = rememberMe;
        user.SessionToken = token;
        return (token, user);
    }

    private static async Task<int?> GetSessionUserIdAsync(SqliteConnection connection, string sessionToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId, ExpiresAt
            FROM AuthSessions
            WHERE TokenHash = $hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$hash", HashToken(sessionToken));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var userId = reader.GetInt32(0);
        var expiresText = reader.IsDBNull(1) ? null : reader.GetString(1);
        await reader.DisposeAsync();

        if (DateTime.TryParse(expiresText, out var expires) && expires < DateTime.UtcNow)
        {
            await using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM AuthSessions WHERE TokenHash = $hash;";
            delete.Parameters.AddWithValue("$hash", HashToken(sessionToken));
            await delete.ExecuteNonQueryAsync();
            return null;
        }

        return userId;
    }

    private static async Task<bool> EmailExistsAsync(SqliteConnection connection, string email)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE lower(Email) = $email;";
        command.Parameters.AddWithValue("$email", email);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<int?> GetRoleIdAsync(SqliteConnection connection, string roleName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Roles WHERE Name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", roleName);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task<UserRow?> FindUserByEmailAsync(SqliteConnection connection, string email)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.Id, u.PasswordHash, u.Status, r.Name
            FROM Users u
            LEFT JOIN Roles r ON r.Id = u.RoleId
            WHERE lower(u.Email) = $email
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", email);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new UserRow(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "Active" : reader.GetString(2),
            reader.IsDBNull(3) ? "Customer" : reader.GetString(3));
    }

    private static async Task<MockUser?> LoadUserAsync(SqliteConnection connection, int userId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.Id, u.FirstName, u.LastName, u.Email, u.PhoneNumber, u.ProfileImage,
                   u.Status, u.CreatedAt, u.LastLoginAt, r.Name,
                   p.StudentNumber
            FROM Users u
            LEFT JOIN Roles r ON r.Id = u.RoleId
            LEFT JOIN CustomerProfiles p ON p.UserId = u.Id
            WHERE u.Id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", userId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var id = reader.GetInt32(0);
        var first = reader.IsDBNull(1) ? "" : reader.GetString(1);
        var last = reader.IsDBNull(2) ? "" : reader.GetString(2);
        var email = reader.IsDBNull(3) ? "" : reader.GetString(3);
        var phone = reader.IsDBNull(4) ? "" : reader.GetString(4);
        var profileImage = reader.IsDBNull(5) ? "" : reader.GetString(5);
        var status = reader.IsDBNull(6) ? "Active" : reader.GetString(6);
        DateTime createdAt = default;
        if (!reader.IsDBNull(7) && DateTime.TryParse(reader.GetString(7), out var parsedCreated))
            createdAt = parsedCreated;
        DateTime? lastLogin = null;
        if (!reader.IsDBNull(8) && DateTime.TryParse(reader.GetString(8), out var parsedLogin))
            lastLogin = parsedLogin;
        var role = reader.IsDBNull(9) ? "customer" : reader.GetString(9).ToLowerInvariant();
        var studentId = reader.IsDBNull(10) ? "" : reader.GetString(10);
        await reader.DisposeAsync();

        string address = "";
        await using (var addr = connection.CreateCommand())
        {
            addr.CommandText = """
                SELECT AddressLine1 FROM Addresses
                WHERE UserId = $userId
                ORDER BY IsDefault DESC, Id DESC
                LIMIT 1;
                """;
            addr.Parameters.AddWithValue("$userId", userId);
            var line = await addr.ExecuteScalarAsync() as string;
            address = line ?? "";
        }

        return new MockUser
        {
            UserId = id,
            FirstName = first,
            LastName = last,
            Name = $"{first} {last}".Trim(),
            Email = email,
            Phone = phone,
            ProfileImage = profileImage,
            Status = status,
            CreatedAt = createdAt,
            LastLoginAt = lastLogin,
            Role = role,
            StudentId = studentId,
            Address = address
        };
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static bool IsActiveStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ||
        status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    private static AuthResult Fail(string error) => new() { Success = false, Error = error };

    private sealed record UserRow(int Id, string PasswordHash, string Status, string Role);
}
