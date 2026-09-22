using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

/// <summary>
/// Direct Supabase implementation of the existing IAppDatabase contract.
/// Uses Supabase Auth + PostgREST; no PHP API is required.
///
/// IMPORTANT:
/// - Use only the publishable/anon key in Web/MAUI clients.
/// - Never place a service_role key in this class or in the app.
/// - Run 01_supabase_direct_hardening.sql before using checkout/profile updates.
/// </summary>
public sealed class SupabaseAppDatabase : IAppDatabase
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;
    private readonly SupabaseSessionState _session;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SupabaseAppDatabase(
        HttpClient http,
        SupabaseOptions options,
        SupabaseSessionState session)
    {
        _http = http;
        _options = options;
        _session = session;

        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_options.Url);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // ========================================================
    // Products
    // ========================================================

    public Task<List<Product>> GetProductsAsync() =>
        GetListAsync<Product>("rest/v1/products?select=*&order=id.asc");

    public Task<List<Product>> GetPublishedProductsAsync() =>
        GetListAsync<Product>(
            "rest/v1/products?select=*&is_published=eq.true&status=eq.Active&order=id.asc");

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var rows = await GetListAsync<Product>(
            $"rest/v1/products?select=*&id=eq.{id}&limit=1");
        return rows.FirstOrDefault();
    }

    public async Task<Product> UpsertProductAsync(Product product)
    {
        var body = ProductPayload(product);

        if (product.Id <= 0)
        {
            var rows = await SendForListAsync<Product>(
                HttpMethod.Post,
                "rest/v1/products",
                body,
                "return=representation");
            return rows.First();
        }

        var updated = await SendForListAsync<Product>(
            HttpMethod.Patch,
            $"rest/v1/products?id=eq.{product.Id}",
            body,
            "return=representation");

        return updated.FirstOrDefault() ?? product;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"rest/v1/products?id=eq.{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProductReview>> GetProductReviewsAsync(int productId)
    {
        var rows = await GetListAsync<ProductReviewRow>(
            $"rest/v1/product_reviews?select=*&product_id=eq.{productId}&order=review_date.desc");

        return rows.Select(r => new ProductReview
        {
            Author = r.Author,
            Initials = r.Initials,
            Rating = r.Rating,
            Date = r.ReviewDate,
            Comment = r.Comment
        }).ToList();
    }

    public async Task SaveProductReviewsAsync(int productId, IEnumerable<ProductReview> reviews)
    {
        using (var delete = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/product_reviews?product_id=eq.{productId}"))
        {
            await EnsureSuccessAsync(delete);
        }

        var rows = reviews.Select(r => new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["author"] = r.Author,
            ["initials"] = r.Initials,
            ["rating"] = r.Rating,
            ["review_date"] = r.Date == default ? DateTime.UtcNow.Date : r.Date.Date,
            ["comment"] = r.Comment
        }).ToList();

        if (rows.Count == 0) return;

        using var insert = await SendAsync(
            HttpMethod.Post,
            "rest/v1/product_reviews",
            rows);
        await EnsureSuccessAsync(insert);
    }

    // ========================================================
    // Product variants
    // ========================================================

    public async Task<List<ProductVariant>> GetProductVariantsAsync(int productId)
    {
        return await GetListAsync<ProductVariant>(
            $"rest/v1/product_variants?select=*&product_id=eq.{productId}&order=id.asc");
    }

    public async Task<List<ProductVariant>> GetProductVariantsByProductIdsAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.Distinct().Where(id => id > 0).ToList();
        if (ids.Count == 0)
            return [];

        var inList = string.Join(",", ids);
        return await GetListAsync<ProductVariant>(
            $"rest/v1/product_variants?select=*&product_id=in.({inList})&order=id.asc");
    }

    public async Task ReplaceProductVariantsAsync(int productId, IReadOnlyList<ProductVariant> variants)
    {
        using (var delete = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/product_variants?product_id=eq.{productId}"))
        {
            await EnsureSuccessAsync(delete);
        }

        if (variants.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var rows = variants.Select(v => new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["size"] = v.Size.Trim(),
            ["sku"] = string.IsNullOrWhiteSpace(v.Sku) ? null : v.Sku.Trim(),
            ["stock_quantity"] = Math.Max(0, v.StockQuantity),
            ["price_adjustment"] = v.PriceAdjustment,
            ["status"] = string.IsNullOrWhiteSpace(v.Status) ? "Active" : v.Status.Trim(),
            ["created_at"] = v.CreatedAt ?? now,
            ["updated_at"] = now
        }).ToList();

        using var insert = await SendAsync(
            HttpMethod.Post,
            "rest/v1/product_variants",
            rows,
            "return=minimal");
        await EnsureSuccessAsync(insert);
    }

    // ========================================================
    // Categories
    // ========================================================

    public Task<List<AdminCategory>> GetCategoriesAsync() =>
        GetListAsync<AdminCategory>(
            "rest/v1/categories?select=*&order=display_order.asc,name.asc");

    public async Task<AdminCategory> UpsertCategoryAsync(AdminCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Slug))
            category.Slug = AdminCategory.ToSlug(category.Name);

        if (string.IsNullOrWhiteSpace(category.Id))
        {
            var generatedId = $"cat-{category.Slug}-{Guid.NewGuid():N}";
            category.Id = generatedId.Length <= 100 ? generatedId : generatedId[..100];
        }

        var body = new Dictionary<string, object?>
        {
            ["id"] = category.Id,
            ["name"] = category.Name,
            ["slug"] = category.Slug,
            ["image_url"] = category.ImageUrl,
            ["description"] = category.Description,
            ["status"] = category.Status,
            ["product_count"] = category.ProductCount,
            ["is_active"] = category.IsActive
        };

        var rows = await SendForListAsync<AdminCategory>(
            HttpMethod.Post,
            "rest/v1/categories?on_conflict=id",
            body,
            "resolution=merge-duplicates,return=representation");

        return rows.FirstOrDefault() ?? category;
    }

    public async Task<bool> DeleteCategoryAsync(string id)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"rest/v1/categories?id=eq.{Esc(id)}");
        return response.IsSuccessStatusCode;
    }

    // ========================================================
    // Orders
    // ========================================================

    public async Task<List<AdminOrder>> GetOrdersAsync()
    {
        var orders = await GetListAsync<AdminOrder>(
            "rest/v1/orders?select=*&order=date.desc");

        foreach (var order in orders)
            order.Items = await GetOrderItemsAsync(order.Id);

        return orders;
    }

    public async Task<AdminOrder?> GetOrderByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var rows = await GetListAsync<AdminOrder>(
            $"rest/v1/orders?select=*&id=eq.{Esc(id)}&limit=1");
        var order = rows.FirstOrDefault();
        if (order is null) return null;

        order.Items = await GetOrderItemsAsync(order.Id);
        return order;
    }

    public async Task<AdminOrder> UpsertOrderAsync(AdminOrder order)
    {
        var existing = string.IsNullOrWhiteSpace(order.Id)
            ? null
            : await GetOrderByIdAsync(order.Id);

        // Existing customer UI calls UpsertOrderAsync when cancelling.
        // Use the secure RPC instead of granting customers general UPDATE access.
        if (existing is not null &&
            order.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) &&
            !existing.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            await SendForSingleAsync<CancelOrderRpcResult>(
                HttpMethod.Post,
                "rest/v1/rpc/cancel_order",
                new { p_order_id = order.Id });

            return await GetOrderByIdAsync(order.Id) ?? order;
        }

        if (existing is not null)
        {
            // Do not send auth_user_id/customer ownership fields when an admin
            // changes status/payment/fulfillment. This prevents ownership changes.
            var updateBody = new Dictionary<string, object?>
            {
                ["customer_name"] = order.CustomerName,
                ["customer_email"] = order.CustomerEmail,
                ["subtotal"] = order.Subtotal,
                ["discount_amount"] = order.DiscountAmount,
                ["total"] = order.Total,
                ["promotion_id"] = order.PromotionId,
                ["promotion_code"] = order.PromotionCode,
                ["payment_status"] = order.PaymentStatus,
                ["fulfillment"] = order.Fulfillment,
                ["status"] = order.Status
            };

            var updated = await SendForListAsync<AdminOrder>(
                HttpMethod.Patch,
                $"rest/v1/orders?id=eq.{Esc(order.Id)}",
                updateBody,
                "return=representation");

            var savedExisting = updated.FirstOrDefault() ?? order;
            savedExisting.Items = await GetOrderItemsAsync(savedExisting.Id);
            return savedExisting;
        }

        if (string.IsNullOrWhiteSpace(order.Id))
            order.Id = GenerateOrderNumber();
        if (order.Date == default)
            order.Date = DateTime.UtcNow;

        var rows = await SendForListAsync<AdminOrder>(
            HttpMethod.Post,
            "rest/v1/orders",
            OrderPayload(order),
            "return=representation");

        if (order.Items.Count > 0)
        {
            var itemRows = order.Items.Select(i => new Dictionary<string, object?>
            {
                ["order_id"] = order.Id,
                ["product_id"] = i.ProductId,
                ["name"] = i.Name,
                ["image_url"] = i.ImageUrl,
                ["quantity"] = i.Quantity,
                ["price"] = i.Price
            }).ToList();

            using var insert = await SendAsync(
                HttpMethod.Post,
                "rest/v1/order_items",
                itemRows);
            await EnsureSuccessAsync(insert);
        }

        var saved = rows.FirstOrDefault() ?? order;
        saved.Items = await GetOrderItemsAsync(saved.Id);
        return saved;
    }

    public async Task<bool> DeleteOrderAsync(string id)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"rest/v1/orders?id=eq.{Esc(id)}");
        return response.IsSuccessStatusCode;
    }

    public async Task PlaceCheckoutOrderAsync(
        AdminOrder order,
        string? promoCode,
        decimal discountAmount,
        string userEmail)
    {
        RequireAuth();

        // Server/database calculates prices and discounts; client discount is ignored.
        _ = discountAmount;
        _ = userEmail;

        var rpcBody = new Dictionary<string, object?>
        {
            ["p_order"] = new Dictionary<string, object?>
            {
                ["id"] = string.IsNullOrWhiteSpace(order.Id) ? null : order.Id,
                ["fulfillment"] = string.IsNullOrWhiteSpace(order.Fulfillment)
                    ? "Campus Pickup"
                    : order.Fulfillment
            },
            ["p_items"] = order.Items.Select(i => new Dictionary<string, object?>
            {
                ["product_id"] = i.ProductId,
                ["quantity"] = i.Quantity,
                ["selected_color"] = null,
                ["selected_size"] = string.IsNullOrWhiteSpace(i.Size) ? null : i.Size.Trim(),
                ["variant_id"] = i.VariantId
            }).ToList(),
            ["p_promo_code"] = string.IsNullOrWhiteSpace(promoCode) ? null : promoCode.Trim()
        };

        var result = await SendForSingleAsync<CheckoutRpcResult>(
            HttpMethod.Post,
            "rest/v1/rpc/place_checkout_order",
            rpcBody);

        if (result is null || string.IsNullOrWhiteSpace(result.Id))
            throw new InvalidOperationException("Supabase did not return the created order number.");

        order.Id = result.Id;
        order.Subtotal = result.Subtotal;
        order.DiscountAmount = result.DiscountAmount;
        order.Total = result.Total;
        order.PromotionId = result.PromotionId;
        order.PromotionCode = result.PromotionCode;
        order.Status = result.Status ?? "Pending";
        order.PaymentStatus = result.PaymentStatus ?? "Pending";
        order.Date = result.Date ?? DateTime.UtcNow;
        order.CustomerEmail = _session.Email ?? order.CustomerEmail;
        order.CustomerId = _session.AuthUserId ?? order.CustomerId;
    }

    public async Task AppendOrderStatusHistoryAsync(
        string orderId,
        string? oldStatus,
        string newStatus,
        string? notes,
        string? changedBy)
    {
        var body = new Dictionary<string, object?>
        {
            ["order_id"] = orderId,
            ["old_status"] = oldStatus,
            ["new_status"] = newStatus,
            ["notes"] = notes,
            ["changed_by"] = changedBy,
            ["created_at"] = DateTime.UtcNow
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            "rest/v1/order_status_history",
            body);
        await EnsureSuccessAsync(response);
    }

    public Task<List<OrderStatusHistoryEntry>> GetOrderStatusHistoryAsync(string orderId) =>
        GetListAsync<OrderStatusHistoryEntry>(
            $"rest/v1/order_status_history?select=*&order_id=eq.{Esc(orderId)}&order=created_at.asc");

    // ========================================================
    // Authentication
    // ========================================================

    public async Task<AuthResult> RegisterCustomerAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
            return Fail("First name and last name are required.");

        if (!AuthValidation.IsValidEmail(request.Email))
            return Fail("Enter a valid email address.");

        if (!AuthValidation.IsValidPhone(request.PhoneNumber))
            return Fail("Enter a valid phone number.");

        if (string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < AuthValidation.MinPasswordLength)
            return Fail($"Password must be at least {AuthValidation.MinPasswordLength} characters.");

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return Fail("Passwords do not match.");

        var body = new
        {
            email = AuthValidation.NormalizeEmail(request.Email),
            password = request.Password,
            data = new
            {
                first_name = request.FirstName.Trim(),
                last_name = request.LastName.Trim(),
                phone_number = request.PhoneNumber.Trim()
            }
        };

        try
        {
            var auth = await SendAuthAsync(
                HttpMethod.Post,
                "auth/v1/signup",
                body);

            if (auth.User is null)
                return Fail("Supabase did not return the registered user.");

            // If email confirmation is enabled, Supabase may create the account
            // without returning an access token.
            if (string.IsNullOrWhiteSpace(auth.AccessToken))
            {
                return new AuthResult
                {
                    Success = true,
                    Error = "Account created. Verify your email, then sign in."
                };
            }

            _session.Set(
                auth.AccessToken,
                auth.RefreshToken,
                auth.User.Id,
                auth.User.Email);

            var user = await BuildMockUserAsync(auth.User.Id, auth.AccessToken);
            if (user is null)
                user = BuildFallbackUser(auth.User, request.FirstName, request.LastName, request.PhoneNumber);

            user.SessionToken = auth.AccessToken;

            return new AuthResult
            {
                Success = true,
                User = user,
                SessionToken = auth.AccessToken
            };
        }
        catch (Exception ex)
        {
            return Fail(CleanAuthError(ex.Message, "Unable to create your account."));
        }
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        if (!AuthValidation.IsValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Fail("Invalid email or password.");

        try
        {
            var auth = await SendAuthAsync(
                HttpMethod.Post,
                "auth/v1/token?grant_type=password",
                new
                {
                    email = AuthValidation.NormalizeEmail(request.Email),
                    password = request.Password
                });

            if (auth.User is null || string.IsNullOrWhiteSpace(auth.AccessToken))
                return Fail("Invalid email or password.");

            _session.Set(
                auth.AccessToken,
                auth.RefreshToken,
                auth.User.Id,
                auth.User.Email);

            var user = await BuildMockUserAsync(auth.User.Id, auth.AccessToken);
            if (user is null)
            {
                await LogoutSessionAsync(auth.AccessToken);
                return Fail("User account was not found.");
            }

            if (!user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                await LogoutSessionAsync(auth.AccessToken);
                return Fail("Your account is currently inactive. Please contact the administrator.");
            }

            user.RememberMe = request.RememberMe;
            user.SessionToken = auth.AccessToken;

            return new AuthResult
            {
                Success = true,
                User = user,
                SessionToken = auth.AccessToken
            };
        }
        catch (Exception ex)
        {
            return Fail(CleanAuthError(ex.Message, "Invalid email or password."));
        }
    }

    public async Task LogoutSessionAsync(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            _session.Clear();
            return;
        }

        try
        {
            using var response = await SendAsync(
                HttpMethod.Post,
                "auth/v1/logout",
                bearerOverride: sessionToken);
            // Sign-out may already be invalid; local session must still be cleared.
            _ = response.StatusCode;
        }
        finally
        {
            _session.Clear();
        }
    }

    public async Task<AuthResult> ValidateSessionAsync(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            return Fail("Your session has expired. Please sign in again.");

        try
        {
            var authUser = await GetAuthUserAsync(sessionToken);
            if (authUser is null || string.IsNullOrWhiteSpace(authUser.Id))
                return Fail("Your session has expired. Please sign in again.");

            _session.SetFromAccessToken(sessionToken, authUser.Id, authUser.Email);

            var user = await BuildMockUserAsync(authUser.Id, sessionToken);
            if (user is null)
            {
                await LogoutSessionAsync(sessionToken);
                return Fail("User account was not found.");
            }

            if (!user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                await LogoutSessionAsync(sessionToken);
                return Fail("Your account is currently inactive. Please contact the administrator.");
            }

            user.SessionToken = sessionToken;
            return new AuthResult
            {
                Success = true,
                User = user,
                SessionToken = sessionToken
            };
        }
        catch
        {
            _session.Clear();
            return Fail("Your session has expired. Please sign in again.");
        }
    }

    public async Task<AuthResult> UpdateCustomerProfileAsync(
        string sessionToken,
        UpdateProfileRequest request)
    {
        try
        {
            var authUser = await GetAuthUserAsync(sessionToken);
            if (authUser is null) return Fail("Your session has expired.");

            _session.SetFromAccessToken(sessionToken, authUser.Id, authUser.Email);

            var body = new Dictionary<string, object?>
            {
                ["first_name"] = request.FirstName.Trim(),
                ["last_name"] = request.LastName.Trim(),
                ["phone_number"] = request.PhoneNumber.Trim(),
                ["profile_image"] = request.ProfileImage,
                ["student_id"] = request.StudentId,
                ["college"] = request.College,
                ["address"] = request.Address
            };

            var rows = await SendForListAsync<UserRow>(
                HttpMethod.Patch,
                $"rest/v1/users?id=eq.{Esc(authUser.Id)}",
                body,
                "return=representation",
                bearerOverride: sessionToken);

            if (rows.Count == 0)
                return Fail("Profile was not updated.");

            var user = await BuildMockUserAsync(authUser.Id, sessionToken);
            if (user is null)
                return Fail("Profile was updated but could not be reloaded.");

            user.SessionToken = sessionToken;

            return new AuthResult
            {
                Success = true,
                User = user,
                SessionToken = sessionToken
            };
        }
        catch (Exception ex)
        {
            return Fail(CleanAuthError(ex.Message, "Unable to update your profile."));
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(
        string sessionToken,
        ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return Fail("Current password is required.");
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < AuthValidation.MinPasswordLength)
            return Fail($"New password must be at least {AuthValidation.MinPasswordLength} characters.");
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
            return Fail("New passwords do not match.");

        try
        {
            var current = await GetAuthUserAsync(sessionToken);
            if (current is null || string.IsNullOrWhiteSpace(current.Email))
                return Fail("Your session has expired.");

            // Reauthenticate so the CurrentPassword field is actually verified.
            var reauth = await SendAuthAsync(
                HttpMethod.Post,
                "auth/v1/token?grant_type=password",
                new { email = current.Email, password = request.CurrentPassword });

            if (string.IsNullOrWhiteSpace(reauth.AccessToken) || reauth.User is null)
                return Fail("Current password is incorrect.");

            using var response = await SendAsync(
                HttpMethod.Put,
                "auth/v1/user",
                new { password = request.NewPassword },
                bearerOverride: reauth.AccessToken);
            await EnsureSuccessAsync(response);

            _session.Set(
                reauth.AccessToken,
                reauth.RefreshToken,
                reauth.User.Id,
                reauth.User.Email);

            var user = await BuildMockUserAsync(reauth.User.Id, reauth.AccessToken);
            if (user is not null)
                user.SessionToken = reauth.AccessToken;

            return new AuthResult
            {
                Success = true,
                User = user,
                SessionToken = reauth.AccessToken
            };
        }
        catch (Exception ex)
        {
            return Fail(CleanAuthError(ex.Message, "Unable to change your password."));
        }
    }

    // ========================================================
    // Customers
    // ========================================================

    public async Task<List<AdminCustomer>> GetCustomersAsync()
    {
        var profiles = await GetListAsync<UserWithRoleRow>(
            "rest/v1/users_with_roles?select=*&role=eq.Customer&order=created_at.desc");

        var orderRows = await GetListAsync<OrderAggregateRow>(
            "rest/v1/orders?select=auth_user_id,total,status");

        return profiles.Select(p =>
        {
            var customerOrders = orderRows
                .Where(o => string.Equals(o.AuthUserId, p.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new AdminCustomer
            {
                Id = p.Id,
                Name = $"{p.FirstName} {p.LastName}".Trim(),
                Email = p.Email ?? string.Empty,
                Contact = p.PhoneNumber ?? string.Empty,
                DateJoined = p.CreatedAt,
                LastLoginAt = null,
                Status = p.Status,
                TotalOrders = customerOrders.Count,
                TotalSpent = customerOrders
                    .Where(o => !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                    .Sum(o => o.Total)
            };
        }).ToList();
    }

    public async Task<AdminCustomer?> GetCustomerByIdAsync(string id)
    {
        var customers = await GetCustomersAsync();
        return customers.FirstOrDefault(c =>
            c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AdminCustomer> UpsertCustomerAsync(AdminCustomer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Id))
        {
            throw new InvalidOperationException(
                "Creating a new login account is a privileged Supabase Auth operation. " +
                "Create the user in Supabase Authentication (or a secure Edge Function) first, " +
                "then edit the profile from the Admin side.");
        }

        var parts = customer.Name.Trim()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        var body = new Dictionary<string, object?>
        {
            ["first_name"] = parts.Length > 0 ? parts[0] : "",
            ["last_name"] = parts.Length > 1 ? parts[1] : "",
            ["phone_number"] = customer.Contact,
            ["status"] = customer.Status
        };

        await SendForListAsync<UserRow>(
            HttpMethod.Patch,
            $"rest/v1/users?id=eq.{Esc(customer.Id)}",
            body,
            "return=representation");

        return await GetCustomerByIdAsync(customer.Id) ?? customer;
    }

    public async Task<bool> SetCustomerStatusAsync(
        string customerId,
        string status,
        int? actorUserId)
    {
        _ = actorUserId;
        var rows = await SendForListAsync<UserRow>(
            HttpMethod.Patch,
            $"rest/v1/users?id=eq.{Esc(customerId)}",
            new { status },
            "return=representation");
        return rows.Count > 0;
    }

    public async Task<decimal> GetCompletedOrderRevenueAsync()
    {
        var rows = await GetListAsync<OrderAggregateRow>(
            "rest/v1/orders?select=auth_user_id,total,status&status=eq.Completed");
        return rows.Sum(o => o.Total);
    }

    // ========================================================
    // Customer notifications
    // ========================================================

    public async Task<List<MockNotification>> GetCustomerNotificationsAsync(string? email = null)
    {
        _ = email;
        if (!_session.IsAuthenticated || string.IsNullOrWhiteSpace(_session.AuthUserId))
            return [];

        var rows = await GetListAsync<CustomerNotificationRow>(
            $"rest/v1/notifications?select=*&auth_user_id=eq.{Esc(_session.AuthUserId)}&order=created_at.desc");

        return rows.Select(r => new MockNotification
        {
            Id = r.Id,
            Title = r.Title,
            Message = r.Message,
            TimeAgo = r.TimeAgo,
            Icon = r.Icon,
            Tone = r.Tone,
            IsRead = r.IsRead
        }).ToList();
    }

    public async Task SaveCustomerNotificationsAsync(
        IEnumerable<MockNotification> items,
        string? email = null)
    {
        RequireAuth();

        var uid = _session.AuthUserId!;
        var resolvedEmail = _session.Email ?? email ?? string.Empty;

        using (var delete = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/notifications?auth_user_id=eq.{Esc(uid)}"))
        {
            await EnsureSuccessAsync(delete);
        }

        var rows = items.Select(n => new Dictionary<string, object?>
        {
            ["id"] = string.IsNullOrWhiteSpace(n.Id) ? Guid.NewGuid().ToString("N") : n.Id,
            ["auth_user_id"] = uid,
            ["email"] = resolvedEmail,
            ["title"] = n.Title,
            ["message"] = n.Message,
            ["time_ago"] = n.TimeAgo,
            ["icon"] = n.Icon,
            ["tone"] = n.Tone,
            ["is_read"] = n.IsRead,
            ["created_at"] = DateTime.UtcNow
        }).ToList();

        if (rows.Count == 0) return;

        using var insert = await SendAsync(
            HttpMethod.Post,
            "rest/v1/notifications",
            rows);
        await EnsureSuccessAsync(insert);
    }

    // ========================================================
    // Admin notifications
    // ========================================================

    public async Task<List<AdminNotificationItem>> GetAdminNotificationsAsync()
    {
        var rows = await GetListAsync<AdminNotificationRow>(
            "rest/v1/admin_notifications?select=*&order=timestamp.desc");

        return rows.Select(r => new AdminNotificationItem
        {
            Id = r.Id,
            Type = r.Type,
            Title = r.Title,
            Message = r.Message,
            RelatedId = r.RelatedId,
            RelatedLabel = r.RelatedLabel,
            RelatedHref = r.RelatedHref,
            Timestamp = r.Timestamp,
            Read = r.IsRead
        }).ToList();
    }

    public async Task SaveAdminNotificationsAsync(IEnumerable<AdminNotificationItem> items)
    {
        using (var delete = await SendAsync(
                   HttpMethod.Delete,
                   "rest/v1/admin_notifications?id=not.is.null"))
        {
            await EnsureSuccessAsync(delete);
        }

        var rows = items.Select(n => new Dictionary<string, object?>
        {
            ["id"] = string.IsNullOrWhiteSpace(n.Id) ? Guid.NewGuid().ToString("N") : n.Id,
            ["type"] = n.Type,
            ["title"] = n.Title,
            ["message"] = n.Message,
            ["related_id"] = n.RelatedId,
            ["related_label"] = n.RelatedLabel,
            ["related_href"] = n.RelatedHref,
            ["timestamp"] = n.Timestamp == default ? DateTime.UtcNow : n.Timestamp,
            ["is_read"] = n.Read
        }).ToList();

        if (rows.Count == 0) return;

        using var insert = await SendAsync(
            HttpMethod.Post,
            "rest/v1/admin_notifications",
            rows);
        await EnsureSuccessAsync(insert);
    }

    // ========================================================
    // Staff
    // ========================================================

    public async Task<List<AdminStaffMember>> GetStaffAsync()
    {
        var rows = await GetListAsync<UserWithRoleRow>(
            "rest/v1/users_with_roles?select=*&role=in.(Admin,Staff)&order=last_name.asc,first_name.asc");

        return rows.Select(UserRowToStaffMember).ToList();
    }

    public async Task<AdminStaffMember> UpsertStaffAsync(AdminStaffMember staff)
    {
        // public.users.id must always be the same UUID as auth.users.id.
        // For a new staff entry, locate an already-registered Supabase Auth user by email.
        UserWithRoleRow? existing = null;

        if (Guid.TryParse(staff.Id, out _))
        {
            existing = (await GetListAsync<UserWithRoleRow>(
                $"rest/v1/users_with_roles?select=*&id=eq.{Esc(staff.Id)}&limit=1"))
                .FirstOrDefault();
        }

        if (existing is null && !string.IsNullOrWhiteSpace(staff.Email))
        {
            existing = (await GetListAsync<UserWithRoleRow>(
                $"rest/v1/users_with_roles?select=*&email=eq.{Esc(staff.Email.Trim())}&limit=1"))
                .FirstOrDefault();
        }

        if (existing is null)
        {
            throw new InvalidOperationException(
                "No registered Supabase account was found for this email. " +
                "Create/register the user first, then assign the Admin or Staff role.");
        }

        // Keep the in-memory object in sync because AdminStaffService currently
        // ignores the returned value from UpsertStaffAsync.
        staff.Id = existing.Id;
        staff.Email = existing.Email ?? staff.Email;

        var roleId = staff.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        var permissions = roleId == 1
            ? AdminStaffPermissions.FullAccess()
            : staff.Permissions ?? AdminStaffPermissions.DefaultStaff();

        var body = new Dictionary<string, object?>
        {
            ["role_id"] = roleId,
            ["first_name"] = staff.FirstName.Trim(),
            ["last_name"] = staff.LastName.Trim(),
            ["status"] = staff.Status,
            ["last_login_at"] = staff.LastLogin == default ? null : staff.LastLogin,
            ["is_primary_admin"] = staff.IsPrimaryAdmin,
            ["permissions"] = permissions
        };

        var rows = await SendForListAsync<UserRow>(
            HttpMethod.Patch,
            $"rest/v1/users?id=eq.{Esc(existing.Id)}",
            body,
            "return=representation");

        if (rows.Count == 0)
            throw new InvalidOperationException("Staff account was not updated.");

        var refreshed = (await GetListAsync<UserWithRoleRow>(
            $"rest/v1/users_with_roles?select=*&id=eq.{Esc(existing.Id)}&limit=1"))
            .FirstOrDefault();

        if (refreshed is null)
            return staff;

        var saved = UserRowToStaffMember(refreshed);
        staff.Id = saved.Id;
        staff.FirstName = saved.FirstName;
        staff.LastName = saved.LastName;
        staff.Email = saved.Email;
        staff.Role = saved.Role;
        staff.Status = saved.Status;
        staff.LastLogin = saved.LastLogin;
        staff.IsPrimaryAdmin = saved.IsPrimaryAdmin;
        staff.Permissions = saved.Permissions;
        return staff;
    }

    public async Task<bool> DeleteStaffAsync(string id)
    {
        if (!Guid.TryParse(id, out _))
            return false;

        // Removing staff access does not delete the Supabase Auth account.
        // It safely converts the account back to Customer.
        var body = new Dictionary<string, object?>
        {
            ["role_id"] = 3,
            ["is_primary_admin"] = false,
            ["permissions"] = new AdminStaffPermissions()
        };

        var rows = await SendForListAsync<UserRow>(
            HttpMethod.Patch,
            $"rest/v1/users?id=eq.{Esc(id)}",
            body,
            "return=representation");

        return rows.Count > 0;
    }

    // ========================================================
    // Promotions
    // ========================================================

    public async Task<List<AdminPromotion>> GetPromotionsAsync()
    {
        var rows = await GetListAsync<PromotionRow>(
            "rest/v1/promotions?select=*&order=start_date.desc");

        var result = new List<AdminPromotion>();
        foreach (var r in rows)
        {
            var productLinks = await GetListAsync<PromotionProductRow>(
                $"rest/v1/promotion_products?select=product_id&promotion_id=eq.{Esc(r.Id)}");
            var categoryLinks = await GetListAsync<PromotionCategoryRow>(
                $"rest/v1/promotion_categories?select=category_id&promotion_id=eq.{Esc(r.Id)}");

            result.Add(new AdminPromotion
            {
                Id = r.Id,
                Name = r.Name,
                Code = r.Code,
                DiscountType = r.DiscountType,
                DiscountValue = r.DiscountValue,
                MinimumOrder = r.MinimumOrder,
                MaximumDiscount = r.MaximumDiscount,
                UsedCount = r.UsedCount,
                UsageLimit = r.UsageLimit,
                UsagePerCustomer = r.UsagePerCustomer,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                Enabled = r.Enabled,
                Description = r.Description,
                ProductIds = productLinks.Select(x => x.ProductId).ToList(),
                CategoryIds = categoryLinks.Select(x => x.CategoryId).ToList()
            });
        }

        return result;
    }

    public async Task<AdminPromotion> UpsertPromotionAsync(AdminPromotion promotion)
    {
        if (string.IsNullOrWhiteSpace(promotion.Id))
            promotion.Id = $"promo-{Guid.NewGuid():N}";

        var body = PromotionPayload(promotion);
        var rows = await SendForListAsync<PromotionRow>(
            HttpMethod.Post,
            "rest/v1/promotions?on_conflict=id",
            body,
            "resolution=merge-duplicates,return=representation");

        using (var delProducts = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/promotion_products?promotion_id=eq.{Esc(promotion.Id)}"))
        {
            await EnsureSuccessAsync(delProducts);
        }
        using (var delCategories = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/promotion_categories?promotion_id=eq.{Esc(promotion.Id)}"))
        {
            await EnsureSuccessAsync(delCategories);
        }

        if (promotion.ProductIds.Count > 0)
        {
            var links = promotion.ProductIds.Select(id => new
            {
                promotion_id = promotion.Id,
                product_id = id
            }).ToList();
            using var insert = await SendAsync(
                HttpMethod.Post,
                "rest/v1/promotion_products",
                links);
            await EnsureSuccessAsync(insert);
        }

        if (promotion.CategoryIds.Count > 0)
        {
            var links = promotion.CategoryIds.Select(id => new
            {
                promotion_id = promotion.Id,
                category_id = id
            }).ToList();
            using var insert = await SendAsync(
                HttpMethod.Post,
                "rest/v1/promotion_categories",
                links);
            await EnsureSuccessAsync(insert);
        }

        return rows.Count == 0 ? promotion : (await GetPromotionsAsync())
            .First(p => p.Id.Equals(promotion.Id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> DeletePromotionAsync(string id)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"rest/v1/promotions?id=eq.{Esc(id)}");
        return response.IsSuccessStatusCode;
    }

    public async Task RecordPromotionUsageAsync(
        string promotionId,
        string userEmail,
        string orderId,
        decimal discountAmount)
    {
        RequireAuth();
        var body = new Dictionary<string, object?>
        {
            ["promotion_id"] = promotionId,
            ["auth_user_id"] = _session.AuthUserId,
            ["user_email"] = _session.Email ?? userEmail,
            ["order_id"] = orderId,
            ["discount_amount"] = discountAmount,
            ["status"] = "Redeemed",
            ["used_at"] = DateTime.UtcNow
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            "rest/v1/promotion_usages",
            body);
        await EnsureSuccessAsync(response);
    }

    public async Task<PromoValidationResult> ValidatePromotionAsync(PromoValidationRequest request)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        var subtotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);

        if (string.IsNullOrWhiteSpace(code))
            return PromoInvalid("Enter a promo code.", subtotal);

        var promotions = await GetPromotionsAsync();
        var promo = promotions.FirstOrDefault(p =>
            p.Code.Equals(code, StringComparison.OrdinalIgnoreCase) &&
            p.Enabled &&
            DateTime.Now >= p.StartDate &&
            DateTime.Now <= p.EndDate);

        if (promo is null)
            return PromoInvalid("Promo code is invalid or inactive.", subtotal);

        if (promo.UsageLimit > 0 && promo.UsedCount >= promo.UsageLimit)
            return PromoInvalid("Promo code usage limit has been reached.", subtotal);

        if (_session.IsAuthenticated && promo.UsagePerCustomer > 0)
        {
            var uses = await GetListAsync<PromotionUsageCountRow>(
                $"rest/v1/promotion_usages?select=id&promotion_id=eq.{Esc(promo.Id)}&auth_user_id=eq.{Esc(_session.AuthUserId!)}&status=eq.Redeemed");
            if (uses.Count >= promo.UsagePerCustomer)
                return PromoInvalid("You have already used this promo code the maximum number of times.", subtotal);
        }

        if (subtotal < promo.MinimumOrder)
            return PromoInvalid($"Minimum order is ₱{promo.MinimumOrder:N0}.", subtotal);

        decimal eligible = 0;
        var unrestricted = promo.ProductIds.Count == 0 && promo.CategoryIds.Count == 0;

        if (unrestricted)
        {
            eligible = subtotal;
        }
        else
        {
            var categories = await GetCategoriesAsync();
            var allowedCategoryNames = categories
                .Where(c => promo.CategoryIds.Contains(c.Id))
                .Select(c => c.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var item in request.Items)
            {
                var category = item.Category;
                if (string.IsNullOrWhiteSpace(category))
                    category = (await GetProductByIdAsync(item.ProductId))?.Category;

                if (promo.ProductIds.Contains(item.ProductId) ||
                    (!string.IsNullOrWhiteSpace(category) && allowedCategoryNames.Contains(category)))
                {
                    eligible += item.UnitPrice * item.Quantity;
                }
            }
        }

        if (eligible <= 0)
            return PromoInvalid("This promo does not apply to the items in your cart.", subtotal);

        decimal discount = promo.DiscountType.Equals("fixed", StringComparison.OrdinalIgnoreCase)
            ? Math.Min(promo.DiscountValue, eligible)
            : Math.Round(eligible * (promo.DiscountValue / 100m), 2);

        if (promo.MaximumDiscount is > 0)
            discount = Math.Min(discount, promo.MaximumDiscount.Value);

        return new PromoValidationResult
        {
            Valid = true,
            Message = "Promo code applied.",
            Code = promo.Code,
            PromotionId = promo.Id,
            PromotionName = promo.Name,
            DiscountAmount = discount,
            Subtotal = subtotal,
            EligibleSubtotal = eligible,
            Total = Math.Max(0, subtotal - discount)
        };
    }

    public async Task<List<ActivePromotionDto>> GetActivePromotionsAsync()
    {
        var now = DateTime.Now;
        return (await GetPromotionsAsync())
            .Where(p => p.Enabled && now >= p.StartDate && now <= p.EndDate)
            .Select(p => new ActivePromotionDto
            {
                Code = p.Code,
                Name = p.Name,
                DiscountLabel = p.DiscountLabel,
                MinimumOrder = p.MinimumOrder,
                EndDate = p.EndDate,
                Description = p.Description
            })
            .ToList();
    }

    // ========================================================
    // Inventory
    // ========================================================

    public Task<List<InventoryHistoryEntry>> GetInventoryHistoryAsync() =>
        GetListAsync<InventoryHistoryEntry>(
            "rest/v1/inventory_history?select=*&order=date.desc");

    public async Task AddInventoryHistoryAsync(InventoryHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Guid.NewGuid().ToString("N");

        var body = new Dictionary<string, object?>
        {
            ["id"] = entry.Id,
            ["product_id"] = entry.ProductId,
            ["product_name"] = entry.ProductName,
            ["type"] = entry.Type,
            ["quantity"] = entry.Quantity,
            ["previous_stock"] = entry.PreviousStock,
            ["new_stock"] = entry.NewStock,
            ["reason"] = entry.Reason,
            ["notes"] = entry.Notes,
            ["date"] = entry.Date == default ? DateTime.UtcNow : entry.Date,
            ["admin_name"] = entry.AdminName
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            "rest/v1/inventory_history",
            body);
        await EnsureSuccessAsync(response);
    }

    public async Task<Dictionary<int, int>> GetReservedStockAsync()
    {
        var rows = await GetListAsync<InventoryMetaRow>(
            "rest/v1/inventory_meta?select=product_id,reserved");
        return rows.ToDictionary(x => x.ProductId, x => x.Reserved);
    }

    public async Task SetReservedStockAsync(int productId, int reserved)
    {
        var current = (await GetListAsync<InventoryMetaRow>(
            $"rest/v1/inventory_meta?select=*&product_id=eq.{productId}&limit=1"))
            .FirstOrDefault();

        var body = new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["reserved"] = Math.Max(0, reserved),
            ["low_stock_level"] = current?.LowStockLevel ?? 20
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            "rest/v1/inventory_meta?on_conflict=product_id",
            body,
            "resolution=merge-duplicates");
        await EnsureSuccessAsync(response);
    }

    public async Task<Dictionary<int, int>> GetLowStockLevelsAsync()
    {
        var rows = await GetListAsync<InventoryMetaRow>(
            "rest/v1/inventory_meta?select=product_id,low_stock_level");
        return rows.ToDictionary(x => x.ProductId, x => x.LowStockLevel);
    }

    public async Task SetLowStockLevelAsync(int productId, int level)
    {
        var current = (await GetListAsync<InventoryMetaRow>(
            $"rest/v1/inventory_meta?select=*&product_id=eq.{productId}&limit=1"))
            .FirstOrDefault();

        var body = new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["reserved"] = current?.Reserved ?? 0,
            ["low_stock_level"] = Math.Max(0, level)
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            "rest/v1/inventory_meta?on_conflict=product_id",
            body,
            "resolution=merge-duplicates");
        await EnsureSuccessAsync(response);
    }

    // ========================================================
    // Cart
    // ========================================================

    public async Task<List<CartItemDto>> GetCartAsync(string email)
    {
        _ = email;
        if (!_session.IsAuthenticated || string.IsNullOrWhiteSpace(_session.AuthUserId))
            return [];

        var carts = await GetListAsync<CartRow>(
            $"rest/v1/carts?select=id&auth_user_id=eq.{Esc(_session.AuthUserId)}&limit=1");
        var cart = carts.FirstOrDefault();
        if (cart is null) return [];

        return await GetListAsync<CartItemDto>(
            $"rest/v1/cart_items?select=product_id,variant_id,quantity,unit_price,selected_color,selected_size&cart_id=eq.{cart.Id}&order=id.asc");
    }

    public async Task SaveCartAsync(string email, IEnumerable<CartItemDto> items)
    {
        RequireAuth();
        var uid = _session.AuthUserId!;
        var cart = (await GetListAsync<CartRow>(
            $"rest/v1/carts?select=id,user_email,auth_user_id&auth_user_id=eq.{Esc(uid)}&limit=1"))
            .FirstOrDefault();

        if (cart is null)
        {
            var created = await SendForListAsync<CartRow>(
                HttpMethod.Post,
                "rest/v1/carts",
                new Dictionary<string, object?>
                {
                    ["user_email"] = _session.Email ?? email,
                    ["auth_user_id"] = uid
                },
                "return=representation");
            cart = created.First();
        }

        using (var delete = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/cart_items?cart_id=eq.{cart.Id}"))
        {
            await EnsureSuccessAsync(delete);
        }

        var rows = items.Select(i => new Dictionary<string, object?>
        {
            ["cart_id"] = cart.Id,
            ["product_id"] = i.ProductId,
            ["variant_id"] = i.VariantId,
            ["quantity"] = Math.Max(1, i.Quantity),
            ["unit_price"] = i.UnitPrice,
            ["selected_color"] = i.SelectedColor,
            ["selected_size"] = i.SelectedSize
        }).ToList();

        if (rows.Count == 0) return;

        using var insert = await SendAsync(
            HttpMethod.Post,
            "rest/v1/cart_items",
            rows);
        await EnsureSuccessAsync(insert);
    }

    // ========================================================
    // Wishlist
    // ========================================================

    public async Task<List<int>> GetWishlistAsync(string email)
    {
        _ = email;
        if (!_session.IsAuthenticated || string.IsNullOrWhiteSpace(_session.AuthUserId))
            return [];

        var rows = await GetListAsync<WishlistRow>(
            $"rest/v1/wishlists?select=product_id&auth_user_id=eq.{Esc(_session.AuthUserId)}&order=created_at.desc");
        return rows.Select(x => x.ProductId).Distinct().ToList();
    }

    public async Task SaveWishlistAsync(string email, IEnumerable<int> productIds)
    {
        RequireAuth();
        var uid = _session.AuthUserId!;
        var resolvedEmail = _session.Email ?? email;

        using (var delete = await SendAsync(
                   HttpMethod.Delete,
                   $"rest/v1/wishlists?auth_user_id=eq.{Esc(uid)}"))
        {
            await EnsureSuccessAsync(delete);
        }

        var rows = productIds.Distinct().Select(productId => new Dictionary<string, object?>
        {
            ["email"] = resolvedEmail,
            ["auth_user_id"] = uid,
            ["product_id"] = productId,
            ["created_at"] = DateTime.UtcNow
        }).ToList();

        if (rows.Count == 0) return;

        using var insert = await SendAsync(
            HttpMethod.Post,
            "rest/v1/wishlists",
            rows);
        await EnsureSuccessAsync(insert);
    }

    // ========================================================
    // Settings
    // ========================================================

    public async Task<string?> GetSettingAsync(string key)
    {
        var rows = await GetListAsync<SettingRow>(
            $"rest/v1/settings?select=key,value&key=eq.{Esc(key)}&limit=1");
        return rows.FirstOrDefault()?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "rest/v1/settings?on_conflict=key",
            new { key, value },
            "resolution=merge-duplicates");
        await EnsureSuccessAsync(response);
    }

    // ========================================================
    // Stats
    // ========================================================

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var orders = await GetOrdersAsync();
        var products = await GetProductsAsync();

        var completed = orders.Where(o =>
            o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)).ToList();

        var stats = new DashboardStats
        {
            TotalSales = completed.Sum(o => o.Total),
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o =>
                o.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)),
            CompletedOrders = completed.Count,
            TotalProducts = products.Count,
            LowStockCount = products.Count(p => p.Stock <= AdminProduct.LowStockThreshold),
            RecentOrders = orders
                .OrderByDescending(o => o.Date)
                .Take(5)
                .Select(o => new AdminOrderRow(
                    o.Id,
                    o.CustomerName,
                    o.Fulfillment,
                    o.Date.ToString("yyyy-MM-dd"),
                    o.Total,
                    o.Status))
                .ToList(),
            OrderStatus = orders
                .GroupBy(o => o.Status)
                .Select(g => new AdminStatusSlice(g.Key, g.Count(), ""))
                .ToList(),
            TopProducts = BuildTopProducts(orders),
            SalesByMonth = BuildMonthlySales(completed)
        };

        return stats;
    }

    public async Task<StorefrontStats> GetStorefrontStatsAsync()
    {
        var result = await SendForSingleAsync<StorefrontStats>(
            HttpMethod.Post,
            "rest/v1/rpc/get_storefront_stats",
            new { });

        return result ?? new StorefrontStats();
    }

    // ========================================================
    // Private helpers
    // ========================================================

    private async Task<List<AdminOrderItem>> GetOrderItemsAsync(string orderId) =>
        await GetListAsync<AdminOrderItem>(
            $"rest/v1/order_items?select=product_id,name,image_url,quantity,price,variant_id,size,variant_sku&order_id=eq.{Esc(orderId)}&order=id.asc");

    private async Task<MockUser?> BuildMockUserAsync(string authUserId, string accessToken)
    {
        var rows = await GetListAsync<UserWithRoleRow>(
            $"rest/v1/users_with_roles?select=*&id=eq.{Esc(authUserId)}&limit=1",
            accessToken);

        return rows.Count == 0 ? null : UserRowToMockUser(rows[0]);
    }

    private static MockUser UserRowToMockUser(UserWithRoleRow p) => new()
    {
        Id = Guid.TryParse(p.Id, out var id) ? id : Guid.Empty,
        UserId = 0,
        RoleId = p.RoleId,
        Name = $"{p.FirstName} {p.LastName}".Trim(),
        FirstName = p.FirstName ?? string.Empty,
        LastName = p.LastName ?? string.Empty,
        Email = p.Email ?? string.Empty,
        Phone = p.PhoneNumber ?? string.Empty,
        StudentId = p.StudentId ?? string.Empty,
        College = p.College ?? string.Empty,
        Address = p.Address ?? string.Empty,
        Role = p.Role,
        Status = p.Status,
        ProfileImage = p.ProfileImage ?? string.Empty,
        CreatedAt = p.CreatedAt
    };

    private static AdminStaffMember UserRowToStaffMember(UserWithRoleRow r) => new()
    {
        Id = r.Id,
        FirstName = r.FirstName ?? string.Empty,
        LastName = r.LastName ?? string.Empty,
        Email = r.Email ?? string.Empty,
        Role = r.Role,
        Status = r.Status,
        LastLogin = r.LastLoginAt ?? DateTime.MinValue,
        IsPrimaryAdmin = r.IsPrimaryAdmin,
        Permissions = r.Permissions ?? AdminStaffPermissions.DefaultStaff()
    };

    private static MockUser BuildFallbackUser(
        SupabaseAuthUser auth,
        string firstName,
        string lastName,
        string phone) => new()
        {
            Id = Guid.TryParse(auth.Id, out var id) ? id : Guid.Empty,
            Name = $"{firstName} {lastName}".Trim(),
            FirstName = firstName,
            LastName = lastName,
            Email = auth.Email ?? string.Empty,
            Phone = phone,
            RoleId = 3,
            Role = "Customer",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

    private async Task<SupabaseAuthUser?> GetAuthUserAsync(string accessToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            "auth/v1/user",
            bearerOverride: accessToken);
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SupabaseAuthUser>(json, JsonOptions);
    }

    private async Task<SupabaseAuthResponse> SendAuthAsync(
        HttpMethod method,
        string path,
        object body)
    {
        using var response = await SendAsync(
            method,
            path,
            body);

        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync();

        var auth =
            JsonSerializer.Deserialize<SupabaseAuthResponse>(
                json,
                JsonOptions)
            ?? new SupabaseAuthResponse();

        // Supabase /signup may return the user directly instead of
        // placing it inside a "user" property.
        if (auth.User is null &&
            !string.IsNullOrWhiteSpace(auth.Id))
        {
            auth.User = new SupabaseAuthUser
            {
                Id = auth.Id,
                Email = auth.Email
            };
        }

        return auth;
    }

    private async Task<List<T>> GetListAsync<T>(string path, string? bearerOverride = null)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            path,
            bearerOverride: bearerOverride);
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return [];

        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    private async Task<List<T>> SendForListAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        string? prefer = null,
        string? bearerOverride = null)
    {
        using var response = await SendAsync(
            method,
            path,
            body,
            prefer,
            bearerOverride);
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return [];

        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    private async Task<T?> SendForSingleAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        string? prefer = null,
        string? bearerOverride = null)
    {
        using var response = await SendAsync(
            method,
            path,
            body,
            prefer,
            bearerOverride);
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return default;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? prefer = null,
        string? bearerOverride = null)
    {
        // Relative paths must resolve against BaseAddress (…supabase.co/).
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.TryAddWithoutValidation("apikey", _options.AnonKey);

        // Supabase requires Authorization on every call. Use the user JWT when
        // signed in; otherwise fall back to the publishable/anon key.
        var bearer = !string.IsNullOrWhiteSpace(bearerOverride)
            ? bearerOverride
            : !string.IsNullOrWhiteSpace(_session.AccessToken)
                ? _session.AccessToken
                : _options.AnonKey;

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        if (!string.IsNullOrWhiteSpace(prefer))
            request.Headers.TryAddWithoutValidation("Prefer", prefer);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        try
        {
            return await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            var target = _http.BaseAddress is null
                ? path
                : new Uri(_http.BaseAddress, path.TrimStart('/')).ToString();

            throw new HttpRequestException(
                $"Unable to reach Supabase at '{target}'. " +
                "Check Supabase:Url / network / firewall. " +
                ex.Message,
                ex);
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            var target = _http.BaseAddress is null
                ? path
                : new Uri(_http.BaseAddress, path.TrimStart('/')).ToString();

            throw new HttpRequestException(
                $"Supabase request timed out for '{target}'.",
                ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var raw = await response.Content.ReadAsStringAsync();
        var message = ExtractError(raw);
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(message)
                ? $"Supabase request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
                : message);
    }

    private static string ExtractError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            foreach (var key in new[] { "message", "msg", "error_description", "error", "details" })
            {
                if (root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                    return p.GetString() ?? raw;
            }
        }
        catch
        {
        }
        return raw;
    }

    private void RequireAuth()
    {
        if (!_session.IsAuthenticated)
            throw new InvalidOperationException("Please sign in first.");
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);

    private static string GenerateOrderNumber() =>
        $"NUBE-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static AuthResult Fail(string message) => new()
    {
        Success = false,
        Error = message
    };

    private static string CleanAuthError(string message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        var lower = message.ToLowerInvariant();
        if (lower.Contains("invalid login credentials")) return "Invalid email or password.";
        if (lower.Contains("user already registered")) return "An account with this email already exists.";
        if (lower.Contains("email not confirmed")) return "Please verify your email before signing in.";
        if (lower.Contains("password should be")) return "The password does not meet the required security rules.";
        return message.Length > 300 ? fallback : message;
    }

    private static Dictionary<string, object?> ProductPayload(Product p) => new()
    {
        ["name"] = p.Name,
        ["category"] = p.Category,
        ["image_url"] = p.ImageUrl,
        ["images"] = p.Images,
        ["price"] = p.Price,
        ["original_price"] = p.OriginalPrice,
        ["rating"] = p.Rating,
        ["reviews"] = p.Reviews,
        ["sold"] = p.Sold,
        ["stock"] = Math.Max(0, p.Stock),
        ["badge"] = p.Badge,
        ["colors"] = p.Colors,
        ["sizes"] = p.Sizes,
        ["material"] = p.Material,
        ["sku"] = p.Sku,
        ["in_stock"] = p.Stock > 0,
        ["is_featured"] = p.IsFeatured,
        ["is_fresh_drop"] = p.IsFreshDrop,
        ["is_best_seller"] = p.IsBestSeller,
        ["is_new_arrival"] = p.IsNewArrival,
        ["is_favorite"] = p.IsFavorite,
        ["description"] = p.Description,
        ["full_description"] = p.FullDescription,
        ["features"] = p.Features,
        ["rating_breakdown"] = p.RatingBreakdown,
        ["section"] = p.Section,
        ["status"] = p.Status,
        ["is_published"] = p.IsPublished,
        ["published_at"] = p.PublishedAt,
        ["created_by"] = p.CreatedBy
    };

    private Dictionary<string, object?> OrderPayload(AdminOrder o) => new()
    {
        ["id"] = o.Id,
        ["customer_id"] = string.IsNullOrWhiteSpace(o.CustomerId)
            ? (_session.AuthUserId ?? "")
            : o.CustomerId,
        ["auth_user_id"] = _session.AuthUserId,
        ["customer_name"] = o.CustomerName,
        ["customer_email"] = string.IsNullOrWhiteSpace(o.CustomerEmail)
            ? (_session.Email ?? "")
            : o.CustomerEmail,
        ["date"] = o.Date,
        ["subtotal"] = o.Subtotal,
        ["discount_amount"] = o.DiscountAmount,
        ["total"] = o.Total,
        ["promotion_id"] = o.PromotionId,
        ["promotion_code"] = o.PromotionCode,
        ["payment_status"] = o.PaymentStatus,
        ["fulfillment"] = o.Fulfillment,
        ["status"] = o.Status
    };

    private static Dictionary<string, object?> PromotionPayload(AdminPromotion p) => new()
    {
        ["id"] = p.Id,
        ["name"] = p.Name,
        ["code"] = p.Code.Trim().ToUpperInvariant(),
        ["discount_type"] = p.DiscountType,
        ["discount_value"] = p.DiscountValue,
        ["minimum_order"] = p.MinimumOrder,
        ["maximum_discount"] = p.MaximumDiscount,
        ["used_count"] = p.UsedCount,
        ["usage_limit"] = p.UsageLimit,
        ["usage_per_customer"] = p.UsagePerCustomer,
        ["start_date"] = p.StartDate,
        ["end_date"] = p.EndDate,
        ["enabled"] = p.Enabled,
        ["description"] = p.Description
    };

    private static PromoValidationResult PromoInvalid(string message, decimal subtotal) => new()
    {
        Valid = false,
        Message = message,
        Subtotal = subtotal,
        EligibleSubtotal = 0,
        DiscountAmount = 0,
        Total = subtotal
    };

    private static List<AdminTopProduct> BuildTopProducts(IEnumerable<AdminOrder> orders)
    {
        return orders
            .Where(o => !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.ProductId, i.Name, i.ImageUrl })
            .Select(g => new
            {
                g.Key,
                Sold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Price * x.Quantity)
            })
            .OrderByDescending(x => x.Sold)
            .Take(5)
            .Select((x, index) => new AdminTopProduct(
                index + 1,
                x.Key.Name,
                x.Key.ImageUrl,
                x.Sold,
                x.Revenue,
                x.Key.Name.Length <= 14 ? x.Key.Name : x.Key.Name[..14] + "…"))
            .ToList();
    }

    private static List<MonthlySalesPoint> BuildMonthlySales(IEnumerable<AdminOrder> orders)
    {
        return orders
            .GroupBy(o => new DateTime(o.Date.Year, o.Date.Month, 1))
            .OrderBy(g => g.Key)
            .TakeLast(12)
            .Select(g => new MonthlySalesPoint
            {
                Month = g.Key.ToString("MMM"),
                Value = (double)g.Sum(o => o.Total)
            })
            .ToList();
    }

    // ========================================================
    // Internal REST row models
    // ========================================================

    private sealed class ProductReviewRow
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime ReviewDate { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    private sealed class SupabaseAuthResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }

        // Returned when Supabase sends a token/session response
        public SupabaseAuthUser? User { get; set; }

        // Returned directly by /signup when email confirmation is enabled
        public string? Id { get; set; }
        public string? Email { get; set; }
    }

    private sealed class SupabaseAuthUser
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    private sealed class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public int RoleId { get; set; } = 3;
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string Status { get; set; } = "Active";
        public string? ProfileImage { get; set; }
        public string? StudentId { get; set; }
        public string? College { get; set; }
        public string? Address { get; set; }
        public bool IsPrimaryAdmin { get; set; }
        public AdminStaffPermissions? Permissions { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class UserWithRoleRow
    {
        public string Id { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "Customer";
        public string Status { get; set; } = "Active";
        public string? ProfileImage { get; set; }
        public string? StudentId { get; set; }
        public string? College { get; set; }
        public string? Address { get; set; }
        public bool IsPrimaryAdmin { get; set; }
        public AdminStaffPermissions? Permissions { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class OrderAggregateRow
    {
        public string? AuthUserId { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class CustomerNotificationRow
    {
        public long RowId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string Icon { get; set; } = "bell";
        public string Tone { get; set; } = "blue";
        public bool IsRead { get; set; }
    }

    private sealed class AdminNotificationRow
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "order";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RelatedId { get; set; }
        public string? RelatedLabel { get; set; }
        public string? RelatedHref { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    private sealed class PromotionRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = "percentage";
        public decimal DiscountValue { get; set; }
        public decimal MinimumOrder { get; set; }
        public decimal? MaximumDiscount { get; set; }
        public int UsedCount { get; set; }
        public int UsageLimit { get; set; }
        public int UsagePerCustomer { get; set; } = 1;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    private sealed class PromotionProductRow
    {
        public int ProductId { get; set; }
    }

    private sealed class PromotionCategoryRow
    {
        public string CategoryId { get; set; } = string.Empty;
    }

    private sealed class PromotionUsageCountRow
    {
        public long Id { get; set; }
    }

    private sealed class InventoryMetaRow
    {
        public int ProductId { get; set; }
        public int Reserved { get; set; }
        public int LowStockLevel { get; set; } = 20;
    }

    private sealed class CartRow
    {
        public long Id { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string? AuthUserId { get; set; }
    }

    private sealed class WishlistRow
    {
        public int ProductId { get; set; }
    }

    private sealed class SettingRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private sealed class CancelOrderRpcResult
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class CheckoutRpcResult
    {
        public string Id { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Total { get; set; }
        public string? PromotionId { get; set; }
        public string? PromotionCode { get; set; }
        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? Date { get; set; }
    }
}
