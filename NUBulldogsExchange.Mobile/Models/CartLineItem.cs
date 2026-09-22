using System.ComponentModel;
using System.Runtime.CompilerServices;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Models;

/// <summary>UI wrapper around a CartService line item (selection + display helpers).</summary>
public sealed class CartLineItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public CartLineItem(CartItem item, bool isSelected = true)
    {
        Item = item;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CartItem Item { get; }

    public string Key => Item.Key;

    public Product Product => Item.Product;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectionChanged;

    public string Name => Product.Name;

    public string PriceText => $"₱{Product.Price:N0}";

    public int Quantity => Item.Quantity;

    public string QuantityText => Quantity.ToString();

    public bool CanDecrease => Quantity > 1;

    public bool CanIncrease => Product.Stock <= 0 || Quantity < Product.Stock;

    public ImageSource DisplayImage => ProductImageHelper.FromProduct(Product);

    public string ImageUrl => Product.ImageUrl ?? string.Empty;

    public string VariantText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Item.SelectedSize))
                parts.Add($"Size: {Item.SelectedSize}");
            if (!string.IsNullOrWhiteSpace(Item.SelectedColor))
                parts.Add($"Color: {MockData.FormatColorName(Item.SelectedColor)}");
            return string.Join(" · ", parts);
        }
    }

    public bool HasVariant => !string.IsNullOrWhiteSpace(VariantText);

    public void NotifyQuantityChanged()
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(QuantityText));
        OnPropertyChanged(nameof(CanDecrease));
        OnPropertyChanged(nameof(CanIncrease));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
