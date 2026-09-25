using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

/// <summary>
/// Centralized persistence contract. Web.Web implements this with SQLite;
/// Mobile/Hybrid use an HTTP client implementation against the Web API.
/// </summary>
public interface IAppDatabase
{
    Task InitializeAsync();

    // Products
    Task<List<Product>> GetProductsAsync();
    Task<List<Product>> GetPublishedProductsAsync();
    Task<Product?> GetProductByIdAsync(int id);
    Task<Product> UpsertProductAsync(Product product);
    Task<bool> DeleteProductAsync(int id);
    Task<List<ProductReview>> GetProductReviewsAsync(int productId);
    Task SaveProductReviewsAsync(int productId, IEnumerable<ProductReview> reviews);

    // Product variants (size stock)
    Task<List<ProductVariant>> GetProductVariantsAsync(int productId);
    Task<List<ProductVariant>> GetProductVariantsByProductIdsAsync(IEnumerable<int> productIds);
    Task ReplaceProductVariantsAsync(int productId, IReadOnlyList<ProductVariant> variants);

    // Categories
    Task<List<AdminCategory>> GetCategoriesAsync();
    Task<AdminCategory> UpsertCategoryAsync(AdminCategory category);
    Task<bool> DeleteCategoryAsync(string id);

    // Orders
    Task<List<AdminOrder>> GetOrdersAsync();
    Task<List<AdminOrder>> GetCustomerOrdersAsync(string? email = null);
    Task<AdminOrder?> GetOrderByIdAsync(string id);
    Task<AdminOrder> UpsertOrderAsync(AdminOrder order);
    Task<bool> DeleteOrderAsync(string id);
    Task PlaceCheckoutOrderAsync(AdminOrder order, string? promoCode, decimal discountAmount, string userEmail);
    Task AppendOrderStatusHistoryAsync(string orderId, string? oldStatus, string newStatus, string? notes, string? changedBy);
    Task<List<OrderStatusHistoryEntry>> GetOrderStatusHistoryAsync(string orderId);

    // Saved delivery addresses (public.user_addresses)
    Task<List<UserAddress>> GetUserAddressesAsync(Guid userId);
    Task<UserAddress> UpsertUserAddressAsync(UserAddress address);

    // Authentication
    Task<AuthResult> RegisterCustomerAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task LogoutSessionAsync(string sessionToken);
    Task<AuthResult> ValidateSessionAsync(string sessionToken);
    Task<AuthResult> UpdateCustomerProfileAsync(string sessionToken, UpdateProfileRequest request);
    Task<AuthResult> ChangePasswordAsync(string sessionToken, ChangePasswordRequest request);

    // Customers
    Task<List<AdminCustomer>> GetCustomersAsync();
    Task<AdminCustomer?> GetCustomerByIdAsync(string id);
    Task<AdminCustomer> UpsertCustomerAsync(AdminCustomer customer);
    Task<bool> SetCustomerStatusAsync(string customerId, string status, int? actorUserId);
    Task<decimal> GetCompletedOrderRevenueAsync();

    // Notifications (customer)
    Task<List<MockNotification>> GetCustomerNotificationsAsync(string? email = null);
    Task SaveCustomerNotificationsAsync(IEnumerable<MockNotification> items, string? email = null);
    Task AddCustomerNotificationAsync(string email, string? authUserId, MockNotification notification);

    // Admin notifications
    Task<List<AdminNotificationItem>> GetAdminNotificationsAsync();
    Task SaveAdminNotificationsAsync(IEnumerable<AdminNotificationItem> items);

    // Staff
    Task<List<AdminStaffMember>> GetStaffAsync();
    Task<AdminStaffMember> UpsertStaffAsync(AdminStaffMember staff);
    Task<bool> DeleteStaffAsync(string id);

    // Promotions
    Task<List<AdminPromotion>> GetPromotionsAsync();
    Task<AdminPromotion> UpsertPromotionAsync(AdminPromotion promotion);
    Task<bool> DeletePromotionAsync(string id);
    Task RecordPromotionUsageAsync(string promotionId, string userEmail, string orderId, decimal discountAmount);
    Task<PromoValidationResult> ValidatePromotionAsync(PromoValidationRequest request);
    Task<List<ActivePromotionDto>> GetActivePromotionsAsync();

    // Inventory
    Task<List<InventoryHistoryEntry>> GetInventoryHistoryAsync();
    Task AddInventoryHistoryAsync(InventoryHistoryEntry entry);
    Task<Dictionary<int, int>> GetReservedStockAsync();
    Task SetReservedStockAsync(int productId, int reserved);
    Task<Dictionary<int, int>> GetLowStockLevelsAsync();
    Task SetLowStockLevelAsync(int productId, int level);

    // Cart
    Task<List<CartItemDto>> GetCartAsync(string email);
    Task SaveCartAsync(string email, IEnumerable<CartItemDto> items);

    // Wishlist
    Task<List<int>> GetWishlistAsync(string email);
    Task SaveWishlistAsync(string email, IEnumerable<int> productIds);

    // Settings
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);

    // Stats
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<StorefrontStats> GetStorefrontStatsAsync();
}

public class DashboardStats
{
    public decimal TotalSales { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public List<AdminOrderRow> RecentOrders { get; set; } = [];
    public List<AdminStatusSlice> OrderStatus { get; set; } = [];
    public List<AdminTopProduct> TopProducts { get; set; } = [];
    public List<MonthlySalesPoint> SalesByMonth { get; set; } = [];
}

public class MonthlySalesPoint
{
    public string Month { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class StorefrontStats
{
    public int PublishedProducts { get; set; }
    public int ActiveCustomers { get; set; }
    public int TotalOrders { get; set; }
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? SelectedColor { get; set; }
    public string? SelectedSize { get; set; }
}

public class OrderStatusHistoryEntry
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CheckoutRequest
{
    public AdminOrder Order { get; set; } = new();
    public string? PromoCode { get; set; }
    /// <summary>Ignored by the server — discount is recalculated from PromoCode.</summary>
    public decimal DiscountAmount { get; set; }
    public string UserEmail { get; set; } = string.Empty;
}
