using System.Net.Http.Json;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

/// <summary>
/// HTTP-backed <see cref="IAppDatabase"/> for Mobile / MAUI Hybrid clients.
/// </summary>
public sealed class HttpAppDatabase : IAppDatabase
{
    private readonly HttpClient _http;

    public HttpAppDatabase(HttpClient http)
    {
        _http = http;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<List<Product>> GetProductsAsync() =>
        await _http.GetFromJsonAsync<List<Product>>("api/admin/products") ?? [];

    public async Task<List<Product>> GetPublishedProductsAsync() =>
        await _http.GetFromJsonAsync<List<Product>>("api/catalog/products") ?? [];

    public async Task<Product?> GetProductByIdAsync(int id) =>
        await _http.GetFromJsonAsync<Product>($"api/products/{id}");

    public async Task<Product> UpsertProductAsync(Product product)
    {
        if (product.Id <= 0)
        {
            var response = await _http.PostAsJsonAsync("api/products", product);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<Product>())!;
        }

        var put = await _http.PutAsJsonAsync($"api/products/{product.Id}", product);
        put.EnsureSuccessStatusCode();
        return (await put.Content.ReadFromJsonAsync<Product>())!;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProductReview>> GetProductReviewsAsync(int productId) =>
        await _http.GetFromJsonAsync<List<ProductReview>>($"api/products/{productId}/reviews") ?? [];

    public async Task SaveProductReviewsAsync(int productId, IEnumerable<ProductReview> reviews)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{productId}/reviews", reviews.ToList());
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AdminCategory>> GetCategoriesAsync() =>
        await _http.GetFromJsonAsync<List<AdminCategory>>("api/categories") ?? [];

    public async Task<AdminCategory> UpsertCategoryAsync(AdminCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Id))
        {
            var response = await _http.PostAsJsonAsync("api/categories", category);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AdminCategory>())!;
        }

        var put = await _http.PutAsJsonAsync($"api/categories/{category.Id}", category);
        put.EnsureSuccessStatusCode();
        return (await put.Content.ReadFromJsonAsync<AdminCategory>())!;
    }

    public async Task<bool> DeleteCategoryAsync(string id)
    {
        var response = await _http.DeleteAsync($"api/categories/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<AdminOrder>> GetOrdersAsync() =>
        await _http.GetFromJsonAsync<List<AdminOrder>>("api/orders") ?? [];

    public async Task<AdminOrder?> GetOrderByIdAsync(string id) =>
        await _http.GetFromJsonAsync<AdminOrder>($"api/orders/{id}");

    public async Task<AdminOrder> UpsertOrderAsync(AdminOrder order)
    {
        var existing = string.IsNullOrWhiteSpace(order.Id) ? null : await GetOrderByIdAsync(order.Id);
        if (existing is null)
        {
            var response = await _http.PostAsJsonAsync("api/orders", order);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AdminOrder>())!;
        }

        var put = await _http.PutAsJsonAsync($"api/orders/{order.Id}", order);
        put.EnsureSuccessStatusCode();
        return (await put.Content.ReadFromJsonAsync<AdminOrder>())!;
    }

    public async Task PlaceCheckoutOrderAsync(AdminOrder order, string? promoCode, decimal discountAmount, string userEmail)
    {
        var response = await _http.PostAsJsonAsync("api/checkout", new CheckoutRequest
        {
            Order = order,
            PromoCode = promoCode,
            DiscountAmount = discountAmount,
            UserEmail = userEmail
        });
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<AdminOrder>();
        if (saved is not null)
            order.Id = saved.Id;
    }

    public async Task AppendOrderStatusHistoryAsync(
        string orderId, string? oldStatus, string newStatus, string? notes, string? changedBy)
    {
        var response = await _http.PostAsJsonAsync($"api/orders/{Uri.EscapeDataString(orderId)}/history",
            new OrderStatusHistoryEntry
            {
                OrderId = orderId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Notes = notes,
                ChangedBy = changedBy
            });
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<OrderStatusHistoryEntry>> GetOrderStatusHistoryAsync(string orderId) =>
        await _http.GetFromJsonAsync<List<OrderStatusHistoryEntry>>(
            $"api/orders/{Uri.EscapeDataString(orderId)}/history") ?? [];

    public async Task RecordPromotionUsageAsync(
        string promotionId, string userEmail, string orderId, decimal discountAmount)
    {
        var response = await _http.PostAsJsonAsync($"api/promotions/{Uri.EscapeDataString(promotionId)}/usage",
            new { userEmail, orderId, discountAmount });
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> DeleteOrderAsync(string id)
    {
        var response = await _http.DeleteAsync($"api/orders/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<AdminCustomer>> GetCustomersAsync() =>
        await _http.GetFromJsonAsync<List<AdminCustomer>>("api/customers") ?? [];

    public async Task<AdminCustomer?> GetCustomerByIdAsync(string id) =>
        (await GetCustomersAsync()).FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public async Task<AdminCustomer> UpsertCustomerAsync(AdminCustomer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Id))
        {
            var response = await _http.PostAsJsonAsync("api/customers", customer);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AdminCustomer>())!;
        }

        var put = await _http.PutAsJsonAsync($"api/customers/{customer.Id}", customer);
        put.EnsureSuccessStatusCode();
        return (await put.Content.ReadFromJsonAsync<AdminCustomer>())!;
    }

    public async Task<bool> SetCustomerStatusAsync(string customerId, string status, int? actorUserId)
    {
        var response = await _http.PutAsJsonAsync($"api/customers/{Uri.EscapeDataString(customerId)}/status",
            new { status, actorUserId });
        return response.IsSuccessStatusCode;
    }

    public async Task<decimal> GetCompletedOrderRevenueAsync()
    {
        var value = await _http.GetFromJsonAsync<decimal>("api/stats/customer-revenue");
        return value;
    }

    public async Task<AuthResult> RegisterCustomerAsync(RegisterRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", request);
            return await ReadAuthResultAsync(response, "Unable to create your account. Please try again.");
        }
        catch (HttpRequestException)
        {
            return new AuthResult { Success = false, Error = "Cannot connect to server. Please verify the backend API is running." };
        }
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request);
            return await ReadAuthResultAsync(response, "Invalid email or password.");
        }
        catch (HttpRequestException)
        {
            return new AuthResult { Success = false, Error = "Cannot connect to server. Please verify the backend API is running." };
        }
    }

    public async Task LogoutSessionAsync(string sessionToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
            ApplySession(message, sessionToken);
            await _http.SendAsync(message);
        }
        catch
        {
        }
    }

    public async Task<AuthResult> ValidateSessionAsync(string sessionToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            ApplySession(message, sessionToken);
            var response = await _http.SendAsync(message);
            return await ReadAuthResultAsync(response, "Your session has expired. Please sign in again.");
        }
        catch (HttpRequestException)
        {
            return new AuthResult { Success = false, Error = "Cannot connect to server." };
        }
    }

    public async Task<AuthResult> UpdateCustomerProfileAsync(string sessionToken, UpdateProfileRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Put, "api/auth/profile")
            {
                Content = JsonContent.Create(request)
            };
            ApplySession(message, sessionToken);
            var response = await _http.SendAsync(message);
            return await ReadAuthResultAsync(response, "Unable to update your profile. Please try again.");
        }
        catch (HttpRequestException)
        {
            return new AuthResult { Success = false, Error = "Cannot connect to server." };
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(string sessionToken, ChangePasswordRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password")
            {
                Content = JsonContent.Create(request)
            };
            ApplySession(message, sessionToken);
            var response = await _http.SendAsync(message);
            return await ReadAuthResultAsync(response, "Unable to change your password. Please try again.");
        }
        catch (HttpRequestException)
        {
            return new AuthResult { Success = false, Error = "Cannot connect to server." };
        }
    }

    private static void ApplySession(HttpRequestMessage message, string sessionToken)
    {
        if (!string.IsNullOrWhiteSpace(sessionToken))
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
    }

    private static async Task<AuthResult> ReadAuthResultAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            if (result is not null)
            {
                if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
                    result.Error = fallback;
                return result;
            }
        }
        catch
        {
        }

        return new AuthResult { Success = false, Error = fallback };
    }

    public async Task<List<MockNotification>> GetCustomerNotificationsAsync(string? email = null)
    {
        var url = string.IsNullOrWhiteSpace(email) ? "api/notifications" : $"api/notifications?email={Uri.EscapeDataString(email)}";
        return await _http.GetFromJsonAsync<List<MockNotification>>(url) ?? [];
    }

    public async Task SaveCustomerNotificationsAsync(IEnumerable<MockNotification> items, string? email = null)
    {
        var url = string.IsNullOrWhiteSpace(email) ? "api/notifications" : $"api/notifications?email={Uri.EscapeDataString(email)}";
        var response = await _http.PutAsJsonAsync(url, items.ToList());
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AdminNotificationItem>> GetAdminNotificationsAsync() =>
        await _http.GetFromJsonAsync<List<AdminNotificationItem>>("api/admin/notifications") ?? [];

    public async Task SaveAdminNotificationsAsync(IEnumerable<AdminNotificationItem> items)
    {
        var response = await _http.PutAsJsonAsync("api/admin/notifications", items.ToList());
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AdminStaffMember>> GetStaffAsync() =>
        await _http.GetFromJsonAsync<List<AdminStaffMember>>("api/staff") ?? [];

    public async Task<AdminStaffMember> UpsertStaffAsync(AdminStaffMember staff)
    {
        var response = await _http.PostAsJsonAsync("api/staff", staff);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminStaffMember>())!;
    }

    public async Task<bool> DeleteStaffAsync(string id)
    {
        var response = await _http.DeleteAsync($"api/staff/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<AdminPromotion>> GetPromotionsAsync() =>
        await _http.GetFromJsonAsync<List<AdminPromotion>>("api/promotions") ?? [];

    public async Task<AdminPromotion> UpsertPromotionAsync(AdminPromotion promotion)
    {
        if (string.IsNullOrWhiteSpace(promotion.Id))
        {
            var response = await _http.PostAsJsonAsync("api/promotions", promotion);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AdminPromotion>())!;
        }

        var put = await _http.PutAsJsonAsync($"api/promotions/{promotion.Id}", promotion);
        put.EnsureSuccessStatusCode();
        return (await put.Content.ReadFromJsonAsync<AdminPromotion>())!;
    }

    public async Task<bool> DeletePromotionAsync(string id)
    {
        var response = await _http.DeleteAsync($"api/promotions/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<PromoValidationResult> ValidatePromotionAsync(PromoValidationRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/promotions/validate", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return new PromoValidationResult
            {
                Valid = false,
                Message = string.IsNullOrWhiteSpace(error) ? "Unable to validate promo code." : error
            };
        }

        return await response.Content.ReadFromJsonAsync<PromoValidationResult>()
               ?? new PromoValidationResult { Valid = false, Message = "Unable to validate promo code." };
    }

    public async Task<List<ActivePromotionDto>> GetActivePromotionsAsync() =>
        await _http.GetFromJsonAsync<List<ActivePromotionDto>>("api/promotions/active") ?? [];

    public async Task<List<InventoryHistoryEntry>> GetInventoryHistoryAsync() =>
        await _http.GetFromJsonAsync<List<InventoryHistoryEntry>>("api/inventory/history") ?? [];

    public async Task AddInventoryHistoryAsync(InventoryHistoryEntry entry)
    {
        var response = await _http.PostAsJsonAsync("api/inventory/history", entry);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Dictionary<int, int>> GetReservedStockAsync() =>
        await _http.GetFromJsonAsync<Dictionary<int, int>>("api/inventory/reserved") ?? [];

    public async Task SetReservedStockAsync(int productId, int reserved)
    {
        var response = await _http.PutAsJsonAsync($"api/inventory/reserved/{productId}", new { value = reserved });
        response.EnsureSuccessStatusCode();
    }

    public async Task<Dictionary<int, int>> GetLowStockLevelsAsync() =>
        await _http.GetFromJsonAsync<Dictionary<int, int>>("api/inventory/low-stock") ?? [];

    public async Task SetLowStockLevelAsync(int productId, int level)
    {
        var response = await _http.PutAsJsonAsync($"api/inventory/low-stock/{productId}", new { value = level });
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<CartItemDto>> GetCartAsync(string email) =>
        await _http.GetFromJsonAsync<List<CartItemDto>>($"api/cart/{Uri.EscapeDataString(email)}") ?? [];

    public async Task SaveCartAsync(string email, IEnumerable<CartItemDto> items)
    {
        var response = await _http.PutAsJsonAsync($"api/cart/{Uri.EscapeDataString(email)}", items.ToList());
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<int>> GetWishlistAsync(string email) =>
        await _http.GetFromJsonAsync<List<int>>($"api/wishlist/{Uri.EscapeDataString(email)}") ?? [];

    public async Task SaveWishlistAsync(string email, IEnumerable<int> productIds)
    {
        var response = await _http.PutAsJsonAsync($"api/wishlist/{Uri.EscapeDataString(email)}", productIds.ToList());
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetSettingAsync(string key) =>
        await _http.GetFromJsonAsync<string>($"api/settings/{Uri.EscapeDataString(key)}");

    public async Task SetSettingAsync(string key, string value)
    {
        var response = await _http.PutAsJsonAsync($"api/settings/{Uri.EscapeDataString(key)}", value);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DashboardStats> GetDashboardStatsAsync() =>
        await _http.GetFromJsonAsync<DashboardStats>("api/stats") ?? new DashboardStats();

    public async Task<StorefrontStats> GetStorefrontStatsAsync() =>
        await _http.GetFromJsonAsync<StorefrontStats>("api/stats/storefront") ?? new StorefrontStats();
}
