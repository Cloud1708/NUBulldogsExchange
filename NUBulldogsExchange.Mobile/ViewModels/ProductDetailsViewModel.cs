using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class ProductSizeItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isAvailable = true;

    public string Size { get; init; } = string.Empty;
    public int? VariantId { get; init; }
    public int Stock { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BorderColor));
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(IndicatorGlyph));
        }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value) return;
            _isAvailable = value;
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(Opacity));
            OnPropertyChanged(nameof(TextColor));
        }
    }

    public Color BackgroundColor => IsSelected ? Color.FromArgb("#00205B") : Colors.White;
    public Color TextColor => IsSelected ? Colors.White : (IsAvailable ? Color.FromArgb("#1E293B") : Color.FromArgb("#94A3B8"));
    public Color BorderColor => IsSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#CBD5E1");
    public double StrokeThickness => IsSelected ? 2.0 : 1.0;
    public double Opacity => IsAvailable ? 1.0 : 0.45;
    public string IndicatorGlyph => IsSelected ? "✓" : string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ProductColorItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Name { get; init; } = string.Empty;
    public Color ColorValue { get; init; } = Color.FromArgb("#00205B");

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(RingStrokeColor));
            OnPropertyChanged(nameof(RingThickness));
        }
    }

    public Color RingStrokeColor => IsSelected ? Color.FromArgb("#00205B") : Colors.Transparent;
    public double RingThickness => IsSelected ? 2.5 : 0.0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[QueryProperty(nameof(ProductId), "id")]
public sealed class ProductDetailsViewModel : INotifyPropertyChanged
{
    private readonly ProductCatalogService _catalog;
    private readonly CartService _cart;
    private readonly WishlistService _wishlist;
    private readonly AuthService _auth;
    private readonly ToastService _toast;
    private readonly IAppDatabase _db;

    private int _productId;
    private Product? _product;
    private string _selectedSize = string.Empty;
    private string _selectedColor = string.Empty;
    private int? _selectedVariantId;
    private int _quantity = 1;
    private string _errorMessage = string.Empty;
    private int _currentImageIndex = 1;
    private bool _isWishlisted;
    private int _cartCount;

    // Accordions
    private bool _isDescriptionExpanded = true;
    private bool _isDetailsExpanded;
    private bool _isMaterialsExpanded;
    private bool _isSizeGuideExpanded;
    private bool _isShippingExpanded;
    private bool _isReturnsExpanded;

    public ProductDetailsViewModel(
        ProductCatalogService catalog,
        CartService cart,
        WishlistService wishlist,
        AuthService auth,
        ToastService toast,
        IAppDatabase db)
    {
        _catalog = catalog;
        _cart = cart;
        _wishlist = wishlist;
        _auth = auth;
        _toast = toast;
        _db = db;

        IncreaseCommand = new Command(IncreaseQuantity, () => AvailableStock > 0 && Quantity < AvailableStock);
        DecreaseCommand = new Command(DecreaseQuantity, () => Quantity > 1);
        SelectSizeCommand = new Command<ProductSizeItem>(OnSelectSizeItem);
        SelectColorCommand = new Command<ProductColorItem>(OnSelectColorItem);
        AddToCartCommand = new Command(async () => await AddToCartAsync(), () => Product is not null && AvailableStock > 0);
        BuyNowCommand = new Command(async () => await BuyNowAsync(), () => Product is not null && AvailableStock > 0);
        ToggleWishlistCommand = new Command(async () => await OnToggleWishlistAsync());
        ShareCommand = new Command(async () => await OnShareAsync());
        OpenCartCommand = new Command(async () => await GoAsync("cart"));
        OpenSizeGuideCommand = new Command(OpenSizeGuide);
        ViewAllReviewsCommand = new Command(async () => await ShowReviewsModalAsync());
        BackCommand = new Command(async () => await GoBackAsync());

        ToggleDescriptionCommand = new Command(() => IsDescriptionExpanded = !IsDescriptionExpanded);
        ToggleDetailsCommand = new Command(() => IsDetailsExpanded = !IsDetailsExpanded);
        ToggleMaterialsCommand = new Command(() => IsMaterialsExpanded = !IsMaterialsExpanded);
        ToggleSizeGuideCommand = new Command(() => IsSizeGuideExpanded = !IsSizeGuideExpanded);
        ToggleShippingCommand = new Command(() => IsShippingExpanded = !IsShippingExpanded);
        ToggleReturnsCommand = new Command(() => IsReturnsExpanded = !IsReturnsExpanded);

        NextImageCommand = new Command(NextImage);
        PrevImageCommand = new Command(PrevImage);

        _cart.OnChange += OnCartChanged;
        CartCount = _cart.TotalCount;
    }

