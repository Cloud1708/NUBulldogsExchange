using System.Text.Json;
using Microsoft.JSInterop;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class CartService
{
    private readonly IAppDatabase _db;
    private readonly ProductCatalogService _catalog;
    private readonly List<CartItem> _items = [];
    public event Action? OnChange;

    public CartService(IAppDatabase db, ProductCatalogService catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public IReadOnlyList<CartItem> Items => _items;
    public int TotalCount => _items.Sum(i => i.Quantity);
    public string? AppliedPromoCode { get; private set; }
    public decimal AppliedDiscount { get; private set; }
    public decimal Subtotal => _items.Sum(i => i.Product.Price * i.Quantity);
    public decimal EstimatedTotal => Math.Max(0, Subtotal - AppliedDiscount);

    public void SetPromo(string? code, decimal discount)
    {
        var normalized = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        var amount = Math.Max(0, discount);
        if (string.Equals(AppliedPromoCode, normalized, StringComparison.Ordinal) &&
            AppliedDiscount == amount)
            return;

        AppliedPromoCode = normalized;
        AppliedDiscount = amount;
        OnChange?.Invoke();
    }

    public void ClearPromo()
    {
        if (AppliedPromoCode is null && AppliedDiscount == 0)
            return;

        AppliedPromoCode = null;
        AppliedDiscount = 0;
        OnChange?.Invoke();
    }

    public void Add(Product product, int quantity = 1, string? color = null, string? size = null)
    {
        quantity = Math.Max(1, quantity);
        var colorKey = Normalize(color);
        var sizeKey = Normalize(size);

        var existing = _items.FirstOrDefault(i =>
            i.Product.Id == product.Id &&
            Normalize(i.SelectedColor) == colorKey &&
            Normalize(i.SelectedSize) == sizeKey);

        if (existing is not null)
            existing.Quantity += quantity;
        else
        {
            _items.Add(new CartItem
            {
                Product = product,
                Quantity = quantity,
                SelectedColor = string.IsNullOrWhiteSpace(color) ? null : color,
                SelectedSize = string.IsNullOrWhiteSpace(size) ? null : size
            });
        }

        OnChange?.Invoke();
    }

    public void Remove(string key)
    {
        _items.RemoveAll(i => i.Key == key);
        OnChange?.Invoke();
    }

    public void Remove(int productId)
    {
        _items.RemoveAll(i => i.Product.Id == productId);
        OnChange?.Invoke();
    }

    public void UpdateQuantity(string key, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.Key == key);
        if (item is null) return;

        var max = item.Product.Stock > 0 ? item.Product.Stock : int.MaxValue;
        item.Quantity = Math.Clamp(quantity, 1, max);
        OnChange?.Invoke();
    }

    public void UpdateQuantity(int productId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.Product.Id == productId);
        if (item is null) return;
        UpdateQuantity(item.Key, quantity);
    }

    public void Clear()
    {
        _items.Clear();
        AppliedPromoCode = null;
        AppliedDiscount = 0;
        OnChange?.Invoke();
    }

    public async Task RestoreAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            await _catalog.EnsureLoadedAsync();
            var rows = await _db.GetCartAsync(email);
            _items.Clear();
            foreach (var row in rows)
            {
                var product = _catalog.GetById(row.ProductId) ?? await _db.GetProductByIdAsync(row.ProductId);
                if (product is null) continue;
                _items.Add(new CartItem
                {
                    Product = product,
                    Quantity = Math.Max(1, row.Quantity),
                    SelectedColor = row.SelectedColor,
                    SelectedSize = row.SelectedSize
                });
            }

            OnChange?.Invoke();
        }
        catch
        {
            // Ignore restore failures during prerender / offline.
        }
    }

    public async Task PersistAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            var dtos = _items.Select(i => new CartItemDto
            {
                ProductId = i.Product.Id,
                Quantity = i.Quantity,
                UnitPrice = i.Product.Price,
                SelectedColor = i.SelectedColor,
                SelectedSize = i.SelectedSize
            });
            await _db.SaveCartAsync(email, dtos);
        }
        catch
        {
            // Ignore persist failures during prerender / offline.
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

public class WishlistService
{
    private readonly IAppDatabase _db;
    private readonly HashSet<int> _ids = [];
    public event Action? OnChange;

    public WishlistService(IAppDatabase db)
    {
        _db = db;
    }

    public int Count => _ids.Count;
    public IReadOnlyCollection<int> Ids => _ids;

    public bool Contains(int productId) => _ids.Contains(productId);

    public void Toggle(int productId)
    {
        if (!_ids.Add(productId))
            _ids.Remove(productId);

        OnChange?.Invoke();
    }

    public async Task RestoreAsync(IJSRuntime js, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            var ids = await _db.GetWishlistAsync(email);
            _ids.Clear();
            foreach (var id in ids)
                _ids.Add(id);
            OnChange?.Invoke();
        }
        catch
        {
            // Fall back to browser storage if API/DB is unavailable during prerender.
            try
            {
                var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey(email));
                _ids.Clear();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var localIds = JsonSerializer.Deserialize<List<int>>(json) ?? [];
                    foreach (var id in localIds)
                        _ids.Add(id);
                }

                OnChange?.Invoke();
            }
            catch (JSException)
            {
            }
            catch (JsonException)
            {
            }
        }
    }

    public async Task PersistAsync(IJSRuntime js, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            await _db.SaveWishlistAsync(email, _ids);
        }
        catch
        {
            try
            {
                var json = JsonSerializer.Serialize(_ids.ToList());
                await js.InvokeVoidAsync("localStorage.setItem", StorageKey(email), json);
            }
            catch (JSException)
            {
            }
        }
    }

    private static string StorageKey(string email) =>
        "nube-wishlist:" + email.Trim().ToLowerInvariant();
}

