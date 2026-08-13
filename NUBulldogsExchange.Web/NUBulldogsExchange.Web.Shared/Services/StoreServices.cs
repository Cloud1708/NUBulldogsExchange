using System.Text.Json;
using Microsoft.JSInterop;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class CartService
{
    private readonly List<CartItem> _items = [];
    public event Action? OnChange;

    public IReadOnlyList<CartItem> Items => _items;
    public int TotalCount => _items.Sum(i => i.Quantity);

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
        OnChange?.Invoke();
    }

    public decimal Subtotal => _items.Sum(i => i.Product.Price * i.Quantity);

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

public class WishlistService
{
    private const string KeyPrefix = "nube-demo-wishlist:";
    private readonly HashSet<int> _ids = [];
    public event Action? OnChange;

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
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey(email));
            _ids.Clear();

            if (!string.IsNullOrWhiteSpace(json))
            {
                var ids = JsonSerializer.Deserialize<List<int>>(json) ?? [];
                foreach (var id in ids)
                    _ids.Add(id);
            }

            OnChange?.Invoke();
        }
        catch (JSException)
        {
            // Browser storage may be unavailable during prerender.
        }
        catch (JsonException)
        {
            // Ignore invalid demo storage.
        }
    }

    public async Task PersistAsync(IJSRuntime js, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            var json = JsonSerializer.Serialize(_ids.ToList());
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey(email), json);
        }
        catch (JSException)
        {
            // Browser storage may be unavailable during prerender.
        }
    }

    private static string StorageKey(string email) =>
        KeyPrefix + email.Trim().ToLowerInvariant();
}

public class ToastService
{
    public event Action<string>? OnShow;

    public void Show(string message) => OnShow?.Invoke(message);
}

public class OrderService
{
    private readonly List<MockOrder> _orders = MockAccountData.CreateOrders();
    public event Action? OnChange;

    public IReadOnlyList<MockOrder> Orders => _orders;
    public int TotalCount => _orders.Count;
    public int ActiveCount => _orders.Count(o => o.IsActive);

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
        OnChange?.Invoke();
    }
}

public class NotificationService
{
    private readonly List<MockNotification> _items = MockAccountData.CreateNotifications();
    public event Action? OnChange;

    public IReadOnlyList<MockNotification> Items => _items;
    public int UnreadCount => _items.Count(n => !n.IsRead);

    public void MarkAllAsRead()
    {
        if (UnreadCount == 0)
            return;

        foreach (var item in _items)
            item.IsRead = true;

        OnChange?.Invoke();
    }
}

public class AuthService
{
    private const string StorageKey = "nube-demo-user";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public event Action? OnChange;

    public MockUser? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;
    public string? Email => CurrentUser?.Email;
    public string Role => CurrentUser?.Role ?? "guest";
    public bool RememberMe => CurrentUser?.RememberMe ?? false;
    public string DisplayName => CurrentUser?.Name ?? "Guest";
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

    public void Login(string email, bool rememberMe = false)
    {
        CurrentUser = MockUser.FromEmail(email, rememberMe);
        OnChange?.Invoke();
    }

    public void Logout()
    {
        CurrentUser = null;
        OnChange?.Invoke();
    }

    public void UpdateProfile(string firstName, string lastName, string email, string phone, string studentId, string college, string address)
    {
        if (CurrentUser is null)
            return;

        CurrentUser.FirstName = firstName.Trim();
        CurrentUser.LastName = lastName.Trim();
        CurrentUser.Name = $"{CurrentUser.FirstName} {CurrentUser.LastName}".Trim();
        CurrentUser.Email = email.Trim();
        CurrentUser.Phone = phone.Trim();
        CurrentUser.StudentId = studentId.Trim();
        CurrentUser.College = college.Trim();
        CurrentUser.Address = address.Trim();
        OnChange?.Invoke();
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
            // Browser storage may be unavailable during prerender.
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

        try
        {
            CurrentUser = JsonSerializer.Deserialize<MockUser>(json, JsonOptions);
            if (CurrentUser is not null && string.IsNullOrWhiteSpace(CurrentUser.Email))
                CurrentUser = null;
        }
        catch (JsonException)
        {
            CurrentUser = null;
        }

        if (CurrentUser is not null)
        {
            NormalizeProfile(CurrentUser);
            OnChange?.Invoke();
        }
    }

    public async Task LogoutAsync(IJSRuntime js)
    {
        Logout();
        await ClearStorageAsync(js);
    }

    private static void NormalizeProfile(MockUser user)
    {
        if (string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(user.Name))
        {
            var parts = user.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            user.FirstName = parts.Length > 0 ? parts[0] : user.Name;
            user.LastName = parts.Length > 1 ? parts[1] : user.LastName;
        }

        if (string.IsNullOrWhiteSpace(user.Phone))
            user.Phone = "+63 912 345 6789";
        if (string.IsNullOrWhiteSpace(user.StudentId))
            user.StudentId = "2021-12345";
        if (string.IsNullOrWhiteSpace(user.College))
            user.College = "College of Business & Accountancy";
        if (string.IsNullOrWhiteSpace(user.Address))
            user.Address = "123 Sampaloc, Manila";
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
            // Browser storage may be unavailable during prerender.
        }
    }
}
