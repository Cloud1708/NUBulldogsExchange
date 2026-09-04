using Microsoft.Data.Sqlite;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Web.Data;

public sealed partial class DatabaseService
{
    public async Task<PromoValidationResult> ValidatePromotionAsync(PromoValidationRequest request)
    {
        await EnsureReadyAsync();
        await using var connection = await OpenAsync();
        return await ValidatePromotionCoreAsync(connection, null, request);
    }

    public async Task<List<ActivePromotionDto>> GetActivePromotionsAsync()
    {
        await EnsureReadyAsync();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var list = new List<ActivePromotionDto>();
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Code, Name, DiscountType, DiscountValue, MinimumOrder, EndDate, Description
            FROM Promotions p
            WHERE Enabled = 1
              AND date(StartDate) <= date($today)
              AND date(EndDate) >= date($today)
              AND (
                    COALESCE(UsageLimit, 0) <= 0
                    OR (
                        SELECT COUNT(*) FROM PromotionUsages u
                        WHERE u.PromotionId = p.Id
                          AND COALESCE(u.Status, 'Redeemed') = 'Redeemed'
                    ) < COALESCE(UsageLimit, 0)
                  )
            ORDER BY EndDate ASC;
            """;
        command.Parameters.AddWithValue("$today", today);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var type = reader.GetString(2);
            var value = reader.GetDecimal(3);
            list.Add(new ActivePromotionDto
            {
                Code = reader.GetString(0),
                Name = reader.GetString(1),
                DiscountLabel = type.Equals("fixed", StringComparison.OrdinalIgnoreCase)
                    ? $"₱{value:N0}"
                    : $"{value:N0}%",
                MinimumOrder = reader.GetDecimal(4),
                EndDate = DateTime.Parse(reader.GetString(5)),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }

        return list;
    }

    private static async Task<PromoValidationResult> ValidatePromotionCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? tx,
        PromoValidationRequest request)
    {
        var code = (request.Code ?? "").Trim();
        var items = request.Items ?? [];
        var subtotal = items.Sum(i => i.UnitPrice * Math.Max(1, i.Quantity));

        if (string.IsNullOrWhiteSpace(code))
            return Fail("Invalid promo code.", subtotal);

        await using var promoCmd = connection.CreateCommand();
        if (tx is not null) promoCmd.Transaction = tx;
        promoCmd.CommandText = """
            SELECT Id, Name, Code, DiscountType, DiscountValue, MinimumOrder,
                   COALESCE(MaximumDiscount, 0), UsageLimit, COALESCE(UsagePerCustomer, 1),
                   StartDate, EndDate, Enabled
            FROM Promotions
            WHERE UPPER(Code) = UPPER($code)
            LIMIT 1;
            """;
        promoCmd.Parameters.AddWithValue("$code", code);

        string promoId;
        string promoName;
        string promoCode;
        string discountType;
        decimal discountValue;
        decimal minimumOrder;
        decimal maximumDiscount;
        int usageLimit;
        int usagePerCustomer;
        DateTime startDate;
        DateTime endDate;
        bool enabled;

        await using (var reader = await promoCmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
                return Fail("Invalid promo code.", subtotal);

            promoId = reader.GetString(0);
            promoName = reader.GetString(1);
            promoCode = reader.GetString(2);
            discountType = reader.GetString(3);
            discountValue = reader.GetDecimal(4);
            minimumOrder = reader.GetDecimal(5);
            maximumDiscount = reader.GetDecimal(6);
            usageLimit = reader.GetInt32(7);
            usagePerCustomer = reader.GetInt32(8);
            startDate = DateTime.Parse(reader.GetString(9));
            endDate = DateTime.Parse(reader.GetString(10));
            enabled = reader.GetInt32(11) == 1;
        }

        var today = DateTime.Today;
        if (!enabled)
            return Fail("This promotion is currently unavailable.", subtotal);
        if (today < startDate.Date)
            return Fail("This promotion is not active yet.", subtotal);
        if (today > endDate.Date)
            return Fail("This promotion has expired.", subtotal);

        if (subtotal < minimumOrder)
            return Fail($"A minimum order of ₱{minimumOrder:N0} is required to use this promotion.", subtotal);

        var redeemedUses = await CountRedeemedUsagesAsync(connection, tx, promoId, userId: null, email: null);
        if (usageLimit > 0 && redeemedUses >= usageLimit)
            return Fail("This promotion has reached its usage limit.", subtotal);

        var emailKey = string.IsNullOrWhiteSpace(request.UserEmail)
            ? null
            : request.UserEmail.Trim().ToLowerInvariant();
        if (usagePerCustomer > 0 && (request.UserId is > 0 || emailKey is not null))
        {
            var customerUses = await CountRedeemedUsagesAsync(
                connection, tx, promoId, request.UserId, emailKey);
            if (customerUses >= usagePerCustomer)
                return Fail("You have already used this promotion.", subtotal);
        }

        var productIds = await LoadPromotionProductIdsAsync(connection, tx, promoId);
        var categoryIds = await LoadPromotionCategoryIdsAsync(connection, tx, promoId);
        var eligibleSubtotal = await ComputeEligibleSubtotalAsync(
            connection, tx, items, productIds, categoryIds);

        if ((productIds.Count > 0 || categoryIds.Count > 0) && eligibleSubtotal <= 0)
            return Fail("This promotion does not apply to your cart.", subtotal);

        var discount = CalculateDiscountAmount(
            discountType, discountValue, maximumDiscount, eligibleSubtotal);
        if (discount <= 0)
            return Fail("This promotion does not apply to your cart.", subtotal);

        return new PromoValidationResult
        {
            Valid = true,
            Message = $"Promo code {promoCode} applied. You saved ₱{discount:N0}.",
            Code = promoCode,
            PromotionId = promoId,
            PromotionName = promoName,
            DiscountAmount = discount,
            Subtotal = subtotal,
            EligibleSubtotal = eligibleSubtotal,
            Total = Math.Max(0, subtotal - discount)
        };
    }

    private static decimal CalculateDiscountAmount(
        string discountType,
        decimal discountValue,
        decimal maximumDiscount,
        decimal eligibleSubtotal)
    {
        if (eligibleSubtotal <= 0) return 0;

        decimal discount;
        if (discountType.Equals("fixed", StringComparison.OrdinalIgnoreCase))
            discount = discountValue;
        else
            discount = Math.Round(eligibleSubtotal * discountValue / 100m, 2, MidpointRounding.AwayFromZero);

        if (maximumDiscount > 0)
            discount = Math.Min(discount, maximumDiscount);

        return Math.Min(discount, eligibleSubtotal);
    }

    private static async Task<int> CountRedeemedUsagesAsync(
        SqliteConnection connection,
        SqliteTransaction? tx,
        string promotionId,
        int? userId,
        string? email)
    {
        await using var cmd = connection.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;

        if (userId is > 0)
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM PromotionUsages
                WHERE PromotionId = $promoId
                  AND COALESCE(Status, 'Redeemed') = 'Redeemed'
                  AND (UserId = $userId OR lower(UserEmail) = lower($email));
                """;
            cmd.Parameters.AddWithValue("$promoId", promotionId);
            cmd.Parameters.AddWithValue("$userId", userId.Value);
            cmd.Parameters.AddWithValue("$email", email ?? "");
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM PromotionUsages
                WHERE PromotionId = $promoId
                  AND COALESCE(Status, 'Redeemed') = 'Redeemed'
                  AND lower(UserEmail) = lower($email);
                """;
            cmd.Parameters.AddWithValue("$promoId", promotionId);
            cmd.Parameters.AddWithValue("$email", email);
        }
        else
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM PromotionUsages
                WHERE PromotionId = $promoId
                  AND COALESCE(Status, 'Redeemed') = 'Redeemed';
                """;
            cmd.Parameters.AddWithValue("$promoId", promotionId);
        }

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<List<int>> LoadPromotionProductIdsAsync(
        SqliteConnection connection, SqliteTransaction? tx, string promotionId)
    {
        var list = new List<int>();
        await using var cmd = connection.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText = "SELECT ProductId FROM PromotionProducts WHERE PromotionId = $id;";
        cmd.Parameters.AddWithValue("$id", promotionId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetInt32(0));
        return list;
    }