public class ToastService
{
    public event Action<string>? OnShow;

    public void Show(string message) => OnShow?.Invoke(message);
}

public class OrderService
{
    private readonly IAppDatabase _db;
    private readonly List<MockOrder> _orders = [];
    private bool _loaded;
    private bool _loading;
    private string? _loadedEmail;
    public event Action? OnChange;

    public OrderService(IAppDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<MockOrder> Orders => _orders;
    public int TotalCount => _orders.Count;
    public int ActiveCount => _orders.Count(o => o.IsActive);

    public async Task EnsureLoadedAsync(string? email = null)
    {
        var emailKey = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (_loading) return;
        if (_loaded && string.Equals(_loadedEmail, emailKey, StringComparison.Ordinal))
            return;

        _loading = true;
        try
        {
            var adminOrders = await _db.GetOrdersAsync();
            _orders.Clear();
            var filtered = emailKey is null
                ? adminOrders
                : adminOrders.Where(o => o.CustomerEmail.Equals(emailKey, StringComparison.OrdinalIgnoreCase));
            _orders.AddRange(filtered.Select(MockOrder.FromAdmin));
            _loadedEmail = emailKey;
            _loaded = true;
            OnChange?.Invoke();
        }
        finally
        {
            _loading = false;
        }
    }

    public IEnumerable<MockOrder> Recent(int take = 3) =>
        _orders.OrderByDescending(o => o.Date).ThenByDescending(o => o.Id).Take(take);

    public IEnumerable<MockOrder> Filter(string status)
    {
        var ordered = _orders.OrderByDescending(o => o.Date).ThenByDescending(o => o.Id);
        if (string.IsNullOrWhiteSpace(status) || status.Equals("All", StringComparison.OrdinalIgnoreCase))
            return ordered;

        return ordered.Where(o => o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    public void Cancel(string orderId)
    {
        var order = _orders.FirstOrDefault(o => o.Id == orderId);
        if (order is null || !order.CanCancel)
            return;

        order.Status = "Cancelled";
        var admin = _db.GetOrderByIdAsync(orderId).GetAwaiter().GetResult();
        if (admin is not null)
        {
            admin.Status = "Cancelled";
            _db.UpsertOrderAsync(admin).GetAwaiter().GetResult();
        }

        OnChange?.Invoke();
    }

    public MockOrder PlaceOrder(AdminOrder order)
    {
        var mock = MockOrder.FromAdmin(order);
        _orders.RemoveAll(o => o.Id.Equals(mock.Id, StringComparison.OrdinalIgnoreCase));
        _orders.Insert(0, mock);
        OnChange?.Invoke();
        return mock;
    }
}

public class NotificationService
{
    private readonly IAppDatabase _db;
    private readonly List<MockNotification> _items = [];
    private string? _email;
    private string? _loadedEmail;
    private bool _loaded;
    private bool _loading;
    public event Action? OnChange;

    public NotificationService(IAppDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<MockNotification> Items => _items;
    public int UnreadCount => _items.Count(n => !n.IsRead);

    public async Task EnsureLoadedAsync(string? email = null)
    {
        var emailKey = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (_loading) return;
        if (_loaded && string.Equals(_loadedEmail, emailKey, StringComparison.Ordinal))
            return;

        _loading = true;
        try
        {
            _email = email;
            _items.Clear();
            _items.AddRange(await _db.GetCustomerNotificationsAsync(email));
            _loadedEmail = emailKey;
            _loaded = true;
            OnChange?.Invoke();
        }
        finally
        {
            _loading = false;
        }
    }

    public void MarkAllAsRead()
    {
        if (UnreadCount == 0)
            return;

        foreach (var item in _items)
            item.IsRead = true;

        _db.SaveCustomerNotificationsAsync(_items, _email).GetAwaiter().GetResult();
        OnChange?.Invoke();
    }

    public void Add(MockNotification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.Id))
            notification.Id = Guid.NewGuid().ToString("N");
        _items.Insert(0, notification);
        _db.SaveCustomerNotificationsAsync(_items, _email).GetAwaiter().GetResult();
        OnChange?.Invoke();
    }

    public async Task AddAsync(MockNotification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.Id))
            notification.Id = Guid.NewGuid().ToString("N");
        _items.Insert(0, notification);
        await _db.SaveCustomerNotificationsAsync(_items, _email);
        OnChange?.Invoke();
    }
}