    public Page? HostPage { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProductSizeItem> SizeList { get; } = [];
    public ObservableCollection<ProductColorItem> ColorList { get; } = [];
    public ObservableCollection<string> GalleryImages { get; } = [];

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
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Badge));
                OnPropertyChanged(nameof(HasBadge));
                OnPropertyChanged(nameof(RatingText));
                OnPropertyChanged(nameof(ReviewsText));
                OnPropertyChanged(nameof(SoldText));
                OnPropertyChanged(nameof(PriceFormatted));
                OnPropertyChanged(nameof(OriginalPriceFormatted));
                OnPropertyChanged(nameof(HasOriginalPrice));
                OnPropertyChanged(nameof(DisplayImage));
                OnPropertyChanged(nameof(DescriptionText));
                OnPropertyChanged(nameof(DetailsText));
                OnPropertyChanged(nameof(MaterialsText));
                OnPropertyChanged(nameof(SizeGuideText));
                OnPropertyChanged(nameof(ShippingText));
                OnPropertyChanged(nameof(ReturnsText));
                OnPropertyChanged(nameof(HasMultipleImages));
                OnPropertyChanged(nameof(ImageCounterText));
                OnPropertyChanged(nameof(IsInStock));
                OnPropertyChanged(nameof(StockStatusText));
                OnPropertyChanged(nameof(StockStatusColor));
                OnPropertyChanged(nameof(StockRemainingText));
                OnPropertyChanged(nameof(StockStatusNote));
                OnPropertyChanged(nameof(Star5Percent));
                OnPropertyChanged(nameof(Star4Percent));
                OnPropertyChanged(nameof(Star3Percent));
                OnPropertyChanged(nameof(Star2Percent));
                OnPropertyChanged(nameof(Star1Percent));
                RefreshCommands();
            }
        }
    }

    public string Name => Product?.Name ?? "NU Product";
    public string Badge => Product?.Badge ?? (Product?.IsBestSeller == true ? "Best Seller" : (Product?.IsFreshDrop == true ? "Fresh Drop" : (Product?.IsNewArrival == true ? "New" : string.Empty)));
    public bool HasBadge => !string.IsNullOrWhiteSpace(Badge);
    public string RatingText => Product is null ? "4.7" : $"{Product.Rating:0.0}";
    public string ReviewsText => $"{Product?.Reviews ?? 89} Reviews";
    public string SoldText => $"{Product?.Sold ?? 342} Sold";
    public string PriceFormatted => Product is null ? "₱0" : $"₱{Product.Price:N0}";
    public string OriginalPriceFormatted => Product?.OriginalPrice is decimal o ? $"₱{o:N0}" : string.Empty;
    public bool HasOriginalPrice => Product?.OriginalPrice is decimal o && o > Product.Price;
    public ImageSource DisplayImage => ProductImageHelper.FromUrl(SelectedImage);

    public string SelectedImage
    {
        get
        {
            if (GalleryImages.Count == 0) return Product?.ImageUrl ?? string.Empty;
            var idx = Math.Clamp(CurrentImageIndex - 1, 0, GalleryImages.Count - 1);
            return GalleryImages[idx];
        }
    }

    public int CurrentImageIndex
    {
        get => _currentImageIndex;
        set
        {
            if (SetField(ref _currentImageIndex, value))
            {
                OnPropertyChanged(nameof(ImageCounterText));
                OnPropertyChanged(nameof(SelectedImage));
                OnPropertyChanged(nameof(DisplayImage));
                OnPropertyChanged(nameof(IsDot1Active));
                OnPropertyChanged(nameof(IsDot2Active));
                OnPropertyChanged(nameof(IsDot3Active));
                OnPropertyChanged(nameof(IsDot4Active));
            }
        }
    }

    public int TotalImages => Math.Max(1, GalleryImages.Count);
    public string ImageCounterText => $"{CurrentImageIndex} / {TotalImages}";
    public bool HasMultipleImages => GalleryImages.Count > 1;

    public bool IsDot1Active => CurrentImageIndex == 1;
    public bool IsDot2Active => CurrentImageIndex == 2;
    public bool IsDot3Active => CurrentImageIndex == 3;
    public bool IsDot4Active => CurrentImageIndex >= 4;

    public bool IsWishlisted
    {
        get => _isWishlisted;
        set
        {
            if (SetField(ref _isWishlisted, value))
            {
                OnPropertyChanged(nameof(WishlistGlyph));
                OnPropertyChanged(nameof(WishlistColor));
            }
        }
    }

    public string WishlistGlyph => IsWishlisted ? "♥" : "♡";
    public Color WishlistColor => IsWishlisted ? Color.FromArgb("#EF4444") : Color.FromArgb("#1E293B");

    public int CartCount
    {
        get => _cartCount;
        private set
        {
            if (SetField(ref _cartCount, value))
                OnPropertyChanged(nameof(HasCartItems));
        }
    }

    public bool HasCartItems => CartCount > 0;

    public bool HasColors => ColorList.Count > 0;
    public bool HasSizes => SizeList.Count > 0;

    public string SelectedSize
    {
        get => _selectedSize;
        private set
        {
            if (SetField(ref _selectedSize, value))
            {
                OnPropertyChanged(nameof(HasSelectedSize));
                OnPropertyChanged(nameof(SelectedSizeTitle));
                OnPropertyChanged(nameof(SelectedSizeHint));
                OnPropertyChanged(nameof(StockStatusNote));
            }
        }
    }

    public bool HasSelectedSize => !string.IsNullOrWhiteSpace(SelectedSize);
    public string SelectedSizeTitle => HasSelectedSize ? $"Selected: {SelectedSize}" : "Choose Size";
    public string SelectedSizeHint => HasSelectedSize
        ? $"Size {SelectedSize} selected • {AvailableStock} pieces available"
        : string.Empty;

    public string SelectedColor
    {
        get => _selectedColor;
        private set => SetField(ref _selectedColor, value);
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

    public int AvailableStock
    {
        get
        {
            if (Product is null) return 0;
            if (Product.HasSizeVariants && _selectedVariantId is int vid)
            {
                var variant = Product.Variants.FirstOrDefault(v => v.Id == vid);
                if (variant is not null)
                    return Math.Max(0, variant.StockQuantity);
            }
            if (Product.HasSizeVariants && !string.IsNullOrWhiteSpace(_selectedSize))
            {
                var variant = Product.Variants.FirstOrDefault(v => v.Size.Equals(_selectedSize, StringComparison.OrdinalIgnoreCase));
                if (variant is not null)
                    return Math.Max(0, variant.StockQuantity);
            }
            if (Product.HasSizeVariants)
            {
                return Product.Variants.Where(v => v.IsActive).Sum(v => Math.Max(0, v.StockQuantity));
            }
            return Math.Max(0, Product.Stock);
        }
    }

    public bool IsInStock => AvailableStock > 0;
    public string StockStatusText => IsInStock ? "In Stock" : "Out of Stock";
    public Color StockStatusColor => IsInStock ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
    public string StockRemainingText => $"{AvailableStock} items remaining";
    public string StockStatusNote => HasSelectedSize
        ? $"({AvailableStock} available for size {SelectedSize})"
        : $"({AvailableStock} available in stock)";

    // Accordions state
    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        set
        {
            if (SetField(ref _isDescriptionExpanded, value))
                OnPropertyChanged(nameof(DescriptionGlyph));
        }
    }

    public bool IsDetailsExpanded
    {
        get => _isDetailsExpanded;
        set
        {
            if (SetField(ref _isDetailsExpanded, value))
                OnPropertyChanged(nameof(DetailsGlyph));
        }
    }

    public bool IsMaterialsExpanded
    {
        get => _isMaterialsExpanded;
        set
        {
            if (SetField(ref _isMaterialsExpanded, value))
                OnPropertyChanged(nameof(MaterialsGlyph));
        }
    }

    public bool IsSizeGuideExpanded
    {
        get => _isSizeGuideExpanded;
        set
        {
            if (SetField(ref _isSizeGuideExpanded, value))
                OnPropertyChanged(nameof(SizeGuideGlyph));
        }
    }

    public bool IsShippingExpanded
    {
        get => _isShippingExpanded;
        set
        {
            if (SetField(ref _isShippingExpanded, value))
                OnPropertyChanged(nameof(ShippingGlyph));
        }
    }

    public bool IsReturnsExpanded
    {
        get => _isReturnsExpanded;
        set
        {
            if (SetField(ref _isReturnsExpanded, value))
                OnPropertyChanged(nameof(ReturnsGlyph));
        }
    }

    public string DescriptionGlyph => IsDescriptionExpanded ? "▲" : "▼";
    public string DetailsGlyph => IsDetailsExpanded ? "▲" : "▼";
    public string MaterialsGlyph => IsMaterialsExpanded ? "▲" : "▼";
    public string SizeGuideGlyph => IsSizeGuideExpanded ? "▲" : "▼";
    public string ShippingGlyph => IsShippingExpanded ? "▲" : "▼";
    public string ReturnsGlyph => IsReturnsExpanded ? "▲" : "▼";

    // Dynamic Accordion Texts
    public string DescriptionText =>
        !string.IsNullOrWhiteSpace(Product?.FullDescription)
            ? Product.FullDescription
            : (!string.IsNullOrWhiteSpace(Product?.Description)
                ? Product.Description
                : "The NU Bulldogs Classic Shirt is a staple for every Bulldog fan. Made from premium cotton blend for all-day campus comfort.");

    public string DetailsText =>
        $"• SKU: {Product?.Sku ?? "NUBE-001"}\n• Category: {Product?.Category ?? "Apparel"}\n• Fit: Regular Campus Fit\n• Gender: Unisex\n• Official NU Bulldogs licensed campus merchandise.";

    public string MaterialsText =>
        $"• Material: {(!string.IsNullOrWhiteSpace(Product?.Material) ? Product.Material : "100% Premium Combed Cotton")}\n• Machine wash cold with like colors\n• Do not bleach\n• Tumble dry low or hang dry\n• Cool iron if needed, avoid direct heat on print.";

    public string SizeGuideText =>
        "• XS: Chest 34-36\", Length 26\"\n• S: Chest 36-38\", Length 27\"\n• M: Chest 38-40\", Length 28\"\n• L: Chest 40-42\", Length 29\"\n• XL: Chest 42-44\", Length 30\"\n• XXL: Chest 44-46\", Length 31\"";

    public string ShippingText =>
        "• Pickup: Free pickup at NU Merchandise Desk (Main Campus).\n• Campus Delivery: 2-4 business days within Metro Manila campus hubs.\n• Real-time order tracking available in My Orders.";

    public string ReturnsText =>
        "• 7-day return and exchange policy for defective items or size adjustments.\n• Item must be unwashed, unworn, and with original tags attached.\n• Present order confirmation receipt at the Merchandise Desk.";

    // Rating breakdown percentages
    public double Star5Percent => 0.75;
    public double Star4Percent => 0.15;
    public double Star3Percent => 0.06;
    public double Star2Percent => 0.02;
    public double Star1Percent => 0.02;

    // Commands
    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand SelectSizeCommand { get; }
    public ICommand SelectColorCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand BuyNowCommand { get; }
    public ICommand ToggleWishlistCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand OpenCartCommand { get; }
    public ICommand OpenSizeGuideCommand { get; }
    public ICommand ViewAllReviewsCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ToggleDescriptionCommand { get; }
    public ICommand ToggleDetailsCommand { get; }
    public ICommand ToggleMaterialsCommand { get; }
    public ICommand ToggleSizeGuideCommand { get; }
    public ICommand ToggleShippingCommand { get; }
    public ICommand ToggleReturnsCommand { get; }
    public ICommand NextImageCommand { get; }
    public ICommand PrevImageCommand { get; }

    public async Task LoadAsync()
    {
        await _catalog.EnsureLoadedAsync();
        Product = _catalog.GetById(_productId);
        if (Product is null)
        {
            ErrorMessage = "Product not found.";
            return;
        }

        ErrorMessage = string.Empty;
        Quantity = 1;
        CurrentImageIndex = 1;

        // Wishlist status
        IsWishlisted = _wishlist.Contains(Product.Id);

        // Populate Gallery Images
        GalleryImages.Clear();
        foreach (var img in Product.GalleryImages)
        {
            if (!string.IsNullOrWhiteSpace(img))
                GalleryImages.Add(img);
        }

        if (GalleryImages.Count == 0 && !string.IsNullOrWhiteSpace(Product.ImageUrl))
            GalleryImages.Add(Product.ImageUrl);

        // If only 1 image, duplicate so gallery can swipe/demo
        if (GalleryImages.Count == 1 && !string.IsNullOrWhiteSpace(GalleryImages[0]))
        {
            var baseImg = GalleryImages[0];
            GalleryImages.Add(baseImg);
            GalleryImages.Add(baseImg);
            GalleryImages.Add(baseImg);
        }

        // Populate Sizes
        SizeList.Clear();
        _selectedVariantId = null;
        _selectedSize = string.Empty;

        if (Product.HasSizeVariants)
        {
            foreach (var variant in Product.Variants.Where(v => v.IsActive))
            {
                SizeList.Add(new ProductSizeItem
                {
                    Size = variant.Size,
                    VariantId = variant.Id,
                    Stock = variant.StockQuantity,
                    IsAvailable = variant.StockQuantity > 0,
                    IsSelected = false
                });
            }
        }
        else if (Product.Sizes.Count > 0)
        {
            foreach (var s in Product.Sizes)
            {
                SizeList.Add(new ProductSizeItem
                {
                    Size = s,
                    Stock = Product.Stock,
                    IsAvailable = Product.Stock > 0,
                    IsSelected = false
                });
            }
        }
        else if (IsApparel(Product))
        {
            // Default sizes for apparel/clothing (XS, S, M, L, XL, XXL)
            var defaultSizes = new[] { "XS", "S", "M", "L", "XL", "XXL" };
            foreach (var s in defaultSizes)
            {
                SizeList.Add(new ProductSizeItem
                {
                    Size = s,
                    Stock = Product.Stock,
                    IsAvailable = Product.Stock > 0,
                    IsSelected = false
                });
            }
        }

        // Auto select first available size (preferably "M" or first in stock)
        var preselect = SizeList.FirstOrDefault(s => s.Size.Equals("M", StringComparison.OrdinalIgnoreCase) && s.IsAvailable)
                     ?? SizeList.FirstOrDefault(s => s.IsAvailable);
        if (preselect is not null)
            OnSelectSizeItem(preselect);

        // Populate Colors
        ColorList.Clear();
        var colors = Product.Colors.Count > 0 ? Product.Colors : ["Navy", "Black"];
        foreach (var c in colors)
        {
            ColorList.Add(new ProductColorItem
            {
                Name = c,
                ColorValue = ParseColor(c),
                IsSelected = false
            });
        }
        if (ColorList.Count > 0)
            OnSelectColorItem(ColorList[0]);

        OnPropertyChanged(nameof(HasSizes));
        OnPropertyChanged(nameof(HasColors));
        OnPropertyChanged(nameof(SelectedImage));
        OnPropertyChanged(nameof(DisplayImage));
        OnPropertyChanged(nameof(ImageCounterText));
        OnPropertyChanged(nameof(TotalImages));
        OnPropertyChanged(nameof(AvailableStock));
        OnPropertyChanged(nameof(StockRemainingText));
        OnPropertyChanged(nameof(StockStatusNote));
        OnPropertyChanged(nameof(SelectedSizeHint));
        OnPropertyChanged(nameof(SelectedSizeTitle));
        OnPropertyChanged(nameof(HasSelectedSize));
        OnPropertyChanged(nameof(IsInStock));
        OnPropertyChanged(nameof(StockStatusText));
        OnPropertyChanged(nameof(StockStatusColor));
        RefreshCommands();
    }

    private void OnSelectSizeItem(ProductSizeItem? item)
    {
        if (item is null || !item.IsAvailable) return;

        foreach (var s in SizeList)
            s.IsSelected = (s == item);

        _selectedVariantId = item.VariantId;
        SelectedSize = item.Size;
        ErrorMessage = string.Empty;

        if (Quantity > AvailableStock && AvailableStock > 0)
            Quantity = AvailableStock;

        OnPropertyChanged(nameof(AvailableStock));
        OnPropertyChanged(nameof(StockRemainingText));
        OnPropertyChanged(nameof(StockStatusNote));
        OnPropertyChanged(nameof(SelectedSizeHint));
        OnPropertyChanged(nameof(SelectedSizeTitle));
        OnPropertyChanged(nameof(HasSelectedSize));
        OnPropertyChanged(nameof(IsInStock));
        OnPropertyChanged(nameof(StockStatusText));
        OnPropertyChanged(nameof(StockStatusColor));
        RefreshCommands();
    }

    private void OnSelectColorItem(ProductColorItem? item)
    {
        if (item is null) return;
        foreach (var c in ColorList)
            c.IsSelected = (c == item);
        SelectedColor = item.Name;
    }

    private void IncreaseQuantity()
    {
        if (Quantity < AvailableStock)
            Quantity++;
    }

    private void DecreaseQuantity()
    {
        if (Quantity > 1)
            Quantity--;
    }

    private void NextImage()
    {
        if (GalleryImages.Count <= 1) return;
        CurrentImageIndex = CurrentImageIndex >= GalleryImages.Count ? 1 : CurrentImageIndex + 1;
    }

    private void PrevImage()
    {
        if (GalleryImages.Count <= 1) return;
        CurrentImageIndex = CurrentImageIndex <= 1 ? GalleryImages.Count : CurrentImageIndex - 1;
    }

    private async Task OnToggleWishlistAsync()
    {
        if (Product is null) return;
        _wishlist.Toggle(Product.Id);
        IsWishlisted = _wishlist.Contains(Product.Id);

        try
        {
            if (!string.IsNullOrWhiteSpace(_auth.Email))
                await _db.SaveWishlistAsync(_auth.Email, _wishlist.Ids);
        }
        catch { }

        _toast.Show(IsWishlisted ? $"Saved {Product.Name} to Wishlist." : $"Removed {Product.Name} from Wishlist.");
    }

    private async Task OnShareAsync()
    {
        if (Product is null) return;
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = Product.Name,
                Text = $"Check out {Product.Name} (₱{Product.Price:N0}) on NU Bulldogs Exchange!",
                Uri = "https://nubulldogsexchange.edu.ph"
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void OpenSizeGuide()
    {
        IsSizeGuideExpanded = true;
        _toast.Show("Size Guide opened below.");
    }

    private async Task ShowReviewsModalAsync()
    {
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync("Ratings & Reviews", $"Overall: {RatingText} ⭐ ({ReviewsText})\n\n100% of reviews are from verified NU students and staff.", "OK");
    }

    private async Task AddToCartAsync()
    {
        if (Product is null) return;

        if (HasSizes && string.IsNullOrWhiteSpace(SelectedSize))
        {
            ErrorMessage = "Please select a size.";
            return;
        }

        if (AvailableStock <= 0)
        {
            ErrorMessage = "This item is currently out of stock.";
            return;
        }

        _cart.Add(Product, Quantity, SelectedColor, SelectedSize, _selectedVariantId);
        await _cart.PersistAsync(_auth.Email);
        _toast.Show($"Added {Quantity}x {Product.Name} ({SelectedSize}) to cart!");
        CartCount = _cart.TotalCount;
    }

    private async Task BuyNowAsync()
    {
        if (Product is null) return;

        if (HasSizes && string.IsNullOrWhiteSpace(SelectedSize))
        {
            ErrorMessage = "Please select a size.";
            return;
        }

        if (AvailableStock <= 0)
        {
            ErrorMessage = "This item is currently out of stock.";
            return;
        }

        _cart.Add(Product, Quantity, SelectedColor, SelectedSize, _selectedVariantId);
        await _cart.PersistAsync(_auth.Email);
        CartCount = _cart.TotalCount;
        await GoAsync("checkout");
    }

    private void OnCartChanged() =>
        MainThread.BeginInvokeOnMainThread(() => CartCount = _cart.TotalCount);

    private static async Task GoAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//shop");
        }
        catch
        {
            await Shell.Current.GoToAsync("//shop");
        }
    }

    private static bool IsApparel(Product product)
    {
        if (product.HasSizeVariants || product.Sizes.Count > 0) return true;
        if (string.Equals(product.Section, "apparel", StringComparison.OrdinalIgnoreCase)) return true;

        var name = product.Name ?? string.Empty;
        var cat = product.Category ?? string.Empty;
        var combined = $"{name} {cat}".ToLowerInvariant();

        var nonApparelKeywords = new[] { "tumbler", "bottle", "mug", "bag", "backpack", "totebag", "tote bag", "sticker", "pen", "notebook", "lanyard", "keychain", "umbrella" };
        if (nonApparelKeywords.Any(k => combined.Contains(k)))
        {
            var apparelKeywords = new[] { "shirt", "polo", "hoodie", "jacket", "jersey", "uniform", "tee", "sweatshirt", "pants", "shorts", "jogger", "cardigan", "top" };
            return apparelKeywords.Any(k => combined.Contains(k));
        }

        return true;
    }

    private static Color ParseColor(string name) => name.Trim().ToLowerInvariant() switch
    {
        "navy" or "blue" or "nu navy" => Color.FromArgb("#00205B"),
        "black" or "charcoal" => Color.FromArgb("#1E293B"),
        "gold" or "nu gold" or "yellow" => Color.FromArgb("#F9C424"),
        "white" => Color.FromArgb("#FFFFFF"),
        "gray" or "grey" => Color.FromArgb("#94A3B8"),
        "red" or "maroon" => Color.FromArgb("#DC2626"),
        "green" => Color.FromArgb("#16A34A"),
        _ => Color.FromArgb("#00205B")
    };

    private void RefreshCommands()
    {
        ((Command)IncreaseCommand).ChangeCanExecute();
        ((Command)DecreaseCommand).ChangeCanExecute();
        ((Command)AddToCartCommand).ChangeCanExecute();
        ((Command)BuyNowCommand).ChangeCanExecute();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
