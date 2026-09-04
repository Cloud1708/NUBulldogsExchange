using System.Text.Json;
using Microsoft.Data.Sqlite;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Web.Web.Data;

public sealed partial class DatabaseService : IAppDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _openLock = new(1, 1);
    private bool _initialized;

    public DatabaseService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "NUBulldogsExchange.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        }.ToString();
    }

    public string DatabasePath
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder(_connectionString);
            return builder.DataSource;
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var connection = await OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = SchemaSql;
            await command.ExecuteNonQueryAsync();
            await DatabaseInitializer.EnsureExtendedSchemaAsync(connection);
            await using (var journal = connection.CreateCommand())
            {
                journal.CommandText = "PRAGMA journal_mode = DELETE;";
                await journal.ExecuteScalarAsync();
            }
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsureReadyAsync()
    {
        if (!_initialized)
            await InitializeAsync();
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        await _openLock.WaitAsync();
        try
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync();
            pragma.CommandText = "PRAGMA busy_timeout = 5000;";
            await pragma.ExecuteNonQueryAsync();
            return connection;
        }
        finally
        {
            _openLock.Release();
        }
    }

    #region Products

    public async Task<List<Product>> GetProductsAsync()
    {
        await EnsureReadyAsync();
        var products = new List<Product>();
        await using var connection = await OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Products ORDER BY Id;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                products.Add(ReadProduct(reader));
        }

        foreach (var product in products)
            product.ProductReviews = await GetProductReviewsAsync(connection, product.Id);

        return products;
    }

    public async Task<List<Product>> GetPublishedProductsAsync()
    {
        await EnsureReadyAsync();
        var products = new List<Product>();
        await using var connection = await OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT * FROM Products
                WHERE Status = 'Active'
                  AND (IsPublished = 1 OR IsPublished IS NULL)
                ORDER BY Id;
                """;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                products.Add(ReadProduct(reader));
        }

        foreach (var product in products)
            product.ProductReviews = await GetProductReviewsAsync(connection, product.Id);

        return products;
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        Product? product;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Products WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;
            product = ReadProduct(reader);
        }

        product.ProductReviews = await GetProductReviewsAsync(connection, id);
        return product;
    }

    public async Task<Product> UpsertProductAsync(Product product)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();

        var now = DateTime.UtcNow;
        product.UpdatedAt = now;
        if (product.IsPublished && product.PublishedAt is null)
            product.PublishedAt = now;

        if (product.Id <= 0)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Products (
                    Name, Category, ImageUrl, ImagesJson, Price, OriginalPrice, Rating, Reviews, Sold, Stock,
                    Badge, ColorsJson, SizesJson, Material, Sku, InStock, IsFeatured, IsFreshDrop, IsBestSeller,
                    IsNewArrival, IsFavorite, Description, FullDescription, FeaturesJson, RatingBreakdownJson, Section,
                    Status, IsPublished, UpdatedAt, PublishedAt, CreatedBy, CreatedAt
                ) VALUES (
                    $name, $category, $imageUrl, $images, $price, $originalPrice, $rating, $reviews, $sold, $stock,
                    $badge, $colors, $sizes, $material, $sku, $inStock, $isFeatured, $isFreshDrop, $isBestSeller,
                    $isNewArrival, $isFavorite, $description, $fullDescription, $features, $ratingBreakdown, $section,
                    $status, $isPublished, $updatedAt, $publishedAt, $createdBy, $createdAt
                );
                SELECT last_insert_rowid();
                """;
            BindProduct(insert, product);
            insert.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            var idObj = await insert.ExecuteScalarAsync();
            product.Id = Convert.ToInt32(idObj);
        }
        else
        {
            await using var update = connection.CreateCommand();
            update.CommandText = """
                INSERT INTO Products (
                    Id, Name, Category, ImageUrl, ImagesJson, Price, OriginalPrice, Rating, Reviews, Sold, Stock,
                    Badge, ColorsJson, SizesJson, Material, Sku, InStock, IsFeatured, IsFreshDrop, IsBestSeller,
                    IsNewArrival, IsFavorite, Description, FullDescription, FeaturesJson, RatingBreakdownJson, Section,
                    Status, IsPublished, UpdatedAt, PublishedAt, CreatedBy, CreatedAt
                ) VALUES (
                    $id, $name, $category, $imageUrl, $images, $price, $originalPrice, $rating, $reviews, $sold, $stock,
                    $badge, $colors, $sizes, $material, $sku, $inStock, $isFeatured, $isFreshDrop, $isBestSeller,
                    $isNewArrival, $isFavorite, $description, $fullDescription, $features, $ratingBreakdown, $section,
                    $status, $isPublished, $updatedAt, $publishedAt, $createdBy,
                    COALESCE((SELECT CreatedAt FROM Products WHERE Id = $id), $createdAt)
                )
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    Category = excluded.Category,
                    ImageUrl = excluded.ImageUrl,
                    ImagesJson = excluded.ImagesJson,
                    Price = excluded.Price,
                    OriginalPrice = excluded.OriginalPrice,
                    Rating = excluded.Rating,
                    Reviews = excluded.Reviews,
                    Sold = excluded.Sold,
                    Stock = excluded.Stock,
                    Badge = excluded.Badge,
                    ColorsJson = excluded.ColorsJson,
                    SizesJson = excluded.SizesJson,
                    Material = excluded.Material,
                    Sku = excluded.Sku,
                    InStock = excluded.InStock,
                    IsFeatured = excluded.IsFeatured,
                    IsFreshDrop = excluded.IsFreshDrop,
                    IsBestSeller = excluded.IsBestSeller,
                    IsNewArrival = excluded.IsNewArrival,
                    IsFavorite = excluded.IsFavorite,
                    Description = excluded.Description,
                    FullDescription = excluded.FullDescription,
                    FeaturesJson = excluded.FeaturesJson,
                    RatingBreakdownJson = excluded.RatingBreakdownJson,
                    Section = excluded.Section,
                    Status = excluded.Status,
                    IsPublished = excluded.IsPublished,
                    UpdatedAt = excluded.UpdatedAt,
                    PublishedAt = excluded.PublishedAt,
                    CreatedBy = excluded.CreatedBy;
                """;
            update.Parameters.AddWithValue("$id", product.Id);
            BindProduct(update, product);
            update.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            await update.ExecuteNonQueryAsync();
        }

        await SyncProductImagesAndInventoryAsync(connection, product);

        if (product.ProductReviews.Count > 0)
            await SaveProductReviewsAsync(product.Id, product.ProductReviews);

        return product;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<ProductReview>> GetProductReviewsAsync(int productId)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        return await GetProductReviewsAsync(connection, productId);
    }

    private static async Task<List<ProductReview>> GetProductReviewsAsync(SqliteConnection connection, int productId)
    {
        var list = new List<ProductReview>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Author, Initials, Rating, Date, Comment
            FROM ProductReviews WHERE ProductId = $productId ORDER BY Date DESC;
            """;
        command.Parameters.AddWithValue("$productId", productId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ProductReview
            {
                Author = reader.GetString(0),
                Initials = reader.GetString(1),
                Rating = reader.GetInt32(2),
                Date = DateTime.Parse(reader.GetString(3)),
                Comment = reader.GetString(4)
            });
        }

        return list;
    }

    public async Task SaveProductReviewsAsync(int productId, IEnumerable<ProductReview> reviews)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM ProductReviews WHERE ProductId = $productId;";
            delete.Parameters.AddWithValue("$productId", productId);
            await delete.ExecuteNonQueryAsync();
        }

        foreach (var review in reviews)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO ProductReviews (ProductId, Author, Initials, Rating, Date, Comment)
                VALUES ($productId, $author, $initials, $rating, $date, $comment);
                """;
            insert.Parameters.AddWithValue("$productId", productId);
            insert.Parameters.AddWithValue("$author", review.Author);
            insert.Parameters.AddWithValue("$initials", review.Initials);
            insert.Parameters.AddWithValue("$rating", review.Rating);
            insert.Parameters.AddWithValue("$date", review.Date.ToString("O"));
            insert.Parameters.AddWithValue("$comment", review.Comment);
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    private static void BindProduct(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$category", product.Category);
        command.Parameters.AddWithValue("$imageUrl", product.ImageUrl ?? "");
        command.Parameters.AddWithValue("$images", JsonSerializer.Serialize(product.Images ?? [], JsonOptions));
        command.Parameters.AddWithValue("$price", product.Price);
        command.Parameters.AddWithValue("$originalPrice", (object?)product.OriginalPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("$rating", product.Rating);
        command.Parameters.AddWithValue("$reviews", product.Reviews);
        command.Parameters.AddWithValue("$sold", product.Sold);
        command.Parameters.AddWithValue("$stock", product.Stock);
        command.Parameters.AddWithValue("$badge", (object?)product.Badge ?? DBNull.Value);
        command.Parameters.AddWithValue("$colors", JsonSerializer.Serialize(product.Colors ?? [], JsonOptions));
        command.Parameters.AddWithValue("$sizes", JsonSerializer.Serialize(product.Sizes ?? [], JsonOptions));
        command.Parameters.AddWithValue("$material", product.Material ?? "");
        command.Parameters.AddWithValue("$sku", product.Sku ?? "");
        command.Parameters.AddWithValue("$inStock", product.InStock ? 1 : 0);
        command.Parameters.AddWithValue("$isFeatured", product.IsFeatured ? 1 : 0);
        command.Parameters.AddWithValue("$isFreshDrop", product.IsFreshDrop ? 1 : 0);
        command.Parameters.AddWithValue("$isBestSeller", product.IsBestSeller ? 1 : 0);
        command.Parameters.AddWithValue("$isNewArrival", product.IsNewArrival ? 1 : 0);
        command.Parameters.AddWithValue("$isFavorite", product.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$description", product.Description ?? "");
        command.Parameters.AddWithValue("$fullDescription", product.FullDescription ?? "");
        command.Parameters.AddWithValue("$features", JsonSerializer.Serialize(product.Features ?? [], JsonOptions));
        command.Parameters.AddWithValue("$ratingBreakdown", JsonSerializer.Serialize(product.RatingBreakdown ?? [], JsonOptions));
        command.Parameters.AddWithValue("$section", product.Section ?? "apparel");
        command.Parameters.AddWithValue("$status", string.IsNullOrWhiteSpace(product.Status) ? "Active" : product.Status);
        command.Parameters.AddWithValue("$isPublished", product.IsPublished ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", (object?)product.UpdatedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$publishedAt", (object?)product.PublishedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdBy", (object?)product.CreatedBy ?? DBNull.Value);
    }

    private static Product ReadProduct(SqliteDataReader reader)
    {
        var product = new Product
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
            Images = DeserializeList(reader.GetString(reader.GetOrdinal("ImagesJson"))),
            Price = reader.GetDecimal(reader.GetOrdinal("Price")),
            OriginalPrice = reader.IsDBNull(reader.GetOrdinal("OriginalPrice"))
                ? null
                : reader.GetDecimal(reader.GetOrdinal("OriginalPrice")),
            Rating = reader.GetDouble(reader.GetOrdinal("Rating")),
            Reviews = reader.GetInt32(reader.GetOrdinal("Reviews")),
            Sold = reader.GetInt32(reader.GetOrdinal("Sold")),
            Stock = reader.GetInt32(reader.GetOrdinal("Stock")),
            Badge = reader.IsDBNull(reader.GetOrdinal("Badge")) ? null : reader.GetString(reader.GetOrdinal("Badge")),
            Colors = DeserializeList(reader.GetString(reader.GetOrdinal("ColorsJson"))),
            Sizes = DeserializeList(reader.GetString(reader.GetOrdinal("SizesJson"))),
            Material = reader.GetString(reader.GetOrdinal("Material")),
            Sku = reader.GetString(reader.GetOrdinal("Sku")),
            InStock = reader.GetInt32(reader.GetOrdinal("InStock")) == 1,
            IsFeatured = reader.GetInt32(reader.GetOrdinal("IsFeatured")) == 1,
            IsFreshDrop = reader.GetInt32(reader.GetOrdinal("IsFreshDrop")) == 1,
            IsBestSeller = reader.GetInt32(reader.GetOrdinal("IsBestSeller")) == 1,
            IsNewArrival = reader.GetInt32(reader.GetOrdinal("IsNewArrival")) == 1,
            IsFavorite = reader.GetInt32(reader.GetOrdinal("IsFavorite")) == 1,
            Description = reader.GetString(reader.GetOrdinal("Description")),
            FullDescription = reader.GetString(reader.GetOrdinal("FullDescription")),
            Features = DeserializeList(reader.GetString(reader.GetOrdinal("FeaturesJson"))),
            RatingBreakdown = DeserializeIntArray(reader.GetString(reader.GetOrdinal("RatingBreakdownJson"))),
            Section = reader.GetString(reader.GetOrdinal("Section"))
        };

        if (TryGetOrdinal(reader, "Status", out var statusOrd) && !reader.IsDBNull(statusOrd))
            product.Status = reader.GetString(statusOrd);
        else
            product.Status = "Active";

        if (TryGetOrdinal(reader, "IsPublished", out var pubOrd) && !reader.IsDBNull(pubOrd))
            product.IsPublished = reader.GetInt32(pubOrd) == 1;
        else
            product.IsPublished = product.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

        if (TryGetOrdinal(reader, "PublishedAt", out var publishedAtOrd) && !reader.IsDBNull(publishedAtOrd))
            product.PublishedAt = DateTime.Parse(reader.GetString(publishedAtOrd));

        if (TryGetOrdinal(reader, "UpdatedAt", out var updatedAtOrd) && !reader.IsDBNull(updatedAtOrd))
            product.UpdatedAt = DateTime.Parse(reader.GetString(updatedAtOrd));

        if (TryGetOrdinal(reader, "CreatedBy", out var createdByOrd) && !reader.IsDBNull(createdByOrd))
            product.CreatedBy = reader.GetString(createdByOrd);

        return product;
    }

    private static bool TryGetOrdinal(SqliteDataReader reader, string name, out int ordinal)
    {
        try
        {
            ordinal = reader.GetOrdinal(name);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }

    #endregion

    #region Categories

    public async Task<List<AdminCategory>> GetCategoriesAsync()
    {
        await EnsureReadyAsync();
        var list = new List<AdminCategory>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Categories ORDER BY Name;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AdminCategory
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Slug = reader.GetString(reader.GetOrdinal("Slug")),
                ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                ProductCount = reader.GetInt32(reader.GetOrdinal("ProductCount"))
            });
        }

        return list;
    }

    public async Task<AdminCategory> UpsertCategoryAsync(AdminCategory category)
    {
        await EnsureReadyAsync();
        if (string.IsNullOrWhiteSpace(category.Id))
            category.Id = $"cat-{Guid.NewGuid():N}"[..12];

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Categories (Id, Name, Slug, ImageUrl, Description, Status, ProductCount)
            VALUES ($id, $name, $slug, $imageUrl, $description, $status, $productCount)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Slug = excluded.Slug,
                ImageUrl = excluded.ImageUrl,
                Description = excluded.Description,
                Status = excluded.Status,
                ProductCount = excluded.ProductCount;
            """;
        command.Parameters.AddWithValue("$id", category.Id);
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$slug", category.Slug);
        command.Parameters.AddWithValue("$imageUrl", category.ImageUrl ?? "");
        command.Parameters.AddWithValue("$description", category.Description ?? "");
        command.Parameters.AddWithValue("$status", category.Status);
        command.Parameters.AddWithValue("$productCount", category.ProductCount);
        await command.ExecuteNonQueryAsync();
        return category;
    }

    public async Task<bool> DeleteCategoryAsync(string id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    #endregion

    #region Orders

    public async Task<List<AdminOrder>> GetOrdersAsync()
    {
        await EnsureReadyAsync();
        var orders = new List<AdminOrder>();
        await using var connection = await OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Orders ORDER BY Date DESC, Id DESC;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                orders.Add(ReadOrder(reader));
        }

        foreach (var order in orders)
            order.Items = await GetOrderItemsAsync(connection, order.Id);

        return orders;
    }

    public async Task<AdminOrder?> GetOrderByIdAsync(string id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        AdminOrder? order;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Orders WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;
            order = ReadOrder(reader);
        }

        order.Items = await GetOrderItemsAsync(connection, id);
        return order;
    }

    public async Task<AdminOrder> UpsertOrderAsync(AdminOrder order)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();

        string? previousStatus = null;
        await using (var existingCmd = connection.CreateCommand())
        {
            existingCmd.CommandText = "SELECT Status FROM Orders WHERE Id = $id;";
            existingCmd.Parameters.AddWithValue("$id", order.Id);
            var result = await existingCmd.ExecuteScalarAsync();
            if (result is string s)
                previousStatus = s;
        }

        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();
        var now = DateTime.UtcNow.ToString("O");

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = """
                INSERT INTO Orders (
                    Id, CustomerId, CustomerName, CustomerEmail, Date, Total, PaymentStatus, Fulfillment, Status,
                    DiscountAmount, ShippingFee, TaxAmount, Subtotal, UpdatedAt, UserId
                ) VALUES (
                    $id, $customerId, $customerName, $customerEmail, $date, $total, $paymentStatus, $fulfillment, $status,
                    $discount, $shipping, $tax, $subtotal, $updatedAt, $userId
                )
                ON CONFLICT(Id) DO UPDATE SET
                    CustomerId = excluded.CustomerId,
                    CustomerName = excluded.CustomerName,
                    CustomerEmail = excluded.CustomerEmail,
                    Date = excluded.Date,
                    Total = excluded.Total,
                    PaymentStatus = excluded.PaymentStatus,
                    Fulfillment = excluded.Fulfillment,
                    Status = excluded.Status,
                    DiscountAmount = excluded.DiscountAmount,
                    ShippingFee = excluded.ShippingFee,
                    TaxAmount = excluded.TaxAmount,
                    Subtotal = excluded.Subtotal,
                    UpdatedAt = excluded.UpdatedAt,
                    UserId = excluded.UserId;
                """;
            command.Parameters.AddWithValue("$id", order.Id);
            command.Parameters.AddWithValue("$customerId", order.CustomerId ?? "");
            command.Parameters.AddWithValue("$customerName", order.CustomerName);
            command.Parameters.AddWithValue("$customerEmail", order.CustomerEmail);
            command.Parameters.AddWithValue("$date", order.Date.ToString("O"));
            command.Parameters.AddWithValue("$total", order.Total);
            command.Parameters.AddWithValue("$paymentStatus", order.PaymentStatus);
            command.Parameters.AddWithValue("$fulfillment", order.Fulfillment);
            command.Parameters.AddWithValue("$status", order.Status);
            command.Parameters.AddWithValue("$discount", 0m);
            command.Parameters.AddWithValue("$shipping", 0m);
            command.Parameters.AddWithValue("$tax", 0m);
            command.Parameters.AddWithValue("$subtotal", order.Total);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.Parameters.AddWithValue(
                "$userId",
                int.TryParse(order.CustomerId, out var parsedUserId) ? parsedUserId : DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM OrderItems WHERE OrderId = $orderId;";
            delete.Parameters.AddWithValue("$orderId", order.Id);
            await delete.ExecuteNonQueryAsync();
        }

        foreach (var item in order.Items)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO OrderItems (OrderId, ProductId, Name, ImageUrl, Quantity, Price)
                VALUES ($orderId, $productId, $name, $imageUrl, $quantity, $price);
                """;
            insert.Parameters.AddWithValue("$orderId", order.Id);
            insert.Parameters.AddWithValue("$productId", item.ProductId);
            insert.Parameters.AddWithValue("$name", item.Name);
            insert.Parameters.AddWithValue("$imageUrl", item.ImageUrl ?? "");
            insert.Parameters.AddWithValue("$quantity", item.Quantity);
            insert.Parameters.AddWithValue("$price", item.Price);
            await insert.ExecuteNonQueryAsync();
        }

        if (!string.Equals(previousStatus, order.Status, StringComparison.OrdinalIgnoreCase))
        {
            await AppendOrderStatusHistoryCoreAsync(
                connection, tx, order.Id, previousStatus, order.Status, null, null);

            if (order.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(previousStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                await ReversePromotionUsageForOrderAsync(connection, tx, order.Id);
            }
        }

        await tx.CommitAsync();
        return order;
    }

    public async Task<bool> DeleteOrderAsync(string id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Orders WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task<List<AdminOrderItem>> GetOrderItemsAsync(string orderId)
    {
        await using var connection = await OpenAsync();
        return await GetOrderItemsAsync(connection, orderId);
    }

    private static async Task<List<AdminOrderItem>> GetOrderItemsAsync(SqliteConnection connection, string orderId)
    {
        var items = new List<AdminOrderItem>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProductId, Name, ImageUrl, Quantity, Price FROM OrderItems WHERE OrderId = $orderId;";
        command.Parameters.AddWithValue("$orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new AdminOrderItem
            {
                ProductId = reader.GetInt32(0),
                Name = reader.GetString(1),
                ImageUrl = reader.GetString(2),
                Quantity = reader.GetInt32(3),
                Price = reader.GetDecimal(4)
            });
        }

        return items;
    }

    private static AdminOrder ReadOrder(SqliteDataReader reader)
    {
        var order = new AdminOrder
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            CustomerId = reader.GetString(reader.GetOrdinal("CustomerId")),
            CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerEmail = reader.GetString(reader.GetOrdinal("CustomerEmail")),
            Date = DateTime.Parse(reader.GetString(reader.GetOrdinal("Date"))),
            Total = reader.GetDecimal(reader.GetOrdinal("Total")),
            PaymentStatus = reader.GetString(reader.GetOrdinal("PaymentStatus")),
            Fulfillment = reader.GetString(reader.GetOrdinal("Fulfillment")),
            Status = reader.GetString(reader.GetOrdinal("Status"))
        };

        try
        {
            var subtotalOrdinal = reader.GetOrdinal("Subtotal");
            if (!reader.IsDBNull(subtotalOrdinal))
                order.Subtotal = reader.GetDecimal(subtotalOrdinal);
        }
        catch (IndexOutOfRangeException) { /* older schema */ }

        try
        {
            var discountOrdinal = reader.GetOrdinal("DiscountAmount");
            if (!reader.IsDBNull(discountOrdinal))
                order.DiscountAmount = reader.GetDecimal(discountOrdinal);
        }
        catch (IndexOutOfRangeException) { }

        try
        {
            var promoIdOrdinal = reader.GetOrdinal("PromotionId");
            if (!reader.IsDBNull(promoIdOrdinal))
                order.PromotionId = reader.GetString(promoIdOrdinal);
        }
        catch (IndexOutOfRangeException) { }

        try
        {
            var promoCodeOrdinal = reader.GetOrdinal("PromotionCode");
            if (!reader.IsDBNull(promoCodeOrdinal))
                order.PromotionCode = reader.GetString(promoCodeOrdinal);
        }
        catch (IndexOutOfRangeException) { }

        if (order.Subtotal <= 0)
            order.Subtotal = order.Total + order.DiscountAmount;

        return order;
    }

    #endregion

    #region Customers / Notifications / Staff / Promotions

    public async Task<List<AdminCustomer>> GetCustomersAsync()
    {
        await EnsureReadyAsync();
        var list = new List<AdminCustomer>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                u.Id,
                COALESCE(u.FirstName, '') AS FirstName,
                COALESCE(u.LastName, '') AS LastName,
                u.Email,
                COALESCE(u.PhoneNumber, '') AS PhoneNumber,
                u.CreatedAt,
                COALESCE(u.Status, 'Active') AS Status,
                u.LastLoginAt,
                (
                    SELECT COUNT(*)
                    FROM Orders o
                    WHERE o.UserId = u.Id
                       OR lower(o.CustomerEmail) = lower(u.Email)
                       OR o.CustomerId = CAST(u.Id AS TEXT)
                ) AS TotalOrders,
                (
                    SELECT COALESCE(SUM(o.Total), 0)
                    FROM Orders o
                    WHERE (o.UserId = u.Id
                        OR lower(o.CustomerEmail) = lower(u.Email)
                        OR o.CustomerId = CAST(u.Id AS TEXT))
                      AND o.Status = 'Completed'
                ) AS TotalSpent
            FROM Users u
            INNER JOIN Roles r ON r.Id = u.RoleId
            WHERE r.Name = 'Customer'
            ORDER BY u.Id;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var first = reader.GetString(1);
            var last = reader.GetString(2);
            var name = $"{first} {last}".Trim();
            DateTime joined = DateTime.UtcNow;
            if (!reader.IsDBNull(5) && DateTime.TryParse(reader.GetString(5), out var parsedJoined))
                joined = parsedJoined;
            DateTime? lastLogin = null;
            if (!reader.IsDBNull(7) && DateTime.TryParse(reader.GetString(7), out var parsedLogin))
                lastLogin = parsedLogin;

            list.Add(new AdminCustomer
            {
                Id = reader.GetInt32(0).ToString(),
                Name = string.IsNullOrWhiteSpace(name) ? reader.GetString(3) : name,
                Email = reader.GetString(3),
                Contact = reader.GetString(4),
                DateJoined = joined,
                Status = reader.GetString(6),
                LastLoginAt = lastLogin,
                TotalOrders = Convert.ToInt32(reader.GetValue(8)),
                TotalSpent = Convert.ToDecimal(reader.GetValue(9))
            });
        }

        return list;
    }

    public async Task<AdminCustomer?> GetCustomerByIdAsync(string id)
    {
        var all = await GetCustomersAsync();
        return all.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AdminCustomer> UpsertCustomerAsync(AdminCustomer customer)
    {
        await EnsureReadyAsync();
        if (string.IsNullOrWhiteSpace(customer.Id) || !int.TryParse(customer.Id, out var userId))
            return customer;

        var nameParts = (customer.Name ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : customer.Name;
        var lastName = nameParts.Length > 1 ? nameParts[1] : "";

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET FirstName = $firstName,
                LastName = $lastName,
                PhoneNumber = $phone,
                Status = $status,
                UpdatedAt = $updatedAt
            WHERE Id = $id
              AND RoleId = (SELECT Id FROM Roles WHERE Name = 'Customer' LIMIT 1);
            """;
        command.Parameters.AddWithValue("$firstName", firstName ?? "");
        command.Parameters.AddWithValue("$lastName", lastName);
        command.Parameters.AddWithValue("$phone", customer.Contact ?? "");
        command.Parameters.AddWithValue("$status", string.IsNullOrWhiteSpace(customer.Status) ? "Active" : customer.Status);
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", userId);
        await command.ExecuteNonQueryAsync();
        return customer;
    }

    public async Task<List<MockNotification>> GetCustomerNotificationsAsync(string? email = null)
    {
        await EnsureReadyAsync();
        var list = new List<MockNotification>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(email))
        {
            command.CommandText = "SELECT Id, Title, Message, TimeAgo, Icon, Tone, IsRead FROM Notifications ORDER BY RowId DESC;";
        }
        else
        {
            command.CommandText = """
                SELECT Id, Title, Message, TimeAgo, Icon, Tone, IsRead
                FROM Notifications WHERE Email = $email OR Email = ''
                ORDER BY RowId DESC;
                """;
            command.Parameters.AddWithValue("$email", email.Trim().ToLowerInvariant());
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new MockNotification
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Message = reader.GetString(2),
                TimeAgo = reader.GetString(3),
                Icon = reader.GetString(4),
                Tone = reader.GetString(5),
                IsRead = reader.GetInt32(6) == 1
            });
        }

        return list;
    }

    public async Task SaveCustomerNotificationsAsync(IEnumerable<MockNotification> items, string? email = null)
    {
        await EnsureReadyAsync();
        var emailKey = string.IsNullOrWhiteSpace(email) ? "" : email.Trim().ToLowerInvariant();
        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM Notifications WHERE Email = $email;";
            delete.Parameters.AddWithValue("$email", emailKey);
            await delete.ExecuteNonQueryAsync();
        }

        foreach (var item in items)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO Notifications (Id, Email, Title, Message, TimeAgo, Icon, Tone, IsRead)
                VALUES ($id, $email, $title, $message, $timeAgo, $icon, $tone, $isRead);
                """;
            insert.Parameters.AddWithValue("$id", item.Id);
            insert.Parameters.AddWithValue("$email", emailKey);
            insert.Parameters.AddWithValue("$title", item.Title);
            insert.Parameters.AddWithValue("$message", item.Message);
            insert.Parameters.AddWithValue("$timeAgo", item.TimeAgo);
            insert.Parameters.AddWithValue("$icon", item.Icon);
            insert.Parameters.AddWithValue("$tone", item.Tone);
            insert.Parameters.AddWithValue("$isRead", item.IsRead ? 1 : 0);
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task<List<AdminNotificationItem>> GetAdminNotificationsAsync()
    {
        await EnsureReadyAsync();
        var list = new List<AdminNotificationItem>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AdminNotifications ORDER BY Timestamp DESC;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AdminNotificationItem
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Type = reader.GetString(reader.GetOrdinal("Type")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Message = reader.GetString(reader.GetOrdinal("Message")),
                RelatedId = reader.IsDBNull(reader.GetOrdinal("RelatedId")) ? null : reader.GetString(reader.GetOrdinal("RelatedId")),
                RelatedLabel = reader.IsDBNull(reader.GetOrdinal("RelatedLabel")) ? null : reader.GetString(reader.GetOrdinal("RelatedLabel")),
                RelatedHref = reader.IsDBNull(reader.GetOrdinal("RelatedHref")) ? null : reader.GetString(reader.GetOrdinal("RelatedHref")),
                Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
                Read = reader.GetInt32(reader.GetOrdinal("Read")) == 1
            });
        }

        return list;
    }

    public async Task SaveAdminNotificationsAsync(IEnumerable<AdminNotificationItem> items)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM AdminNotifications;";
            await delete.ExecuteNonQueryAsync();
        }

        foreach (var item in items)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO AdminNotifications (Id, Type, Title, Message, RelatedId, RelatedLabel, RelatedHref, Timestamp, Read)
                VALUES ($id, $type, $title, $message, $relatedId, $relatedLabel, $relatedHref, $timestamp, $read);
                """;
            insert.Parameters.AddWithValue("$id", item.Id);
            insert.Parameters.AddWithValue("$type", item.Type);
            insert.Parameters.AddWithValue("$title", item.Title);
            insert.Parameters.AddWithValue("$message", item.Message);
            insert.Parameters.AddWithValue("$relatedId", (object?)item.RelatedId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$relatedLabel", (object?)item.RelatedLabel ?? DBNull.Value);
            insert.Parameters.AddWithValue("$relatedHref", (object?)item.RelatedHref ?? DBNull.Value);
            insert.Parameters.AddWithValue("$timestamp", item.Timestamp.ToString("O"));
            insert.Parameters.AddWithValue("$read", item.Read ? 1 : 0);
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task<List<AdminStaffMember>> GetStaffAsync()
    {
        await EnsureReadyAsync();
        var list = new List<AdminStaffMember>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Staff ORDER BY Id;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var permissionsJson = reader.GetString(reader.GetOrdinal("PermissionsJson"));
            list.Add(new AdminStaffMember
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                Role = reader.GetString(reader.GetOrdinal("Role")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                LastLogin = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastLogin"))),
                IsPrimaryAdmin = reader.GetInt32(reader.GetOrdinal("IsPrimaryAdmin")) == 1,
                Permissions = JsonSerializer.Deserialize<AdminStaffPermissions>(permissionsJson, JsonOptions)
                              ?? AdminStaffPermissions.DefaultStaff()
            });
        }

        return list;
    }

    public async Task<AdminStaffMember> UpsertStaffAsync(AdminStaffMember staff)
    {
        await EnsureReadyAsync();
        if (string.IsNullOrWhiteSpace(staff.Id))
            staff.Id = $"S-{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Staff (Id, FirstName, LastName, Email, Role, Status, LastLogin, IsPrimaryAdmin, PermissionsJson)
            VALUES ($id, $firstName, $lastName, $email, $role, $status, $lastLogin, $isPrimaryAdmin, $permissions)
            ON CONFLICT(Id) DO UPDATE SET
                FirstName = excluded.FirstName,
                LastName = excluded.LastName,
                Email = excluded.Email,
                Role = excluded.Role,
                Status = excluded.Status,
                LastLogin = excluded.LastLogin,
                IsPrimaryAdmin = excluded.IsPrimaryAdmin,
                PermissionsJson = excluded.PermissionsJson;
            """;
        command.Parameters.AddWithValue("$id", staff.Id);
        command.Parameters.AddWithValue("$firstName", staff.FirstName);
        command.Parameters.AddWithValue("$lastName", staff.LastName);
        command.Parameters.AddWithValue("$email", staff.Email);
        command.Parameters.AddWithValue("$role", staff.Role);
        command.Parameters.AddWithValue("$status", staff.Status);
        command.Parameters.AddWithValue("$lastLogin", staff.LastLogin.ToString("O"));
        command.Parameters.AddWithValue("$isPrimaryAdmin", staff.IsPrimaryAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$permissions", JsonSerializer.Serialize(staff.Permissions, JsonOptions));
        await command.ExecuteNonQueryAsync();
        return staff;
    }

    public async Task<bool> DeleteStaffAsync(string id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Staff WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<AdminPromotion>> GetPromotionsAsync()
    {
        await EnsureReadyAsync();
        var list = new List<AdminPromotion>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                p.Id, p.Name, p.Code, p.DiscountType, p.DiscountValue, p.MinimumOrder,
                p.UsedCount, p.UsageLimit, p.StartDate, p.EndDate, p.Enabled, p.Description,
                (
                    SELECT COUNT(*) FROM PromotionUsages u
                    WHERE u.PromotionId = p.Id
                      AND COALESCE(u.Status, 'Redeemed') = 'Redeemed'
                ) AS RedeemedUses
            FROM Promotions p
            ORDER BY p.Id;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var promo = new AdminPromotion
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Code = reader.GetString(2),
                DiscountType = reader.GetString(3),
                DiscountValue = reader.GetDecimal(4),
                MinimumOrder = reader.GetDecimal(5),
                UsedCount = reader.GetInt32(12),
                UsageLimit = reader.GetInt32(7),
                StartDate = DateTime.Parse(reader.GetString(8)),
                EndDate = DateTime.Parse(reader.GetString(9)),
                Enabled = reader.GetInt32(10) == 1,
                Description = reader.IsDBNull(11) ? "" : reader.GetString(11)
            };
            list.Add(promo);
        }

        foreach (var promo in list)
        {
            promo.ProductIds = await LoadPromotionProductIdsAsync(connection, null, promo.Id);
            promo.CategoryIds = await LoadPromotionCategoryIdsAsync(connection, null, promo.Id);

            // Optional columns may not exist on very old DBs before migration runs.
            await using var extra = connection.CreateCommand();
            extra.CommandText = "SELECT MaximumDiscount, UsagePerCustomer FROM Promotions WHERE Id = $id;";
            extra.Parameters.AddWithValue("$id", promo.Id);
            try
            {
                await using var er = await extra.ExecuteReaderAsync();
                if (await er.ReadAsync())
                {
                    if (!er.IsDBNull(0))
                        promo.MaximumDiscount = er.GetDecimal(0);
                    if (!er.IsDBNull(1))
                        promo.UsagePerCustomer = er.GetInt32(1);
                }
            }
            catch
            {
                promo.UsagePerCustomer = 1;
            }
        }

        return list;
    }

    public async Task<AdminPromotion> UpsertPromotionAsync(AdminPromotion promotion)
    {
        await EnsureReadyAsync();
        if (string.IsNullOrWhiteSpace(promotion.Id))
            promotion.Id = $"PROMO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        promotion.Code = promotion.Code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = """
                INSERT INTO Promotions (
                    Id, Name, Code, DiscountType, DiscountValue, MinimumOrder, UsedCount, UsageLimit,
                    StartDate, EndDate, Enabled, Description, MaximumDiscount, UsagePerCustomer, CreatedAt, UpdatedAt
                ) VALUES (
                    $id, $name, $code, $discountType, $discountValue, $minimumOrder, $usedCount, $usageLimit,
                    $startDate, $endDate, $enabled, $description, $maxDiscount, $perCustomer, $createdAt, $updatedAt
                )
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    Code = excluded.Code,
                    DiscountType = excluded.DiscountType,
                    DiscountValue = excluded.DiscountValue,
                    MinimumOrder = excluded.MinimumOrder,
                    UsageLimit = excluded.UsageLimit,
                    StartDate = excluded.StartDate,
                    EndDate = excluded.EndDate,
                    Enabled = excluded.Enabled,
                    Description = excluded.Description,
                    MaximumDiscount = excluded.MaximumDiscount,
                    UsagePerCustomer = excluded.UsagePerCustomer,
                    UpdatedAt = excluded.UpdatedAt;
                """;
            command.Parameters.AddWithValue("$id", promotion.Id);
            command.Parameters.AddWithValue("$name", promotion.Name);
            command.Parameters.AddWithValue("$code", promotion.Code);
            command.Parameters.AddWithValue("$discountType", promotion.DiscountType);
            command.Parameters.AddWithValue("$discountValue", promotion.DiscountValue);
            command.Parameters.AddWithValue("$minimumOrder", promotion.MinimumOrder);
            command.Parameters.AddWithValue("$usedCount", Math.Max(0, promotion.UsedCount));
            command.Parameters.AddWithValue("$usageLimit", promotion.UsageLimit);
            command.Parameters.AddWithValue("$startDate", promotion.StartDate.ToString("O"));
            command.Parameters.AddWithValue("$endDate", promotion.EndDate.ToString("O"));
            command.Parameters.AddWithValue("$enabled", promotion.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$description", promotion.Description ?? "");
            command.Parameters.AddWithValue("$maxDiscount", (object?)promotion.MaximumDiscount ?? DBNull.Value);
            command.Parameters.AddWithValue("$perCustomer", promotion.UsagePerCustomer <= 0 ? 1 : promotion.UsagePerCustomer);
            command.Parameters.AddWithValue("$createdAt", now);
            command.Parameters.AddWithValue("$updatedAt", now);
            await command.ExecuteNonQueryAsync();
        }

        await using (var clearProducts = connection.CreateCommand())
        {
            clearProducts.Transaction = tx;
            clearProducts.CommandText = "DELETE FROM PromotionProducts WHERE PromotionId = $id;";
            clearProducts.Parameters.AddWithValue("$id", promotion.Id);
            await clearProducts.ExecuteNonQueryAsync();
        }

        foreach (var productId in promotion.ProductIds.Distinct())
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT OR IGNORE INTO PromotionProducts (PromotionId, ProductId)
                VALUES ($promoId, $productId);
                """;
            insert.Parameters.AddWithValue("$promoId", promotion.Id);
            insert.Parameters.AddWithValue("$productId", productId);
            await insert.ExecuteNonQueryAsync();
        }

        await using (var clearCategories = connection.CreateCommand())
        {
            clearCategories.Transaction = tx;
            clearCategories.CommandText = "DELETE FROM PromotionCategories WHERE PromotionId = $id;";
            clearCategories.Parameters.AddWithValue("$id", promotion.Id);
            await clearCategories.ExecuteNonQueryAsync();
        }

        foreach (var categoryId in promotion.CategoryIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT OR IGNORE INTO PromotionCategories (PromotionId, CategoryId)
                VALUES ($promoId, $categoryId);
                """;
            insert.Parameters.AddWithValue("$promoId", promotion.Id);
            insert.Parameters.AddWithValue("$categoryId", categoryId);
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return promotion;
    }

    public async Task<bool> DeletePromotionAsync(string id)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();

        await using (var usageCount = connection.CreateCommand())
        {
            usageCount.CommandText = """
                SELECT COUNT(*) FROM PromotionUsages
                WHERE PromotionId = $id AND COALESCE(Status, 'Redeemed') = 'Redeemed';
                """;
            usageCount.Parameters.AddWithValue("$id", id);
            var count = Convert.ToInt32(await usageCount.ExecuteScalarAsync());
            if (count > 0)
            {
                // Preserve history: deactivate instead of hard delete.
                await using var deactivate = connection.CreateCommand();
                deactivate.CommandText = "UPDATE Promotions SET Enabled = 0, UpdatedAt = $updatedAt WHERE Id = $id;";
                deactivate.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
                deactivate.Parameters.AddWithValue("$id", id);
                return await deactivate.ExecuteNonQueryAsync() > 0;
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Promotions WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    #endregion

    #region Inventory / Wishlist / Settings / Stats

    public async Task<List<InventoryHistoryEntry>> GetInventoryHistoryAsync()
    {
        await EnsureReadyAsync();
        var list = new List<InventoryHistoryEntry>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM InventoryHistory ORDER BY Date DESC;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new InventoryHistoryEntry
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                Type = reader.GetString(reader.GetOrdinal("Type")),
                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                PreviousStock = reader.GetInt32(reader.GetOrdinal("PreviousStock")),
                NewStock = reader.GetInt32(reader.GetOrdinal("NewStock")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                Notes = reader.GetString(reader.GetOrdinal("Notes")),
                Date = DateTime.Parse(reader.GetString(reader.GetOrdinal("Date"))),
                AdminName = reader.GetString(reader.GetOrdinal("AdminName"))
            });
        }

        return list;
    }

    public async Task AddInventoryHistoryAsync(InventoryHistoryEntry entry)
    {
        await EnsureReadyAsync();
        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Guid.NewGuid().ToString("N");

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO InventoryHistory (
                Id, ProductId, ProductName, Type, Quantity, PreviousStock, NewStock, Reason, Notes, Date, AdminName
            ) VALUES (
                $id, $productId, $productName, $type, $quantity, $previousStock, $newStock, $reason, $notes, $date, $adminName
            );
            """;
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$productId", entry.ProductId);
        command.Parameters.AddWithValue("$productName", entry.ProductName);
        command.Parameters.AddWithValue("$type", entry.Type);
        command.Parameters.AddWithValue("$quantity", entry.Quantity);
        command.Parameters.AddWithValue("$previousStock", entry.PreviousStock);
        command.Parameters.AddWithValue("$newStock", entry.NewStock);
        command.Parameters.AddWithValue("$reason", entry.Reason);
        command.Parameters.AddWithValue("$notes", entry.Notes ?? "");
        command.Parameters.AddWithValue("$date", entry.Date.ToString("O"));
        command.Parameters.AddWithValue("$adminName", entry.AdminName);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<int, int>> GetReservedStockAsync()
    {
        await EnsureReadyAsync();
        var map = new Dictionary<int, int>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProductId, Reserved FROM InventoryMeta;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            map[reader.GetInt32(0)] = reader.GetInt32(1);
        return map;
    }

    public async Task SetReservedStockAsync(int productId, int reserved)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO InventoryMeta (ProductId, Reserved, LowStockLevel)
            VALUES ($productId, $reserved, 20)
            ON CONFLICT(ProductId) DO UPDATE SET Reserved = excluded.Reserved;
            """;
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$reserved", reserved);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<int, int>> GetLowStockLevelsAsync()
    {
        await EnsureReadyAsync();
        var map = new Dictionary<int, int>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProductId, LowStockLevel FROM InventoryMeta;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            map[reader.GetInt32(0)] = reader.GetInt32(1);
        return map;
    }

    public async Task SetLowStockLevelAsync(int productId, int level)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO InventoryMeta (ProductId, Reserved, LowStockLevel)
            VALUES ($productId, 0, $level)
            ON CONFLICT(ProductId) DO UPDATE SET LowStockLevel = excluded.LowStockLevel;
            """;
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$level", level);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<int>> GetWishlistAsync(string email)
    {
        await EnsureReadyAsync();
        var list = new List<int>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProductId FROM Wishlists WHERE Email = $email;";
        command.Parameters.AddWithValue("$email", email.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetInt32(0));
        return list;
    }

    public async Task SaveWishlistAsync(string email, IEnumerable<int> productIds)
    {
        await EnsureReadyAsync();
        var emailKey = email.Trim().ToLowerInvariant();
        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM Wishlists WHERE Email = $email;";
            delete.Parameters.AddWithValue("$email", emailKey);
            await delete.ExecuteNonQueryAsync();
        }

        foreach (var id in productIds.Distinct())
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = "INSERT INTO Wishlists (Email, ProductId) VALUES ($email, $productId);";
            insert.Parameters.AddWithValue("$email", emailKey);
            insert.Parameters.AddWithValue("$productId", id);
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();

        await using var sys = connection.CreateCommand();
        sys.CommandText = """
            INSERT INTO SystemSettings (SettingKey, SettingValue, Description, UpdatedBy, UpdatedAt)
            VALUES ($key, $value, NULL, 'admin', $updatedAt)
            ON CONFLICT(SettingKey) DO UPDATE SET
                SettingValue = excluded.SettingValue,
                UpdatedBy = excluded.UpdatedBy,
                UpdatedAt = excluded.UpdatedAt;
            """;
        sys.Parameters.AddWithValue("$key", key);
        sys.Parameters.AddWithValue("$value", value);
        sys.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        await sys.ExecuteNonQueryAsync();
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        await EnsureReadyAsync();
        var products = await GetProductsAsync();
        var orders = await GetOrdersAsync();
        var lowThreshold = AdminProduct.LowStockThreshold;

        var stats = new DashboardStats
        {
            TotalSales = orders.Where(o => !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                .Sum(o => o.Total),
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o =>
                o.Status is "Pending" or "Processing" or "Confirmed" or "Ready for Pickup"),
            CompletedOrders = orders.Count(o => o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)),
            TotalProducts = products.Count,
            LowStockCount = products.Count(p => p.Stock > 0 && p.Stock <= lowThreshold)
        };

        stats.RecentOrders = orders
            .OrderByDescending(o => o.Date)
            .Take(6)
            .Select(o => new AdminOrderRow(
                o.Id,
                o.CustomerName,
                o.Fulfillment,
                o.Date.ToString("yyyy-MM-dd"),
                o.Total,
                o.Status))
            .ToList();

        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Completed"] = "#22C55E",
            ["Processing"] = "#3B82F6",
            ["Pending"] = "#F9C424",
            ["Ready for Pickup"] = "#A855F7",
            ["Cancelled"] = "#EF4444",
            ["Confirmed"] = "#64748B"
        };

        stats.OrderStatus = orders
            .GroupBy(o => o.Status)
            .Select(g => new AdminStatusSlice(g.Key, g.Count(), colors.GetValueOrDefault(g.Key, "#94A3B8")))
            .OrderByDescending(s => s.Count)
            .ToList();

        stats.TopProducts = products
            .OrderByDescending(p => p.Sold)
            .ThenByDescending(p => p.Price * p.Sold)
            .Take(5)
            .Select((p, i) => new AdminTopProduct(
                i + 1,
                p.Name,
                string.IsNullOrWhiteSpace(p.ImageUrl) ? CatalogHelpers.PlaceholderImage : p.ImageUrl,
                p.Sold,
                p.Sold * p.Price,
                p.Name.Length > 18 ? p.Name[..18] : p.Name))
            .ToList();

        stats.SalesByMonth = Enumerable.Range(0, 8)
            .Select(i =>
            {
                var month = DateTime.Today.AddMonths(i - 7);
                var label = month.ToString("MMM");
                var revenue = (double)orders
                    .Where(o => o.Date.Year == month.Year && o.Date.Month == month.Month &&
                                !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                    .Sum(o => o.Total);
                return new MonthlySalesPoint { Month = label, Value = revenue };
            })
            .ToList();

        return stats;
    }

    public async Task<StorefrontStats> GetStorefrontStatsAsync()
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();

        async Task<int> ScalarAsync(string sql)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        return new StorefrontStats
        {
            PublishedProducts = await ScalarAsync(
                "SELECT COUNT(*) FROM Products WHERE Status = 'Active' AND (IsPublished = 1 OR IsPublished IS NULL);"),
            ActiveCustomers = await ScalarAsync("""
                SELECT COUNT(*)
                FROM Users u
                INNER JOIN Roles r ON r.Id = u.RoleId
                WHERE r.Name = 'Customer' AND COALESCE(u.Status, 'Active') = 'Active';
                """),
            TotalOrders = await ScalarAsync("SELECT COUNT(*) FROM Orders;")
        };
    }

    public async Task PlaceCheckoutOrderAsync(AdminOrder order, string? promoCode, decimal discountAmount, string userEmail)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        try
        {
            if (string.IsNullOrWhiteSpace(order.Id))
                order.Id = await GenerateOrderNumberAsync(connection, tx);

            var emailKey = (userEmail ?? order.CustomerEmail ?? "").Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;
            var nowText = now.ToString("O");
            var subtotal = order.Items.Sum(i => i.Price * i.Quantity);

            int? userId = null;
            await using (var userLookup = connection.CreateCommand())
            {
                userLookup.Transaction = tx;
                userLookup.CommandText = "SELECT Id FROM Users WHERE lower(Email) = $email LIMIT 1;";
                userLookup.Parameters.AddWithValue("$email", emailKey);
                var userObj = await userLookup.ExecuteScalarAsync();
                if (userObj is not null and not DBNull)
                    userId = Convert.ToInt32(userObj);
            }

            if (userId is int resolvedId && string.IsNullOrWhiteSpace(order.CustomerId))
                order.CustomerId = resolvedId.ToString();

            // Never trust client-calculated discount — revalidate on the server.
            _ = discountAmount;
            discountAmount = 0;
            string? resolvedPromoId = null;
            string? resolvedPromoCode = null;
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var validation = await ValidatePromotionCoreAsync(connection, tx, new PromoValidationRequest
                {
                    Code = promoCode,
                    UserEmail = emailKey,
                    UserId = userId,
                    Items = order.Items.Select(i => new PromoCartItem
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.Price
                    }).ToList()
                });

                if (!validation.Valid)
                    throw new InvalidOperationException(validation.Message);

                discountAmount = validation.DiscountAmount;
                resolvedPromoId = validation.PromotionId;
                resolvedPromoCode = validation.Code;
            }

            order.Subtotal = subtotal;
            order.DiscountAmount = discountAmount;
            order.PromotionId = resolvedPromoId;
            order.PromotionCode = resolvedPromoCode;
            order.Total = Math.Max(0, subtotal - discountAmount);
            order.Date = order.Date == default ? now : order.Date;
            if (string.IsNullOrWhiteSpace(order.Status))
                order.Status = "Pending";
            if (string.IsNullOrWhiteSpace(order.PaymentStatus))
                order.PaymentStatus = "Pending";
            if (string.IsNullOrWhiteSpace(order.Fulfillment))
                order.Fulfillment = "Campus Pickup";

            foreach (var item in order.Items)
            {
                await using var stockCmd = connection.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText = "SELECT Stock, Name FROM Products WHERE Id = $id;";
                stockCmd.Parameters.AddWithValue("$id", item.ProductId);
                await using var reader = await stockCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException($"Product {item.ProductId} was not found.");
                var stock = reader.GetInt32(0);
                var name = reader.GetString(1);
                await reader.DisposeAsync();

                if (stock < item.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for {name}.");
            }

            await using (var orderCmd = connection.CreateCommand())
            {
                orderCmd.Transaction = tx;
                orderCmd.CommandText = """
                    INSERT INTO Orders (
                        Id, CustomerId, CustomerName, CustomerEmail, Date, Total, PaymentStatus, Fulfillment, Status,
                        DiscountAmount, ShippingFee, TaxAmount, Subtotal, UpdatedAt, UserId, PromotionId, PromotionCode
                    ) VALUES (
                        $id, $customerId, $customerName, $customerEmail, $date, $total, $paymentStatus, $fulfillment, $status,
                        $discount, 0, 0, $subtotal, $updatedAt, $userId, $promotionId, $promotionCode
                    );
                    """;
                orderCmd.Parameters.AddWithValue("$id", order.Id);
                orderCmd.Parameters.AddWithValue("$customerId", order.CustomerId ?? "");
                orderCmd.Parameters.AddWithValue("$customerName", order.CustomerName);
                orderCmd.Parameters.AddWithValue("$customerEmail", order.CustomerEmail);
                orderCmd.Parameters.AddWithValue("$date", order.Date.ToString("O"));
                orderCmd.Parameters.AddWithValue("$total", order.Total);
                orderCmd.Parameters.AddWithValue("$paymentStatus", order.PaymentStatus);
                orderCmd.Parameters.AddWithValue("$fulfillment", order.Fulfillment);
                orderCmd.Parameters.AddWithValue("$status", order.Status);
                orderCmd.Parameters.AddWithValue("$discount", discountAmount);
                orderCmd.Parameters.AddWithValue("$subtotal", subtotal);
                orderCmd.Parameters.AddWithValue("$updatedAt", nowText);
                orderCmd.Parameters.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
                orderCmd.Parameters.AddWithValue("$promotionId", (object?)resolvedPromoId ?? DBNull.Value);
                orderCmd.Parameters.AddWithValue("$promotionCode", (object?)resolvedPromoCode ?? DBNull.Value);
                await orderCmd.ExecuteNonQueryAsync();
            }

            foreach (var item in order.Items)
            {
                await using var itemCmd = connection.CreateCommand();
                itemCmd.Transaction = tx;
                itemCmd.CommandText = """
                    INSERT INTO OrderItems (OrderId, ProductId, Name, ImageUrl, Quantity, Price)
                    VALUES ($orderId, $productId, $name, $imageUrl, $quantity, $price);
                    """;
                itemCmd.Parameters.AddWithValue("$orderId", order.Id);
                itemCmd.Parameters.AddWithValue("$productId", item.ProductId);
                itemCmd.Parameters.AddWithValue("$name", item.Name);
                itemCmd.Parameters.AddWithValue("$imageUrl", item.ImageUrl ?? "");
                itemCmd.Parameters.AddWithValue("$quantity", item.Quantity);
                itemCmd.Parameters.AddWithValue("$price", item.Price);
                await itemCmd.ExecuteNonQueryAsync();

                int previousStock;
                string productName;
                await using (var read = connection.CreateCommand())
                {
                    read.Transaction = tx;
                    read.CommandText = "SELECT Stock, Name, Sold FROM Products WHERE Id = $id;";
                    read.Parameters.AddWithValue("$id", item.ProductId);
                    await using var reader = await read.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    previousStock = reader.GetInt32(0);
                    productName = reader.GetString(1);
                    var sold = reader.GetInt32(2);
                    await reader.DisposeAsync();

                    var newStock = previousStock - item.Quantity;
                    await using var update = connection.CreateCommand();
                    update.Transaction = tx;
                    update.CommandText = """
                        UPDATE Products
                        SET Stock = $stock, Sold = $sold, InStock = $inStock, UpdatedAt = $updatedAt
                        WHERE Id = $id;
                        """;
                    update.Parameters.AddWithValue("$stock", newStock);
                    update.Parameters.AddWithValue("$sold", sold + item.Quantity);
                    update.Parameters.AddWithValue("$inStock", newStock > 0 ? 1 : 0);
                    update.Parameters.AddWithValue("$updatedAt", nowText);
                    update.Parameters.AddWithValue("$id", item.ProductId);
                    await update.ExecuteNonQueryAsync();
                }

                await using (var hist = connection.CreateCommand())
                {
                    hist.Transaction = tx;
                    hist.CommandText = """
                        INSERT INTO InventoryHistory (
                            Id, ProductId, ProductName, Type, Quantity, PreviousStock, NewStock, Reason, Notes, Date, AdminName
                        ) VALUES (
                            $id, $productId, $productName, 'Sale', $quantity, $previous, $new, 'Order', $notes, $date, $admin
                        );
                        """;
                    hist.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
                    hist.Parameters.AddWithValue("$productId", item.ProductId);
                    hist.Parameters.AddWithValue("$productName", productName);
                    hist.Parameters.AddWithValue("$quantity", item.Quantity);
                    hist.Parameters.AddWithValue("$previous", previousStock);
                    hist.Parameters.AddWithValue("$new", previousStock - item.Quantity);
                    hist.Parameters.AddWithValue("$notes", $"Order {order.Id}");
                    hist.Parameters.AddWithValue("$date", nowText);
                    hist.Parameters.AddWithValue("$admin", emailKey);
                    await hist.ExecuteNonQueryAsync();
                }

                int? variantId = null;
                await using (var variantCmd = connection.CreateCommand())
                {
                    variantCmd.Transaction = tx;
                    variantCmd.CommandText = """
                        SELECT Id FROM ProductVariants
                        WHERE ProductId = $productId AND VariantName = 'Default'
                        LIMIT 1;
                        """;
                    variantCmd.Parameters.AddWithValue("$productId", item.ProductId);
                    var v = await variantCmd.ExecuteScalarAsync();
                    if (v is not null and not DBNull)
                        variantId = Convert.ToInt32(v);
                }

                await using (var move = connection.CreateCommand())
                {
                    move.Transaction = tx;
                    move.CommandText = """
                        INSERT INTO InventoryMovements (
                            ProductId, VariantId, MovementType, Quantity, PreviousQuantity, NewQuantity,
                            ReferenceType, ReferenceId, Notes, PerformedBy, CreatedAt
                        ) VALUES (
                            $productId, $variantId, 'Sale', $quantity, $previous, $new,
                            'Order', $orderId, $notes, $performedBy, $createdAt
                        );
                        """;
                    move.Parameters.AddWithValue("$productId", item.ProductId);
                    move.Parameters.AddWithValue("$variantId", (object?)variantId ?? DBNull.Value);
                    move.Parameters.AddWithValue("$quantity", -item.Quantity);
                    move.Parameters.AddWithValue("$previous", previousStock);
                    move.Parameters.AddWithValue("$new", previousStock - item.Quantity);
                    move.Parameters.AddWithValue("$orderId", order.Id);
                    move.Parameters.AddWithValue("$notes", $"Checkout sale for {productName}");
                    move.Parameters.AddWithValue("$performedBy", emailKey);
                    move.Parameters.AddWithValue("$createdAt", nowText);
                    await move.ExecuteNonQueryAsync();
                }

                if (variantId is int vid)
                {
                    await using var inv = connection.CreateCommand();
                    inv.Transaction = tx;
                    inv.CommandText = """
                        UPDATE Inventory
                        SET QuantityOnHand = $qty, UpdatedAt = $updatedAt
                        WHERE ProductId = $productId AND VariantId = $variantId;
                        """;
                    inv.Parameters.AddWithValue("$qty", previousStock - item.Quantity);
                    inv.Parameters.AddWithValue("$updatedAt", nowText);
                    inv.Parameters.AddWithValue("$productId", item.ProductId);
                    inv.Parameters.AddWithValue("$variantId", vid);
                    await inv.ExecuteNonQueryAsync();

                    await using var pv = connection.CreateCommand();
                    pv.Transaction = tx;
                    pv.CommandText = """
                        UPDATE ProductVariants
                        SET StockQuantity = $qty, UpdatedAt = $updatedAt
                        WHERE Id = $id;
                        """;
                    pv.Parameters.AddWithValue("$qty", previousStock - item.Quantity);
                    pv.Parameters.AddWithValue("$updatedAt", nowText);
                    pv.Parameters.AddWithValue("$id", vid);
                    await pv.ExecuteNonQueryAsync();
                }
            }

            await AppendOrderStatusHistoryCoreAsync(connection, tx, order.Id, null, order.Status, "Order placed", emailKey);

            await using (var pay = connection.CreateCommand())
            {
                pay.Transaction = tx;
                pay.CommandText = """
                    INSERT INTO Payments (
                        OrderId, PaymentReference, PaymentMethod, Amount, Status, PaidAt, CreatedAt, UpdatedAt
                    ) VALUES (
                        $orderId, $ref, $method, $amount, 'Pending', NULL, $createdAt, $updatedAt
                    );
                    """;
                pay.Parameters.AddWithValue("$orderId", order.Id);
                pay.Parameters.AddWithValue("$ref", $"PAY-{order.Id}");
                pay.Parameters.AddWithValue("$method", "CampusPickup/PayOnPickup");
                pay.Parameters.AddWithValue("$amount", order.Total);
                pay.Parameters.AddWithValue("$createdAt", nowText);
                pay.Parameters.AddWithValue("$updatedAt", nowText);
                await pay.ExecuteNonQueryAsync();
            }

            if (!string.IsNullOrWhiteSpace(resolvedPromoId) && discountAmount > 0)
            {
                await using var usage = connection.CreateCommand();
                usage.Transaction = tx;
                usage.CommandText = """
                    INSERT INTO PromotionUsages (PromotionId, UserEmail, OrderId, DiscountAmount, UsedAt, UserId, Status)
                    VALUES ($promoId, $email, $orderId, $discount, $usedAt, $userId, 'Redeemed');
                    """;
                usage.Parameters.AddWithValue("$promoId", resolvedPromoId);
                usage.Parameters.AddWithValue("$email", emailKey);
                usage.Parameters.AddWithValue("$orderId", order.Id);
                usage.Parameters.AddWithValue("$discount", discountAmount);
                usage.Parameters.AddWithValue("$usedAt", nowText);
                usage.Parameters.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
                await usage.ExecuteNonQueryAsync();

                await using var bump = connection.CreateCommand();
                bump.Transaction = tx;
                bump.CommandText = """
                    UPDATE Promotions
                    SET UsedCount = (
                        SELECT COUNT(*) FROM PromotionUsages
                        WHERE PromotionId = $id AND COALESCE(Status, 'Redeemed') = 'Redeemed'
                    )
                    WHERE Id = $id;
                    """;
                bump.Parameters.AddWithValue("$id", resolvedPromoId);
                await bump.ExecuteNonQueryAsync();
            }

            await using (var cartLookup = connection.CreateCommand())
            {
                cartLookup.Transaction = tx;
                cartLookup.CommandText = "SELECT Id FROM Carts WHERE UserEmail = $email;";
                cartLookup.Parameters.AddWithValue("$email", emailKey);
                var cartIdObj = await cartLookup.ExecuteScalarAsync();
                if (cartIdObj is not null and not DBNull)
                {
                    var cartId = Convert.ToInt32(cartIdObj);
                    await using var clear = connection.CreateCommand();
                    clear.Transaction = tx;
                    clear.CommandText = "DELETE FROM CartItems WHERE CartId = $cartId;";
                    clear.Parameters.AddWithValue("$cartId", cartId);
                    await clear.ExecuteNonQueryAsync();
                }
            }

            await RecordAuditCoreAsync(connection, tx, userId, "Checkout", "Order", order.Id,
                $"Order {order.Id} placed by {emailKey}", null);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task AppendOrderStatusHistoryAsync(
        string orderId, string? oldStatus, string newStatus, string? notes, string? changedBy)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await AppendOrderStatusHistoryCoreAsync(connection, null, orderId, oldStatus, newStatus, notes, changedBy);
    }

    public async Task<List<OrderStatusHistoryEntry>> GetOrderStatusHistoryAsync(string orderId)
    {
        await EnsureReadyAsync();
        var list = new List<OrderStatusHistoryEntry>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, OrderId, OldStatus, NewStatus, Notes, ChangedBy, CreatedAt
            FROM OrderStatusHistory
            WHERE OrderId = $orderId
            ORDER BY CreatedAt ASC, Id ASC;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new OrderStatusHistoryEntry
            {
                Id = reader.GetInt32(0),
                OrderId = reader.GetString(1),
                OldStatus = reader.IsDBNull(2) ? null : reader.GetString(2),
                NewStatus = reader.GetString(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                ChangedBy = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }

        return list;
    }

    public async Task RecordPromotionUsageAsync(
        string promotionId, string userEmail, string orderId, decimal discountAmount)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PromotionUsages (PromotionId, UserEmail, OrderId, DiscountAmount, UsedAt, Status)
            VALUES ($promoId, $email, $orderId, $discount, $usedAt, 'Redeemed');
            """;
        command.Parameters.AddWithValue("$promoId", promotionId);
        command.Parameters.AddWithValue("$email", userEmail.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("$orderId", orderId);
        command.Parameters.AddWithValue("$discount", discountAmount);
        command.Parameters.AddWithValue("$usedAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();

        await using var bump = connection.CreateCommand();
        bump.CommandText = """
            UPDATE Promotions
            SET UsedCount = (
                SELECT COUNT(*) FROM PromotionUsages
                WHERE PromotionId = $id AND COALESCE(Status, 'Redeemed') = 'Redeemed'
            )
            WHERE Id = $id;
            """;
        bump.Parameters.AddWithValue("$id", promotionId);
        await bump.ExecuteNonQueryAsync();
    }

    public async Task<List<CartItemDto>> GetCartAsync(string email)
    {
        await EnsureReadyAsync();
        var emailKey = email.Trim().ToLowerInvariant();
        var list = new List<CartItemDto>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ci.ProductId, ci.VariantId, ci.Quantity, ci.UnitPrice, ci.SelectedColor, ci.SelectedSize
            FROM CartItems ci
            INNER JOIN Carts c ON c.Id = ci.CartId
            WHERE c.UserEmail = $email
            ORDER BY ci.Id;
            """;
        command.Parameters.AddWithValue("$email", emailKey);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CartItemDto
            {
                ProductId = reader.GetInt32(0),
                VariantId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Quantity = reader.GetInt32(2),
                UnitPrice = reader.GetDecimal(3),
                SelectedColor = reader.IsDBNull(4) ? null : reader.GetString(4),
                SelectedSize = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return list;
    }

    public async Task SaveCartAsync(string email, IEnumerable<CartItemDto> items)
    {
        await EnsureReadyAsync();
        var emailKey = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow.ToString("O");
        await using var connection = await OpenAsync();
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

        int cartId;
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = "SELECT Id FROM Carts WHERE UserEmail = $email;";
            find.Parameters.AddWithValue("$email", emailKey);
            var existing = await find.ExecuteScalarAsync();
            if (existing is not null and not DBNull)
            {
                cartId = Convert.ToInt32(existing);
                await using var touch = connection.CreateCommand();
                touch.Transaction = tx;
                touch.CommandText = "UPDATE Carts SET UpdatedAt = $updatedAt WHERE Id = $id;";
                touch.Parameters.AddWithValue("$updatedAt", now);
                touch.Parameters.AddWithValue("$id", cartId);
                await touch.ExecuteNonQueryAsync();
            }
            else
            {
                await using var create = connection.CreateCommand();
                create.Transaction = tx;
                create.CommandText = """
                    INSERT INTO Carts (UserEmail, CreatedAt, UpdatedAt)
                    VALUES ($email, $createdAt, $updatedAt);
                    SELECT last_insert_rowid();
                    """;
                create.Parameters.AddWithValue("$email", emailKey);
                create.Parameters.AddWithValue("$createdAt", now);
                create.Parameters.AddWithValue("$updatedAt", now);
                cartId = Convert.ToInt32(await create.ExecuteScalarAsync());
            }
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM CartItems WHERE CartId = $cartId;";
            clear.Parameters.AddWithValue("$cartId", cartId);
            await clear.ExecuteNonQueryAsync();
        }

        foreach (var item in items)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO CartItems (
                    CartId, ProductId, VariantId, Quantity, UnitPrice, SelectedColor, SelectedSize, CreatedAt, UpdatedAt
                ) VALUES (
                    $cartId, $productId, $variantId, $quantity, $unitPrice, $color, $size, $createdAt, $updatedAt
                );
                """;
            insert.Parameters.AddWithValue("$cartId", cartId);
            insert.Parameters.AddWithValue("$productId", item.ProductId);
            insert.Parameters.AddWithValue("$variantId", (object?)item.VariantId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$quantity", item.Quantity);
            insert.Parameters.AddWithValue("$unitPrice", item.UnitPrice);
            insert.Parameters.AddWithValue("$color", (object?)item.SelectedColor ?? DBNull.Value);
            insert.Parameters.AddWithValue("$size", (object?)item.SelectedSize ?? DBNull.Value);
            insert.Parameters.AddWithValue("$createdAt", now);
            insert.Parameters.AddWithValue("$updatedAt", now);
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task RecordAuditAsync(
        int? userId, string action, string entityType, string? entityId, string? description, string? ipAddress)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        await RecordAuditCoreAsync(connection, null, userId, action, entityType, entityId, description, ipAddress);
    }

    private static async Task AppendOrderStatusHistoryCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? tx,
        string orderId,
        string? oldStatus,
        string newStatus,
        string? notes,
        string? changedBy)
    {
        await using var command = connection.CreateCommand();
        if (tx is not null) command.Transaction = tx;
        command.CommandText = """
            INSERT INTO OrderStatusHistory (OrderId, OldStatus, NewStatus, Notes, ChangedBy, CreatedAt)
            VALUES ($orderId, $oldStatus, $newStatus, $notes, $changedBy, $createdAt);
            """;
        command.Parameters.AddWithValue("$orderId", orderId);
        command.Parameters.AddWithValue("$oldStatus", (object?)oldStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("$newStatus", newStatus);
        command.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$changedBy", (object?)changedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RecordAuditCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? tx,
        int? userId,
        string action,
        string entityType,
        string? entityId,
        string? description,
        string? ipAddress)
    {
        await using var command = connection.CreateCommand();
        if (tx is not null) command.Transaction = tx;
        command.CommandText = """
            INSERT INTO AuditLogs (UserId, Action, EntityType, EntityId, Description, IpAddress, CreatedAt)
            VALUES ($userId, $action, $entityType, $entityId, $description, $ipAddress, $createdAt);
            """;
        command.Parameters.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$entityType", entityType);
        command.Parameters.AddWithValue("$entityId", (object?)entityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
        command.Parameters.AddWithValue("$ipAddress", (object?)ipAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SyncProductImagesAndInventoryAsync(SqliteConnection connection, Product product)
    {
        var now = DateTime.UtcNow.ToString("O");
        var images = product.GalleryImages.ToList();

        await using (var delete = connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM ProductImages WHERE ProductId = $productId;";
            delete.Parameters.AddWithValue("$productId", product.Id);
            await delete.ExecuteNonQueryAsync();
        }

        for (var i = 0; i < images.Count; i++)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ProductImages (ProductId, ImagePath, AltText, DisplayOrder, IsPrimary, CreatedAt)
                VALUES ($productId, $path, $alt, $order, $primary, $createdAt);
                """;
            insert.Parameters.AddWithValue("$productId", product.Id);
            insert.Parameters.AddWithValue("$path", images[i]);
            insert.Parameters.AddWithValue("$alt", product.Name);
            insert.Parameters.AddWithValue("$order", i);
            insert.Parameters.AddWithValue("$primary", i == 0 ? 1 : 0);
            insert.Parameters.AddWithValue("$createdAt", now);
            await insert.ExecuteNonQueryAsync();
        }

        int? variantId = null;
        await using (var find = connection.CreateCommand())
        {
            find.CommandText = """
                SELECT Id FROM ProductVariants
                WHERE ProductId = $productId AND VariantName = 'Default'
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$productId", product.Id);
            var existing = await find.ExecuteScalarAsync();
            if (existing is not null and not DBNull)
                variantId = Convert.ToInt32(existing);
        }

        if (variantId is null)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ProductVariants (
                    ProductId, Sku, VariantName, Size, Color, AdditionalPrice, StockQuantity,
                    LowStockThreshold, IsActive, CreatedAt, UpdatedAt
                ) VALUES (
                    $productId, $sku, 'Default', NULL, NULL, 0, $stock, 20, 1, $createdAt, $updatedAt
                );
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$productId", product.Id);
            insert.Parameters.AddWithValue("$sku", string.IsNullOrWhiteSpace(product.Sku) ? $"VAR-{product.Id}" : product.Sku);
            insert.Parameters.AddWithValue("$stock", product.Stock);
            insert.Parameters.AddWithValue("$createdAt", now);
            insert.Parameters.AddWithValue("$updatedAt", now);
            variantId = Convert.ToInt32(await insert.ExecuteScalarAsync());
        }
        else
        {
            await using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE ProductVariants
                SET Sku = $sku, StockQuantity = $stock, UpdatedAt = $updatedAt, IsActive = 1
                WHERE Id = $id;
                """;
            update.Parameters.AddWithValue("$sku", string.IsNullOrWhiteSpace(product.Sku) ? $"VAR-{product.Id}" : product.Sku);
            update.Parameters.AddWithValue("$stock", product.Stock);
            update.Parameters.AddWithValue("$updatedAt", now);
            update.Parameters.AddWithValue("$id", variantId.Value);
            await update.ExecuteNonQueryAsync();
        }

        await using (var invFind = connection.CreateCommand())
        {
            invFind.CommandText = """
                SELECT Id FROM Inventory
                WHERE ProductId = $productId AND VariantId = $variantId
                LIMIT 1;
                """;
            invFind.Parameters.AddWithValue("$productId", product.Id);
            invFind.Parameters.AddWithValue("$variantId", variantId.Value);
            var invId = await invFind.ExecuteScalarAsync();
            if (invId is null or DBNull)
            {
                await using var invInsert = connection.CreateCommand();
                invInsert.CommandText = """
                    INSERT INTO Inventory (ProductId, VariantId, QuantityOnHand, QuantityReserved, ReorderLevel, UpdatedAt)
                    VALUES ($productId, $variantId, $qty, 0, 20, $updatedAt);
                    """;
                invInsert.Parameters.AddWithValue("$productId", product.Id);
                invInsert.Parameters.AddWithValue("$variantId", variantId.Value);
                invInsert.Parameters.AddWithValue("$qty", product.Stock);
                invInsert.Parameters.AddWithValue("$updatedAt", now);
                await invInsert.ExecuteNonQueryAsync();
            }
            else
            {
                await using var invUpdate = connection.CreateCommand();
                invUpdate.CommandText = """
                    UPDATE Inventory
                    SET QuantityOnHand = $qty, UpdatedAt = $updatedAt
                    WHERE Id = $id;
                    """;
                invUpdate.Parameters.AddWithValue("$qty", product.Stock);
                invUpdate.Parameters.AddWithValue("$updatedAt", now);
                invUpdate.Parameters.AddWithValue("$id", Convert.ToInt32(invId));
                await invUpdate.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<string> GenerateOrderNumberAsync(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        string prefix = "NUBE";
        await using (var setting = connection.CreateCommand())
        {
            if (tx is not null) setting.Transaction = tx;
            setting.CommandText = "SELECT Value FROM Settings WHERE Key = 'OrderPrefix';";
            var result = await setting.ExecuteScalarAsync();
            if (result is string s && !string.IsNullOrWhiteSpace(s))
                prefix = s.Trim();
        }

        var year = DateTime.Now.Year;
        var pattern = $"{prefix}-{year}-%";
        var maxSeq = 0;
        await using (var maxCmd = connection.CreateCommand())
        {
            if (tx is not null) maxCmd.Transaction = tx;
            maxCmd.CommandText = """
                SELECT Id FROM Orders
                WHERE Id LIKE $pattern
                ORDER BY Id DESC
                LIMIT 1;
                """;
            maxCmd.Parameters.AddWithValue("$pattern", pattern);
            var last = await maxCmd.ExecuteScalarAsync() as string;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var parts = last.Split('-');
                if (parts.Length >= 3 && int.TryParse(parts[^1], out var n))
                    maxSeq = n;
            }
        }

        return $"{prefix}-{year}-{(maxSeq + 1):000000}";
    }

    #endregion

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
    }

    private static int[] DeserializeIntArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [40, 30, 15, 10, 5];
        return JsonSerializer.Deserialize<int[]>(json, JsonOptions) ?? [40, 30, 15, 10, 5];
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS Products (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Category TEXT NOT NULL DEFAULT '',
            ImageUrl TEXT NOT NULL DEFAULT '',
            ImagesJson TEXT NOT NULL DEFAULT '[]',
            Price REAL NOT NULL DEFAULT 0,
            OriginalPrice REAL NULL,
            Rating REAL NOT NULL DEFAULT 0,
            Reviews INTEGER NOT NULL DEFAULT 0,
            Sold INTEGER NOT NULL DEFAULT 0,
            Stock INTEGER NOT NULL DEFAULT 0,
            Badge TEXT NULL,
            ColorsJson TEXT NOT NULL DEFAULT '[]',
            SizesJson TEXT NOT NULL DEFAULT '[]',
            Material TEXT NOT NULL DEFAULT '',
            Sku TEXT NOT NULL DEFAULT '',
            InStock INTEGER NOT NULL DEFAULT 1,
            IsFeatured INTEGER NOT NULL DEFAULT 0,
            IsFreshDrop INTEGER NOT NULL DEFAULT 0,
            IsBestSeller INTEGER NOT NULL DEFAULT 0,
            IsNewArrival INTEGER NOT NULL DEFAULT 0,
            IsFavorite INTEGER NOT NULL DEFAULT 0,
            Description TEXT NOT NULL DEFAULT '',
            FullDescription TEXT NOT NULL DEFAULT '',
            FeaturesJson TEXT NOT NULL DEFAULT '[]',
            RatingBreakdownJson TEXT NOT NULL DEFAULT '[]',
            Section TEXT NOT NULL DEFAULT 'apparel',
            Status TEXT NOT NULL DEFAULT 'Active',
            IsPublished INTEGER NOT NULL DEFAULT 1,
            UpdatedAt TEXT,
            PublishedAt TEXT,
            CreatedBy TEXT,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ProductReviews (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductId INTEGER NOT NULL,
            Author TEXT NOT NULL,
            Initials TEXT NOT NULL,
            Rating INTEGER NOT NULL,
            Date TEXT NOT NULL,
            Comment TEXT NOT NULL,
            FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Categories (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Slug TEXT NOT NULL,
            ImageUrl TEXT NOT NULL DEFAULT '',
            Description TEXT NOT NULL DEFAULT '',
            Status TEXT NOT NULL DEFAULT 'Active',
            ProductCount INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Orders (
            Id TEXT PRIMARY KEY,
            CustomerId TEXT NOT NULL DEFAULT '',
            CustomerName TEXT NOT NULL,
            CustomerEmail TEXT NOT NULL,
            Date TEXT NOT NULL,
            Total REAL NOT NULL,
            PaymentStatus TEXT NOT NULL,
            Fulfillment TEXT NOT NULL,
            Status TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS OrderItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OrderId TEXT NOT NULL,
            ProductId INTEGER NOT NULL,
            Name TEXT NOT NULL,
            ImageUrl TEXT NOT NULL DEFAULT '',
            Quantity INTEGER NOT NULL,
            Price REAL NOT NULL,
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Customers (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Email TEXT NOT NULL UNIQUE,
            Contact TEXT NOT NULL DEFAULT '',
            DateJoined TEXT NOT NULL,
            Status TEXT NOT NULL DEFAULT 'Active',
            TotalOrders INTEGER NOT NULL DEFAULT 0,
            TotalSpent REAL NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Notifications (
            RowId INTEGER PRIMARY KEY AUTOINCREMENT,
            Id TEXT NOT NULL,
            Email TEXT NOT NULL DEFAULT '',
            Title TEXT NOT NULL,
            Message TEXT NOT NULL,
            TimeAgo TEXT NOT NULL DEFAULT '',
            Icon TEXT NOT NULL DEFAULT 'bell',
            Tone TEXT NOT NULL DEFAULT 'blue',
            IsRead INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS AdminNotifications (
            Id TEXT PRIMARY KEY,
            Type TEXT NOT NULL,
            Title TEXT NOT NULL,
            Message TEXT NOT NULL,
            RelatedId TEXT NULL,
            RelatedLabel TEXT NULL,
            RelatedHref TEXT NULL,
            Timestamp TEXT NOT NULL,
            Read INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Staff (
            Id TEXT PRIMARY KEY,
            FirstName TEXT NOT NULL,
            LastName TEXT NOT NULL,
            Email TEXT NOT NULL UNIQUE,
            Role TEXT NOT NULL,
            Status TEXT NOT NULL,
            LastLogin TEXT NOT NULL,
            IsPrimaryAdmin INTEGER NOT NULL DEFAULT 0,
            PermissionsJson TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Promotions (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Code TEXT NOT NULL UNIQUE,
            DiscountType TEXT NOT NULL,
            DiscountValue REAL NOT NULL,
            MinimumOrder REAL NOT NULL DEFAULT 0,
            UsedCount INTEGER NOT NULL DEFAULT 0,
            UsageLimit INTEGER NOT NULL DEFAULT 0,
            StartDate TEXT NOT NULL,
            EndDate TEXT NOT NULL,
            Enabled INTEGER NOT NULL DEFAULT 1,
            Description TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS InventoryHistory (
            Id TEXT PRIMARY KEY,
            ProductId INTEGER NOT NULL,
            ProductName TEXT NOT NULL,
            Type TEXT NOT NULL,
            Quantity INTEGER NOT NULL,
            PreviousStock INTEGER NOT NULL,
            NewStock INTEGER NOT NULL,
            Reason TEXT NOT NULL,
            Notes TEXT NOT NULL DEFAULT '',
            Date TEXT NOT NULL,
            AdminName TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS InventoryMeta (
            ProductId INTEGER PRIMARY KEY,
            Reserved INTEGER NOT NULL DEFAULT 0,
            LowStockLevel INTEGER NOT NULL DEFAULT 20
        );

        CREATE TABLE IF NOT EXISTS Wishlists (
            Email TEXT NOT NULL,
            ProductId INTEGER NOT NULL,
            PRIMARY KEY (Email, ProductId)
        );

        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );
        """;
}