public class AuthService
{
    private const string StorageKey = "nube-user";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IAppDatabase _db;

    public AuthService(IAppDatabase db)
    {
        _db = db;
    }

    public event Action? OnChange;

    public MockUser? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;
    public string? Email => CurrentUser?.Email;
    public string Role => CurrentUser?.Role ?? "guest";
    public bool RememberMe => CurrentUser?.RememberMe ?? false;
    public string DisplayName => CurrentUser?.Name ?? "Guest";
    public int UserId => CurrentUser?.UserId ?? 0;
    public string FirstName
    {
        get
        {
            if (CurrentUser is null)
                return "Guest";
            if (!string.IsNullOrWhiteSpace(CurrentUser.FirstName))
                return CurrentUser.FirstName;

            var name = CurrentUser.Name.Trim();
            var space = name.IndexOf(' ');
            return space > 0 ? name[..space] : name;
        }
    }
    public string Initial => CurrentUser?.Initial ?? "?";
    public bool IsCustomer => CurrentUser?.IsCustomer == true;
    public bool IsAdmin => CurrentUser?.IsAdmin == true;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var result = await _db.RegisterCustomerAsync(request);
        // Do not auto-login — user must sign in after creating an account.
        if (result.Success && !string.IsNullOrWhiteSpace(result.SessionToken))
        {
            try
            {
                await _db.LogoutSessionAsync(result.SessionToken);
            }
            catch
            {
            }

            result.SessionToken = null;
            if (result.User is not null)
                result.User.SessionToken = null;
        }

