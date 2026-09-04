using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;
using NUBulldogsExchange.Web.Web.Components;
using NUBulldogsExchange.Web.Web.Data;
using NUBulldogsExchange.Web.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IAppDatabase>(sp => sp.GetRequiredService<DatabaseService>());

builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AdminProductService>();
builder.Services.AddScoped<AdminCategoryService>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddScoped<AdminSettingsService>();
builder.Services.AddScoped<AdminInventoryService>();
builder.Services.AddScoped<AdminCustomerService>();
builder.Services.AddScoped<AdminStaffService>();
builder.Services.AddScoped<AdminPromotionService>();
builder.Services.AddScoped<AdminReportService>();
builder.Services.AddScoped<AdminNotificationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    await db.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

MapApi(app);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(NUBulldogsExchange.Web.Shared._Imports).Assembly);

app.Run();

static void MapApi(WebApplication app)
{
    var api = app.MapGroup("/api");

    api.MapPost("/auth/register", async (RegisterRequest request, IAppDatabase db) =>
    {
        var result = await db.RegisterCustomerAsync(request);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    });

    api.MapPost("/auth/login", async (LoginRequest request, IAppDatabase db) =>
    {
        var result = await db.LoginAsync(request);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    });

    api.MapPost("/auth/logout", async (HttpRequest http, IAppDatabase db) =>
    {
        var token = ReadSessionToken(http);
        if (!string.IsNullOrWhiteSpace(token))
            await db.LogoutSessionAsync(token);
        return Results.NoContent();
    });

    api.MapGet("/auth/me", async (HttpRequest http, IAppDatabase db) =>
    {
        var token = ReadSessionToken(http);
        if (string.IsNullOrWhiteSpace(token))
            return Results.Unauthorized();
        var result = await db.ValidateSessionAsync(token);
        return result.Success ? Results.Ok(result) : Results.Unauthorized();
    });

    api.MapPut("/auth/profile", async (UpdateProfileRequest body, HttpRequest http, IAppDatabase db) =>
    {
        var token = ReadSessionToken(http);
        if (string.IsNullOrWhiteSpace(token))
            return Results.Unauthorized();
        var result = await db.UpdateCustomerProfileAsync(token, body);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    });

    api.MapPost("/auth/change-password", async (ChangePasswordRequest body, HttpRequest http, IAppDatabase db) =>
    {
        var token = ReadSessionToken(http);
        if (string.IsNullOrWhiteSpace(token))
            return Results.Unauthorized();
        var result = await db.ChangePasswordAsync(token, body);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    });

    api.MapGet("/products", async (IAppDatabase db) =>
        Results.Ok(await db.GetProductsAsync()));

    api.MapGet("/admin/products", async (IAppDatabase db) =>
        Results.Ok(await db.GetProductsAsync()));

    api.MapGet("/catalog/products", async (IAppDatabase db) =>
        Results.Ok(await db.GetPublishedProductsAsync()));

    api.MapGet("/products/{id:int}", async (int id, IAppDatabase db) =>
    {
        var product = await db.GetProductByIdAsync(id);
        return product is null ? Results.NotFound() : Results.Ok(product);
    });

    api.MapPost("/products", async (Product product, IAppDatabase db) =>
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return Results.BadRequest(new { error = "Name is required." });
        var created = await db.UpsertProductAsync(product);
        return Results.Created($"/api/products/{created.Id}", created);
    });

    api.MapPut("/products/{id:int}", async (int id, Product product, IAppDatabase db) =>
    {
        product.Id = id;
        var existing = await db.GetProductByIdAsync(id);
        if (existing is null) return Results.NotFound();
        var updated = await db.UpsertProductAsync(product);
        return Results.Ok(updated);
    });

    api.MapDelete("/products/{id:int}", async (int id, IAppDatabase db) =>
    {
        var deleted = await db.DeleteProductAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    });

    api.MapGet("/categories", async (IAppDatabase db) =>
        Results.Ok(await db.GetCategoriesAsync()));

    api.MapPost("/categories", async (AdminCategory category, IAppDatabase db) =>
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            return Results.BadRequest(new { error = "Name is required." });
        var created = await db.UpsertCategoryAsync(category);
        return Results.Created($"/api/categories/{created.Id}", created);
    });

    api.MapPut("/categories/{id}", async (string id, AdminCategory category, IAppDatabase db) =>
    {
        category.Id = id;
        var updated = await db.UpsertCategoryAsync(category);
        return Results.Ok(updated);
    });

    api.MapDelete("/categories/{id}", async (string id, IAppDatabase db) =>
    {
        var deleted = await db.DeleteCategoryAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    });

    api.MapGet("/orders", async (IAppDatabase db, string? email) =>
    {
        var orders = await db.GetOrdersAsync();
        if (!string.IsNullOrWhiteSpace(email))
            orders = orders.Where(o => o.CustomerEmail.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList();
        return Results.Ok(orders);
    });

    api.MapGet("/orders/{id}", async (string id, IAppDatabase db) =>
    {
        var order = await db.GetOrderByIdAsync(id);
        return order is null ? Results.NotFound() : Results.Ok(order);
    });

    api.MapGet("/orders/{id}/history", async (string id, IAppDatabase db) =>
        Results.Ok(await db.GetOrderStatusHistoryAsync(id)));

    api.MapPost("/orders/{id}/history", async (string id, OrderStatusHistoryEntry entry, IAppDatabase db) =>
    {
        await db.AppendOrderStatusHistoryAsync(id, entry.OldStatus, entry.NewStatus, entry.Notes, entry.ChangedBy);
        return Results.NoContent();
    });

    api.MapPost("/orders", async (AdminOrder order, IAppDatabase db) =>
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            return Results.BadRequest(new { error = "Customer email is required." });
        if (string.IsNullOrWhiteSpace(order.Id))
            order.Id = $"NUBE-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var created = await db.UpsertOrderAsync(order);
        return Results.Created($"/api/orders/{created.Id}", created);
    });

    api.MapPost("/checkout", async (CheckoutRequest request, IAppDatabase db) =>
    {
        if (request.Order is null)
            return Results.BadRequest(new { error = "Order is required." });
        if (string.IsNullOrWhiteSpace(request.UserEmail) &&
            string.IsNullOrWhiteSpace(request.Order.CustomerEmail))
            return Results.BadRequest(new { error = "Customer email is required." });

        try
        {
            var email = string.IsNullOrWhiteSpace(request.UserEmail)
                ? request.Order.CustomerEmail
                : request.UserEmail;
            await db.PlaceCheckoutOrderAsync(request.Order, request.PromoCode, request.DiscountAmount, email);
            return Results.Created($"/api/orders/{request.Order.Id}", request.Order);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    api.MapPut("/orders/{id}", async (string id, AdminOrder order, IAppDatabase db) =>
    {
        order.Id = id;
        var existing = await db.GetOrderByIdAsync(id);
        if (existing is null) return Results.NotFound();
        var updated = await db.UpsertOrderAsync(order);
        return Results.Ok(updated);
    });

    api.MapDelete("/orders/{id}", async (string id, IAppDatabase db) =>
    {
        var deleted = await db.DeleteOrderAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    });

    api.MapGet("/customers", async (IAppDatabase db) =>
        Results.Ok(await db.GetCustomersAsync()));

    api.MapPost("/customers", async (AdminCustomer customer, IAppDatabase db) =>
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
            return Results.BadRequest(new { error = "Email is required." });
        var created = await db.UpsertCustomerAsync(customer);
        return Results.Created($"/api/customers/{created.Id}", created);
    });

    api.MapPut("/customers/{id}", async (string id, AdminCustomer customer, IAppDatabase db) =>
    {
        customer.Id = id;
        var updated = await db.UpsertCustomerAsync(customer);
        return Results.Ok(updated);
    });

    api.MapPut("/customers/{id}/status", async (string id, CustomerStatusRequest body, IAppDatabase db) =>
    {
        var updated = await db.SetCustomerStatusAsync(id, body.Status, body.ActorUserId);
        return updated ? Results.NoContent() : Results.NotFound();
    });

    api.MapGet("/notifications", async (IAppDatabase db, string? email) =>
        Results.Ok(await db.GetCustomerNotificationsAsync(email)));

    api.MapPut("/notifications", async (List<MockNotification> items, IAppDatabase db, string? email) =>
    {
        await db.SaveCustomerNotificationsAsync(items, email);
        return Results.NoContent();
    });

    api.MapGet("/admin/notifications", async (IAppDatabase db) =>
        Results.Ok(await db.GetAdminNotificationsAsync()));

    api.MapPut("/admin/notifications", async (List<AdminNotificationItem> items, IAppDatabase db) =>
    {
        await db.SaveAdminNotificationsAsync(items);
        return Results.NoContent();
    });

    api.MapGet("/promotions", async (IAppDatabase db) =>
        Results.Ok(await db.GetPromotionsAsync()));

    api.MapGet("/promotions/active", async (IAppDatabase db) =>
        Results.Ok(await db.GetActivePromotionsAsync()));

    api.MapPost("/promotions/validate", async (PromoValidationRequest request, IAppDatabase db) =>
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Code))
            return Results.BadRequest(new PromoValidationResult
            {
                Valid = false,
                Message = "Invalid promo code."
            });

        var result = await db.ValidatePromotionAsync(request);
        return Results.Ok(result);
    });

    api.MapPost("/promotions", async (AdminPromotion promo, IAppDatabase db) =>
    {
        if (string.IsNullOrWhiteSpace(promo.Code))
            return Results.BadRequest(new { error = "Code is required." });
        var created = await db.UpsertPromotionAsync(promo);
        return Results.Created($"/api/promotions/{created.Id}", created);
    });

    api.MapPut("/promotions/{id}", async (string id, AdminPromotion promo, IAppDatabase db) =>
    {
        promo.Id = id;
        var updated = await db.UpsertPromotionAsync(promo);
        return Results.Ok(updated);
    });

    api.MapPost("/promotions/{id}/usage", async (string id, PromotionUsageRequest body, IAppDatabase db) =>
    {
        await db.RecordPromotionUsageAsync(id, body.UserEmail, body.OrderId, body.DiscountAmount);
        return Results.NoContent();
    });

    api.MapDelete("/promotions/{id}", async (string id, IAppDatabase db) =>
    {
        var deleted = await db.DeletePromotionAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    });

    api.MapGet("/staff", async (IAppDatabase db) =>
        Results.Ok(await db.GetStaffAsync()));

    api.MapPost("/staff", async (AdminStaffMember staff, IAppDatabase db) =>
    {
        if (string.IsNullOrWhiteSpace(staff.Email))
            return Results.BadRequest(new { error = "Email is required." });
        var created = await db.UpsertStaffAsync(staff);
        return Results.Created($"/api/staff/{created.Id}", created);
    });

    api.MapDelete("/staff/{id}", async (string id, IAppDatabase db) =>
    {
        var deleted = await db.DeleteStaffAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    });

    api.MapGet("/wishlist/{email}", async (string email, IAppDatabase db) =>
        Results.Ok(await db.GetWishlistAsync(email)));

    api.MapPut("/wishlist/{email}", async (string email, List<int> productIds, IAppDatabase db) =>
    {
        await db.SaveWishlistAsync(email, productIds);
        return Results.NoContent();
    });

    api.MapGet("/cart/{email}", async (string email, IAppDatabase db) =>
        Results.Ok(await db.GetCartAsync(email)));

    api.MapPut("/cart/{email}", async (string email, List<CartItemDto> items, IAppDatabase db) =>
    {
        await db.SaveCartAsync(email, items);
        return Results.NoContent();
    });

    api.MapGet("/settings", async (IAppDatabase db) =>
    {
        var keys = new[]
        {
            "StoreName", "StoreEmail", "StorePhone", "PickupLocation", "PickupInstructions",
            "Currency", "LowStockDefaultThreshold", "OrderPrefix", "StoreOpen", "AdminPortalSettings"
        };
        var map = new Dictionary<string, string?>();
        foreach (var key in keys)
            map[key] = await db.GetSettingAsync(key);
        return Results.Ok(map);
    });

    api.MapPut("/settings", async (Dictionary<string, string> values, IAppDatabase db) =>
    {
        foreach (var (key, value) in values)
            await db.SetSettingAsync(key, value);
        return Results.NoContent();
    });

    api.MapGet("/settings/{key}", async (string key, IAppDatabase db) =>
    {
        var value = await db.GetSettingAsync(key);
        return value is null ? Results.NotFound() : Results.Ok(value);
    });

    api.MapPut("/settings/{key}", async (string key, string value, IAppDatabase db) =>
    {
        await db.SetSettingAsync(key, value);
        return Results.NoContent();
    });

    api.MapGet("/stats", async (IAppDatabase db) =>
        Results.Ok(await db.GetDashboardStatsAsync()));

    api.MapGet("/stats/storefront", async (IAppDatabase db) =>
        Results.Ok(await db.GetStorefrontStatsAsync()));

    api.MapGet("/stats/customer-revenue", async (IAppDatabase db) =>
        Results.Ok(await db.GetCompletedOrderRevenueAsync()));

    api.MapGet("/inventory/history", async (IAppDatabase db) =>
        Results.Ok(await db.GetInventoryHistoryAsync()));

    api.MapPost("/inventory/history", async (InventoryHistoryEntry entry, IAppDatabase db) =>
    {
        await db.AddInventoryHistoryAsync(entry);
        return Results.Created("/api/inventory/history", entry);
    });

    api.MapGet("/inventory/reserved", async (IAppDatabase db) =>
        Results.Ok(await db.GetReservedStockAsync()));

    api.MapPut("/inventory/reserved/{productId:int}", async (int productId, InventoryLevelRequest body, IAppDatabase db) =>
    {
        await db.SetReservedStockAsync(productId, body.Value);
        return Results.NoContent();
    });

    api.MapGet("/inventory/low-stock", async (IAppDatabase db) =>
        Results.Ok(await db.GetLowStockLevelsAsync()));

    api.MapPut("/inventory/low-stock/{productId:int}", async (int productId, InventoryLevelRequest body, IAppDatabase db) =>
    {
        await db.SetLowStockLevelAsync(productId, body.Value);
        return Results.NoContent();
    });

    api.MapGet("/products/{id:int}/reviews", async (int id, IAppDatabase db) =>
        Results.Ok(await db.GetProductReviewsAsync(id)));

    api.MapPut("/products/{id:int}/reviews", async (int id, List<ProductReview> reviews, IAppDatabase db) =>
    {
        await db.SaveProductReviewsAsync(id, reviews);
        return Results.NoContent();
    });
}

static string? ReadSessionToken(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    if (!string.IsNullOrWhiteSpace(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return header["Bearer ".Length..].Trim();

    if (request.Headers.TryGetValue("X-Session-Token", out var values))
        return values.ToString();

    return null;
}

file sealed class PromotionUsageRequest
{
    public string UserEmail { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
}

file sealed class CustomerStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public int? ActorUserId { get; set; }
}

file sealed class InventoryLevelRequest
{
    public int Value { get; set; }
}