    private static async Task<List<string>> LoadPromotionCategoryIdsAsync(
        SqliteConnection connection, SqliteTransaction? tx, string promotionId)
    {
        var list = new List<string>();
        await using var cmd = connection.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText = "SELECT CategoryId FROM PromotionCategories WHERE PromotionId = $id;";
        cmd.Parameters.AddWithValue("$id", promotionId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));
        return list;
    }

    private static async Task<decimal> ComputeEligibleSubtotalAsync(
        SqliteConnection connection,
        SqliteTransaction? tx,
        List<PromoCartItem> items,
        List<int> productIds,
        List<string> categoryIds)
    {
        if (productIds.Count == 0 && categoryIds.Count == 0)
            return items.Sum(i => i.UnitPrice * Math.Max(1, i.Quantity));

        decimal eligible = 0;
        foreach (var item in items)
        {
            var qty = Math.Max(1, item.Quantity);
            var line = item.UnitPrice * qty;
            var productMatch = productIds.Contains(item.ProductId);
            var categoryMatch = false;

            if (categoryIds.Count > 0)
            {
                await using var catCmd = connection.CreateCommand();
                if (tx is not null) catCmd.Transaction = tx;
                catCmd.CommandText = """
                    SELECT c.Id
                    FROM Products p
                    LEFT JOIN Categories c ON lower(c.Name) = lower(p.Category)
                    WHERE p.Id = $productId
                    LIMIT 1;
                    """;
                catCmd.Parameters.AddWithValue("$productId", item.ProductId);
                var catObj = await catCmd.ExecuteScalarAsync();
                if (catObj is string catId &&
                    categoryIds.Any(id => id.Equals(catId, StringComparison.OrdinalIgnoreCase)))
                    categoryMatch = true;

                if (!categoryMatch && !string.IsNullOrWhiteSpace(item.Category))
                {
                    await using var byName = connection.CreateCommand();
                    if (tx is not null) byName.Transaction = tx;
                    byName.CommandText = "SELECT Id FROM Categories WHERE lower(Name) = lower($name) LIMIT 1;";
                    byName.Parameters.AddWithValue("$name", item.Category.Trim());
                    var nameId = await byName.ExecuteScalarAsync();
                    if (nameId is string nid &&
                        categoryIds.Any(id => id.Equals(nid, StringComparison.OrdinalIgnoreCase)))
                        categoryMatch = true;
                }
            }

            if (productMatch || categoryMatch)
                eligible += line;
        }

        return eligible;
    }

    private static PromoValidationResult Fail(string message, decimal subtotal) => new()
    {
        Valid = false,
        Message = message,
        Subtotal = subtotal,
        Total = subtotal
    };

    private async Task ReversePromotionUsageForOrderAsync(
        SqliteConnection connection, SqliteTransaction? tx, string orderId)
    {
        await using var find = connection.CreateCommand();
        if (tx is not null) find.Transaction = tx;
        find.CommandText = """
            SELECT Id, PromotionId FROM PromotionUsages
            WHERE OrderId = $orderId AND COALESCE(Status, 'Redeemed') = 'Redeemed';
            """;
        find.Parameters.AddWithValue("$orderId", orderId);

        var rows = new List<(int Id, string PromoId)>();
        await using (var reader = await find.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                rows.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        foreach (var (id, promoId) in rows)
        {
            await using var update = connection.CreateCommand();
            if (tx is not null) update.Transaction = tx;
            update.CommandText = "UPDATE PromotionUsages SET Status = 'Reversed' WHERE Id = $id;";
            update.Parameters.AddWithValue("$id", id);
            await update.ExecuteNonQueryAsync();

            await using var bump = connection.CreateCommand();
            if (tx is not null) bump.Transaction = tx;
            bump.CommandText = """
                UPDATE Promotions
                SET UsedCount = (
                    SELECT COUNT(*) FROM PromotionUsages
                    WHERE PromotionId = $promoId AND COALESCE(Status, 'Redeemed') = 'Redeemed'
                )
                WHERE Id = $promoId;
                """;
            bump.Parameters.AddWithValue("$promoId", promoId);
            await bump.ExecuteNonQueryAsync();
        }
    }
}
