using System.Text.Json;
using Microsoft.Data.Sqlite;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Data;

/// <summary>
/// Extended schema, column migrations, and minimal seed data for the shared SQLite store.
/// </summary>
public static class DatabaseInitializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task EnsureExtendedSchemaAsync(SqliteConnection connection)
    {
        // Create new tables first, then migrate columns onto existing tables,
        // then create indexes that depend on those columns (e.g. Products.Status).
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ExtendedTablesSql;
            await command.ExecuteNonQueryAsync();
        }

        await MigrateProductsAsync(connection);
        await MigrateCategoriesAsync(connection);
        await MigrateOrdersAsync(connection);
        await MigratePromotionsAsync(connection);
        await MigratePromotionUsagesAsync(connection);

        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText = ExtendedIndexesSql;
            await indexCommand.ExecuteNonQueryAsync();
        }

        await SeedRolesAsync(connection);
        await SeedAdminUserIfMissingAsync(connection);
        await SeedSystemSettingsAsync(connection);
        await SeedCategoriesIfEmptyAsync(connection);
        await RemoveSchoolEssentialsCategoryAsync(connection);
        await SeedAdminStaffIfEmptyAsync(connection);
    }

    private static async Task MigrateProductsAsync(SqliteConnection connection)
    {
        var columns = await GetColumnNamesAsync(connection, "Products");
        await AddColumnIfMissingAsync(connection, "Products", columns, "Status", "TEXT DEFAULT 'Active'");
        await AddColumnIfMissingAsync(connection, "Products", columns, "IsPublished", "INTEGER DEFAULT 1");
        await AddColumnIfMissingAsync(connection, "Products", columns, "UpdatedAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Products", columns, "PublishedAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Products", columns, "CreatedBy", "TEXT");
    }

    private static async Task MigrateCategoriesAsync(SqliteConnection connection)
    {
        var columns = await GetColumnNamesAsync(connection, "Categories");
        await AddColumnIfMissingAsync(connection, "Categories", columns, "DisplayOrder", "INTEGER DEFAULT 0");
        await AddColumnIfMissingAsync(connection, "Categories", columns, "IsActive", "INTEGER DEFAULT 1");
        await AddColumnIfMissingAsync(connection, "Categories", columns, "UpdatedAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Categories", columns, "CreatedAt", "TEXT");
    }

    private static async Task MigrateOrdersAsync(SqliteConnection connection)
    {
        var columns = await GetColumnNamesAsync(connection, "Orders");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "DiscountAmount", "REAL DEFAULT 0");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "ShippingFee", "REAL DEFAULT 0");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "TaxAmount", "REAL DEFAULT 0");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "Subtotal", "REAL DEFAULT 0");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "CustomerNotes", "TEXT");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "AdminNotes", "TEXT");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "UpdatedAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "CompletedAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "CancelledAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "UserId", "INTEGER");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "PromotionId", "TEXT");
        await AddColumnIfMissingAsync(connection, "Orders", columns, "PromotionCode", "TEXT");
    }

    private static async Task MigratePromotionsAsync(SqliteConnection connection)
    {
        var columns = await GetColumnNamesAsync(connection, "Promotions");
        await AddColumnIfMissingAsync(connection, "Promotions", columns, "MaximumDiscount", "REAL");
        await AddColumnIfMissingAsync(connection, "Promotions", columns, "UsagePerCustomer", "INTEGER DEFAULT 1");
        await AddColumnIfMissingAsync(connection, "Promotions", columns, "CreatedAt", "TEXT");
        await AddColumnIfMissingAsync(connection, "Promotions", columns, "UpdatedAt", "TEXT");
    }

    private static async Task MigratePromotionUsagesAsync(SqliteConnection connection)
    {
        var columns = await GetColumnNamesAsync(connection, "PromotionUsages");
        await AddColumnIfMissingAsync(connection, "PromotionUsages", columns, "UserId", "INTEGER");
        await AddColumnIfMissingAsync(connection, "PromotionUsages", columns, "Status", "TEXT DEFAULT 'Redeemed'");
    }

    private static async Task SeedRolesAsync(SqliteConnection connection)
    {
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Roles;";
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        if (count > 0) return;

        var now = DateTime.UtcNow.ToString("O");
        var roles = new (string Name, string Description)[]
        {
            ("Admin", "Full administrative access"),
            ("Staff", "Staff portal access"),
            ("Customer", "Storefront customer")
        };

        foreach (var (name, description) in roles)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Roles (Name, Description, CreatedAt)
                VALUES ($name, $description, $createdAt);
                """;
            insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$description", description);
            insert.Parameters.AddWithValue("$createdAt", now);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedAdminUserIfMissingAsync(SqliteConnection connection)
    {
        await using var roleCmd = connection.CreateCommand();
        roleCmd.CommandText = "SELECT Id FROM Roles WHERE Name = 'Admin' LIMIT 1;";
        var roleObj = await roleCmd.ExecuteScalarAsync();
        if (roleObj is null or DBNull) return;
        var roleId = Convert.ToInt32(roleObj);

        await using var exists = connection.CreateCommand();
        exists.CommandText = """
            SELECT COUNT(*) FROM Users
            WHERE RoleId = $roleId OR lower(Email) = 'admin@nu.edu';
            """;
        exists.Parameters.AddWithValue("$roleId", roleId);
        if (Convert.ToInt32(await exists.ExecuteScalarAsync()) > 0)
            return;

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), "Admin@123");
        var now = DateTime.UtcNow.ToString("O");

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO Users (
                FirstName, LastName, Email, PasswordHash, PhoneNumber, RoleId,
                Status, EmailVerified, CreatedAt, UpdatedAt
            ) VALUES (
                'Admin', 'User', 'admin@nu.edu', $hash, '', $roleId,
                'Active', 1, $createdAt, $updatedAt
            );
            """;
        insert.Parameters.AddWithValue("$hash", hash);
        insert.Parameters.AddWithValue("$roleId", roleId);
        insert.Parameters.AddWithValue("$createdAt", now);
        insert.Parameters.AddWithValue("$updatedAt", now);
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task SeedSystemSettingsAsync(SqliteConnection connection)
    {
        var defaults = new (string Key, string Value, string Description)[]
        {
            ("StoreName", "NU Bulldogs Exchange", "Store display name"),
            ("StoreEmail", "store@nu.edu", "Public store email"),
            ("StorePhone", "", "Store phone number"),
            ("PickupLocation", "NU Lipa Campus", "Campus pickup location"),
            ("PickupInstructions", "NU Lipa Campus pickup location. You'll receive a notification when your order is ready.", "Pickup instructions"),
            ("Currency", "PHP", "Store currency"),
            ("LowStockDefaultThreshold", "20", "Default low-stock threshold"),
            ("OrderPrefix", "NUBE", "Order number prefix"),
            ("StoreOpen", "true", "Whether the store is open for orders")
        };

        var now = DateTime.UtcNow.ToString("O");
        foreach (var (key, value, description) in defaults)
        {
            await using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM SystemSettings WHERE SettingKey = $key;";
            exists.Parameters.AddWithValue("$key", key);
            var count = Convert.ToInt32(await exists.ExecuteScalarAsync());
            if (count > 0) continue;

            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO SystemSettings (SettingKey, SettingValue, Description, UpdatedBy, UpdatedAt)
                VALUES ($key, $value, $description, $updatedBy, $updatedAt);
                """;
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$value", value);
            insert.Parameters.AddWithValue("$description", description);
            insert.Parameters.AddWithValue("$updatedBy", "system");
            insert.Parameters.AddWithValue("$updatedAt", now);
            await insert.ExecuteNonQueryAsync();

            // Mirror into legacy Settings table for GetSettingAsync compatibility.
            await using var legacy = connection.CreateCommand();
            legacy.CommandText = """
                INSERT INTO Settings (Key, Value) VALUES ($key, $value)
                ON CONFLICT(Key) DO NOTHING;
                """;
            legacy.Parameters.AddWithValue("$key", key);
            legacy.Parameters.AddWithValue("$value", value);
            await legacy.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedCategoriesIfEmptyAsync(SqliteConnection connection)
    {
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Categories;";
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        if (count > 0) return;

        var now = DateTime.UtcNow.ToString("O");
        var categories = new (string Id, string Name, string Slug, int Order)[]
        {
            ("cat-apparel", "Apparel", "apparel", 1),
            ("cat-accessories", "Accessories", "accessories", 2)
        };

        foreach (var (id, name, slug, order) in categories)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Categories (
                    Id, Name, Slug, ImageUrl, Description, Status, ProductCount,
                    DisplayOrder, IsActive, CreatedAt, UpdatedAt
                ) VALUES (
                    $id, $name, $slug, '', '', 'Active', 0,
                    $order, 1, $createdAt, $updatedAt
                );
                """;
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$slug", slug);
            insert.Parameters.AddWithValue("$order", order);
            insert.Parameters.AddWithValue("$createdAt", now);
            insert.Parameters.AddWithValue("$updatedAt", now);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task RemoveSchoolEssentialsCategoryAsync(SqliteConnection connection)
    {
        await using (var promo = connection.CreateCommand())
        {
            promo.CommandText = """
                DELETE FROM PromotionCategories
                WHERE CategoryId IN (
                    SELECT Id FROM Categories
                    WHERE Id = 'cat-essentials'
                       OR Slug = 'school-essentials'
                       OR Name = 'School Essentials'
                );
                """;
            await promo.ExecuteNonQueryAsync();
        }

        await using (var cat = connection.CreateCommand())
        {
            cat.CommandText = """
                DELETE FROM Categories
                WHERE Id = 'cat-essentials'
                   OR Slug = 'school-essentials'
                   OR Name = 'School Essentials';
                """;
            await cat.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedAdminStaffIfEmptyAsync(SqliteConnection connection)
    {
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Staff;";
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        if (count > 0) return;

        var permissions = JsonSerializer.Serialize(AdminStaffPermissions.FullAccess(), JsonOptions);
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO Staff (
                Id, FirstName, LastName, Email, Role, Status, LastLogin, IsPrimaryAdmin, PermissionsJson
            ) VALUES (
                $id, $firstName, $lastName, $email, $role, $status, $lastLogin, 1, $permissions
            );
            """;
        insert.Parameters.AddWithValue("$id", "S-ADMIN01");
        insert.Parameters.AddWithValue("$firstName", "Admin");
        insert.Parameters.AddWithValue("$lastName", "User");
        insert.Parameters.AddWithValue("$email", "admin@nu.edu");
        insert.Parameters.AddWithValue("$role", "Admin");
        insert.Parameters.AddWithValue("$status", "Active");
        insert.Parameters.AddWithValue("$lastLogin", DateTime.UtcNow.ToString("O"));
        insert.Parameters.AddWithValue("$permissions", permissions);
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(SqliteConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        string table,
        HashSet<string> columns,
        string column,
        string definition)
    {
        if (columns.Contains(column)) return;
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await command.ExecuteNonQueryAsync();
        columns.Add(column);
    }

    private const string ExtendedTablesSql = """
        CREATE TABLE IF NOT EXISTS Roles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Description TEXT,
            CreatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FirstName TEXT,
            MiddleName TEXT,
            LastName TEXT,
            Email TEXT NOT NULL UNIQUE,
            PasswordHash TEXT,
            PhoneNumber TEXT,
            RoleId INTEGER,
            ProfileImage TEXT,
            Status TEXT,
            EmailVerified INTEGER DEFAULT 0,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            LastLoginAt TEXT,
            FOREIGN KEY (RoleId) REFERENCES Roles(Id)
        );

        CREATE TABLE IF NOT EXISTS StaffProfiles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER,
            EmployeeNumber TEXT,
            Position TEXT,
            Department TEXT,
            DateHired TEXT,
            Status TEXT,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id)
        );

        CREATE TABLE IF NOT EXISTS CustomerProfiles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER,
            StudentNumber TEXT,
            CustomerType TEXT,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id)
        );

        CREATE TABLE IF NOT EXISTS Addresses (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER,
            Label TEXT,
            RecipientName TEXT,
            PhoneNumber TEXT,
            AddressLine1 TEXT,
            AddressLine2 TEXT,
            Barangay TEXT,
            City TEXT,
            Province TEXT,
            PostalCode TEXT,
            IsDefault INTEGER DEFAULT 0,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id)
        );

        CREATE TABLE IF NOT EXISTS ProductImages (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductId INTEGER NOT NULL,
            ImagePath TEXT NOT NULL,
            AltText TEXT,
            DisplayOrder INTEGER DEFAULT 0,
            IsPrimary INTEGER DEFAULT 0,
            CreatedAt TEXT,
            FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS ProductVariants (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductId INTEGER NOT NULL,
            Sku TEXT,
            VariantName TEXT,
            Size TEXT,
            Color TEXT,
            AdditionalPrice REAL DEFAULT 0,
            StockQuantity INTEGER DEFAULT 0,
            LowStockThreshold INTEGER DEFAULT 20,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Inventory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductId INTEGER NOT NULL,
            VariantId INTEGER,
            QuantityOnHand INTEGER DEFAULT 0,
            QuantityReserved INTEGER DEFAULT 0,
            ReorderLevel INTEGER DEFAULT 20,
            UpdatedAt TEXT,
            FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
            FOREIGN KEY (VariantId) REFERENCES ProductVariants(Id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS InventoryMovements (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductId INTEGER NOT NULL,
            VariantId INTEGER,
            MovementType TEXT NOT NULL,
            Quantity INTEGER NOT NULL,
            PreviousQuantity INTEGER,
            NewQuantity INTEGER,
            ReferenceType TEXT,
            ReferenceId TEXT,
            Notes TEXT,
            PerformedBy TEXT,
            CreatedAt TEXT,
            FOREIGN KEY (ProductId) REFERENCES Products(Id)
        );

        CREATE TABLE IF NOT EXISTS Carts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserEmail TEXT NOT NULL UNIQUE,
            CreatedAt TEXT,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS CartItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CartId INTEGER NOT NULL,
            ProductId INTEGER NOT NULL,
            VariantId INTEGER,
            Quantity INTEGER NOT NULL DEFAULT 1,
            UnitPrice REAL NOT NULL DEFAULT 0,
            SelectedColor TEXT,
            SelectedSize TEXT,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            UNIQUE(CartId, ProductId, SelectedColor, SelectedSize),
            FOREIGN KEY (CartId) REFERENCES Carts(Id) ON DELETE CASCADE,
            FOREIGN KEY (ProductId) REFERENCES Products(Id)
        );

        CREATE TABLE IF NOT EXISTS OrderStatusHistory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OrderId TEXT NOT NULL,
            OldStatus TEXT,
            NewStatus TEXT NOT NULL,
            Notes TEXT,
            ChangedBy TEXT,
            CreatedAt TEXT,
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Payments (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OrderId TEXT NOT NULL,
            PaymentReference TEXT,
            PaymentMethod TEXT,
            Amount REAL NOT NULL DEFAULT 0,
            Status TEXT,
            PaidAt TEXT,
            CreatedAt TEXT,
            UpdatedAt TEXT,
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS PromotionProducts (
            PromotionId TEXT NOT NULL,
            ProductId INTEGER NOT NULL,
            PRIMARY KEY (PromotionId, ProductId),
            FOREIGN KEY (PromotionId) REFERENCES Promotions(Id) ON DELETE CASCADE,
            FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS PromotionCategories (
            PromotionId TEXT NOT NULL,
            CategoryId TEXT NOT NULL,
            PRIMARY KEY (PromotionId, CategoryId),
            FOREIGN KEY (PromotionId) REFERENCES Promotions(Id) ON DELETE CASCADE,
            FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS PromotionUsages (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PromotionId TEXT NOT NULL,
            UserEmail TEXT,
            OrderId TEXT,
            DiscountAmount REAL DEFAULT 0,
            UsedAt TEXT,
            FOREIGN KEY (PromotionId) REFERENCES Promotions(Id)
        );

        CREATE TABLE IF NOT EXISTS Announcements (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Message TEXT,
            StartDate TEXT,
            EndDate TEXT,
            IsActive INTEGER DEFAULT 1,
            CreatedBy TEXT,
            CreatedAt TEXT,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS AuditLogs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER,
            Action TEXT,
            EntityType TEXT,
            EntityId TEXT,
            Description TEXT,
            IpAddress TEXT,
            CreatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS SystemSettings (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SettingKey TEXT NOT NULL UNIQUE,
            SettingValue TEXT,
            Description TEXT,
            UpdatedBy TEXT,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS AuthSessions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            TokenHash TEXT NOT NULL UNIQUE,
            CreatedAt TEXT,
            ExpiresAt TEXT,
            RememberMe INTEGER DEFAULT 0,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );
        """;

    private const string ExtendedIndexesSql = """
        CREATE INDEX IF NOT EXISTS IX_Products_Status ON Products(Status);
        CREATE INDEX IF NOT EXISTS IX_Products_IsPublished ON Products(IsPublished);
        CREATE INDEX IF NOT EXISTS IX_Products_Sku ON Products(Sku);
        CREATE INDEX IF NOT EXISTS IX_Products_Category ON Products(Category);
        CREATE INDEX IF NOT EXISTS IX_Orders_CustomerEmail ON Orders(CustomerEmail);
        CREATE INDEX IF NOT EXISTS IX_Orders_Status ON Orders(Status);
        CREATE INDEX IF NOT EXISTS IX_Orders_Date ON Orders(Date);
        CREATE INDEX IF NOT EXISTS IX_OrderItems_OrderId ON OrderItems(OrderId);
        CREATE INDEX IF NOT EXISTS IX_OrderItems_ProductId ON OrderItems(ProductId);
        CREATE INDEX IF NOT EXISTS IX_Notifications_Email ON Notifications(Email);
        CREATE INDEX IF NOT EXISTS IX_PromotionUsages_PromotionId ON PromotionUsages(PromotionId);
        CREATE INDEX IF NOT EXISTS IX_Inventory_ProductId ON Inventory(ProductId);
        CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users(Email);
        CREATE INDEX IF NOT EXISTS IX_Users_RoleId ON Users(RoleId);
        CREATE INDEX IF NOT EXISTS IX_Users_Status ON Users(Status);
        CREATE INDEX IF NOT EXISTS IX_Orders_UserId ON Orders(UserId);
        CREATE INDEX IF NOT EXISTS IX_AuthSessions_TokenHash ON AuthSessions(TokenHash);
        CREATE INDEX IF NOT EXISTS IX_AuthSessions_UserId ON AuthSessions(UserId);
        CREATE INDEX IF NOT EXISTS IX_CustomerProfiles_UserId ON CustomerProfiles(UserId);
        """;
}
