using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

[QueryProperty(nameof(ProductId), "id")]
public sealed class ProductDetailsViewModel : INotifyPropertyChanged
{
    private readonly ProductCatalogService _catalog;
    private readonly CartService _cart;
    private readonly ToastService _toast;

    private int _productId;
    private Product? _product;
    private string _selectedSize = string.Empty;
    private int? _selectedVariantId;
    private int _quantity = 1;
    private string _errorMessage = string.Empty;
    private string _stockHint = string.Empty;

    public ProductDetailsViewModel(ProductCatalogService catalog, CartService cart, ToastService toast)
    {
        _catalog = catalog;
        _cart = cart;
        _toast = toast;

        IncreaseCommand = new Command(() =>
        {
            if (Quantity < AvailableStock)
                Quantity++;
        }, () => Product is not null && Quantity < AvailableStock);

        DecreaseCommand = new Command(() =>
        {
            if (Quantity > 1)
                Quantity--;
        }, () => Quantity > 1);

        SelectSizeCommand = new Command<ProductVariant>(OnSelectSize);
        AddToCartCommand = new Command(async () => await AddToCartAsync(), () => Product is not null);
        BackCommand = new Command(async () =>
        {
            try { await Shell.Current.GoToAsync(".."); }
            catch { /* ignore */ }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProductId
    {
        get => _productId.ToString();
        set
        {
            if (int.TryParse(value, out var id))
            {
                _productId = id;
                _ = LoadAsync();
            }
        }
    }

    public Product? Product
    {
        get => _product;
        private set
        {
            if (SetField(ref _product, value))
            {
                OnPropertyChanged(nameof(HasSizeVariants));
                OnPropertyChanged(nameof(SizeVariants));
                OnPropertyChanged(nameof(PriceLabel));
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(ImageUrl));
                OnPropertyChanged(nameof(DisplayImage));
                RefreshCommands();
            }
        }
    }

    public string Name => Product?.Name ?? string.Empty;
    public string ImageUrl => Product?.ImageUrl ?? string.Empty;
    public ImageSource DisplayImage => ProductImageHelper.FromProduct(Product);
    public string PriceLabel => Product is null ? string.Empty : $"₱{Product.Price:N0}";
    public bool HasSizeVariants => Product?.HasSizeVariants == true;
    public IEnumerable<ProductVariant> SizeVariants =>
        Product?.Variants.Where(v => v.IsActive) ?? Enumerable.Empty<ProductVariant>();

    public string SelectedSize
    {
        get => _selectedSize;
        private set => SetField(ref _selectedSize, value);
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetField(ref _quantity, Math.Max(1, value)))
                RefreshCommands();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string StockHint
    {
        get => _stockHint;
        private set => SetField(ref _stockHint, value);
    }

    public int AvailableStock
    {
        get
        {
            if (Product is null) return 0;
            if (HasSizeVariants)
            {
                if (_selectedVariantId is int id)
                    return Product.Variants.FirstOrDefault(v => v.Id == id)?.StockQuantity ?? 0;
                return Product.Variants.Where(v => v.IsActive).Sum(v => Math.Max(0, v.StockQuantity));
            }

            return Math.Max(0, Product.Stock);
        }
    }

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand SelectSizeCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand BackCommand { get; }

    private async Task LoadAsync()
    {
        await _catalog.EnsureLoadedAsync();
        Product = _catalog.GetById(_productId);
        if (Product is null)
        {
            ErrorMessage = "Product not found.";
            return;
        }

        ErrorMessage = string.Empty;
        _selectedVariantId = null;
        SelectedSize = string.Empty;
        Quantity = 1;

        if (HasSizeVariants)
        {
            var first = Product.Variants.FirstOrDefault(v => v.IsActive && v.StockQuantity > 0);
            if (first is not null)
                OnSelectSize(first);
        }

        UpdateStockHint();
        RefreshCommands();
    }

    private void OnSelectSize(ProductVariant? variant)
    {
        if (variant is null || variant.StockQuantity <= 0)
            return;

        _selectedVariantId = variant.Id;
        SelectedSize = variant.Size;
        ErrorMessage = string.Empty;
        if (Quantity > variant.StockQuantity)
            Quantity = variant.StockQuantity;
        UpdateStockHint();
        RefreshCommands();
        OnPropertyChanged(nameof(AvailableStock));
    }

    private async Task AddToCartAsync()
    {
        if (Product is null) return;

        if (HasSizeVariants)
        {
            if (_selectedVariantId is null)
            {
                ErrorMessage = "Please select a size.";
                return;
            }

            var variant = Product.Variants.FirstOrDefault(v => v.Id == _selectedVariantId);
            if (variant is null || variant.StockQuantity < Quantity)
            {
                ErrorMessage = "Not enough stock for the selected size.";
                return;
            }
        }

        if (AvailableStock <= 0)
        {
            ErrorMessage = "This product is out of stock.";
            return;
        }

        _cart.Add(Product, Quantity, Product.Colors.FirstOrDefault(), SelectedSize, _selectedVariantId);
        _toast.Show("Product added to cart.");
        try { await Shell.Current.GoToAsync("cart"); }
        catch { /* ignore */ }
    }

    private void UpdateStockHint() =>
        StockHint = $"{AvailableStock} pieces available";

    private void RefreshCommands()
    {
        ((Command)IncreaseCommand).ChangeCanExecute();
        ((Command)DecreaseCommand).ChangeCanExecute();
        ((Command)AddToCartCommand).ChangeCanExecute();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
