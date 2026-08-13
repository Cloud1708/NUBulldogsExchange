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

        if (quantity <= 0)
            _items.Remove(item);
        else
            item.Quantity = quantity;

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
}

public class ToastService
{
    public event Action<string>? OnShow;

    public void Show(string message) => OnShow?.Invoke(message);
}

public class AuthService
{
    public event Action? OnChange;

    public bool IsLoggedIn { get; private set; }
    public string? Email { get; private set; }
    public string Role { get; private set; } = "guest";
    public bool RememberMe { get; private set; }

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Email))
                return "Guest";

            var local = Email.Split('@')[0];
            if (string.IsNullOrWhiteSpace(local))
                return Email;

            return char.ToUpperInvariant(local[0]) + local[1..];
        }
    }

    public void Login(string email, bool rememberMe = false)
    {
        IsLoggedIn = true;
        Email = email.Trim();
        RememberMe = rememberMe;
        Role = string.Equals(Email, "admin@nu.edu", StringComparison.OrdinalIgnoreCase)
            ? "admin"
            : "customer";
        OnChange?.Invoke();
    }

    public void Logout()
    {
        IsLoggedIn = false;
        Email = null;
        RememberMe = false;
        Role = "guest";
        OnChange?.Invoke();
    }
}