        return result;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe = false)
    {
        var result = await _db.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = rememberMe
        });
        if (result.Success && result.User is not null)
            SetUser(result.User, result.SessionToken, rememberMe);
        return result;
    }

    public void Logout() => _ = LogoutAsync();

    public async Task LogoutAsync()
    {
        var token = CurrentUser?.SessionToken;
        CurrentUser = null;
        OnChange?.Invoke();
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            await _db.LogoutSessionAsync(token);
        }
        catch
        {
        }
    }

    public async Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var token = CurrentUser?.SessionToken;
        if (string.IsNullOrWhiteSpace(token))
            return new AuthResult { Success = false, Error = "Please sign in to update your profile." };

        var result = await _db.UpdateCustomerProfileAsync(token, request);
        if (result.Success && result.User is not null)
        {
            result.User.RememberMe = RememberMe;
            result.User.SessionToken = token;
            CurrentUser = result.User;
            NormalizeProfile(CurrentUser);
            OnChange?.Invoke();
        }

        return result;
    }

    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var token = CurrentUser?.SessionToken;
        if (string.IsNullOrWhiteSpace(token))
            return new AuthResult { Success = false, Error = "Please sign in to change your password." };

        return await _db.ChangePasswordAsync(token, request);
    }

    public async Task<AuthResult> RestoreFromTokenAsync(string sessionToken, bool rememberMe = true)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            return new AuthResult { Success = false, Error = "Your session has expired. Please sign in again." };

        var result = await _db.ValidateSessionAsync(sessionToken);
        if (result.Success && result.User is not null)
        {
            SetUser(result.User, result.SessionToken ?? sessionToken, rememberMe);
            return result;
        }

        if (CurrentUser is not null)
        {
            CurrentUser = null;
            OnChange?.Invoke();
        }

        return result;
    }

    public async Task PersistAsync(IJSRuntime js)
    {
        try
        {
            if (CurrentUser is null)
            {
                await ClearStorageAsync(js);
                return;
            }

            var json = JsonSerializer.Serialize(CurrentUser);
            if (CurrentUser.RememberMe)
            {
                await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
            }
            else
            {
                await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
                await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
        }
        catch (JSException)
        {
        }
    }

    public async Task RestoreAsync(IJSRuntime js)
    {
        if (CurrentUser is not null)
            return;

        string? json = null;
        try
        {
            json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey)
                ?? await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
            return;

        MockUser? stored;
        try
        {
            stored = JsonSerializer.Deserialize<MockUser>(json, JsonOptions);
        }
        catch (JsonException)
        {
            await ClearStorageAsync(js);
            return;
        }

        if (stored is null || string.IsNullOrWhiteSpace(stored.SessionToken))
        {
            await ClearStorageAsync(js);
            return;
        }

        try
        {
            var result = await _db.ValidateSessionAsync(stored.SessionToken);
            if (!result.Success || result.User is null)
            {
                await ClearStorageAsync(js);
                return;
            }

            result.User.RememberMe = stored.RememberMe;
            result.User.SessionToken = stored.SessionToken;
            CurrentUser = result.User;
            NormalizeProfile(CurrentUser);
            OnChange?.Invoke();
        }
        catch
        {
            stored.SessionToken = stored.SessionToken;
            CurrentUser = stored;
            NormalizeProfile(CurrentUser);
            OnChange?.Invoke();
        }
    }

    public async Task LogoutAsync(IJSRuntime js)
    {
        await LogoutAsync();
        await ClearStorageAsync(js);
    }

    private void SetUser(MockUser user, string? sessionToken, bool rememberMe)
    {
        user.RememberMe = rememberMe;
        if (!string.IsNullOrWhiteSpace(sessionToken))
            user.SessionToken = sessionToken;
        NormalizeProfile(user);
        CurrentUser = user;
        OnChange?.Invoke();
    }

    private static void NormalizeProfile(MockUser user)
    {
        if (string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(user.Name))
        {
            var parts = user.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            user.FirstName = parts.Length > 0 ? parts[0] : user.Name;
            user.LastName = parts.Length > 1 ? parts[1] : user.LastName;
        }

        if (string.IsNullOrWhiteSpace(user.Name))
            user.Name = $"{user.FirstName} {user.LastName}".Trim();
    }

    private static async Task ClearStorageAsync(IJSRuntime js)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch (JSException)
        {
        }
    }
}
