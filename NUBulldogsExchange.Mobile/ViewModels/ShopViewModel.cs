using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class ShopCategoryItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Key { get; init; } = "All";
    public string Label { get; init; } = "All";
    public string Icon { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextColor)));
        }
    }

    public Color BackgroundColor => IsSelected
        ? Color.FromArgb("#00205B")
        : Color.FromArgb("#F4F5F7");

    public Color TextColor => IsSelected
        ? Colors.White
        : Color.FromArgb("#334155");

    public string DisplayText => string.IsNullOrEmpty(Icon) ? Label : $"{Icon}  {Label}";

    public event PropertyChangedEventHandler? PropertyChanged;
}

[QueryProperty(nameof(CategoryId), "categoryId")]
[QueryProperty(nameof(CategoryId), "category")]
[QueryProperty(nameof(CategoryId), "id")]
public sealed class ShopViewModel : INotifyPropertyChanged, IQueryAttributable
{
    private readonly ProductCatalogService _catalog;
    private readonly CartService _cart;
    private readonly WishlistService _wishlist;
    private readonly AuthService _auth;
    private readonly ToastService _toast;
    private readonly IAppDatabase _db;
    private readonly AdminProductService _adminProducts;
    private readonly AdminCategoryService _adminCategories;

    private string _searchQuery = string.Empty;
    private string _selectedCategoryKey = "All";
    private string _sortMode = "Default";
    private string _filterMode = "All";
    private bool _isBusy;
    private int _cartCount;
    private string _productCountLabel = "0 products";
    private bool _hasProducts = true;

    public ShopViewModel(
        ProductCatalogService catalog,
        CartService cart,
        WishlistService wishlist,
        AuthService auth,
        ToastService toast,
        IAppDatabase db,
        AdminProductService adminProducts,
        AdminCategoryService adminCategories)
    {
        _catalog = catalog;
        _cart = cart;
        _wishlist = wishlist;
        _auth = auth;
        _toast = toast;
        _db = db;
        _adminProducts = adminProducts;
        _adminCategories = adminCategories;

        SelectCategoryCommand = new Command<ShopCategoryItem>(OnSelectCategory);
        OpenCartCommand = new Command(async () => await GoAsync("cart"));
        OpenFilterCommand = new Command(async () => await ShowFilterAsync());
        OpenSortCommand = new Command(async () => await ShowSortAsync());
        ClearFiltersCommand = new Command(ClearFilters);
        ToggleWishlistCommand = new Command<Product>(async p => await OnToggleWishlistAsync(p));
        AddToCartCommand = new Command<Product>(async p => await OnAddToCartAsync(p));
        OpenProductCommand = new Command<Product>(async p => await OnOpenProductAsync(p));

        _catalog.OnChange += OnCatalogChanged;
        _adminCategories.OnChange += OnCategoriesChanged;
        _cart.OnChange += OnCartChanged;
        _wishlist.OnChange += () => MainThread.BeginInvokeOnMainThread(ApplyFilters);
    }

    /// <summary>Set by the page so ActionSheets can be shown.</summary>
    public Page? HostPage { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<ShopCategoryItem> Categories { get; } = [];

    public string CategoryId
    {
        get => _selectedCategoryKey;
        set => SetSelectedCategory(value);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        string? categoryId = null;
        if (query.TryGetValue("categoryId", out var idObj) && idObj is string idStr)
            categoryId = idStr;
        else if (query.TryGetValue("category", out var catObj) && catObj is string catStr)
            categoryId = catStr;
        else if (query.TryGetValue("id", out var rawIdObj) && rawIdObj is string rawIdStr)
            categoryId = rawIdStr;

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            SetSelectedCategory(categoryId);
        }
    }

    public void SetSelectedCategory(string? categoryKeyOrId)
    {
        if (string.IsNullOrWhiteSpace(categoryKeyOrId))
            categoryKeyOrId = "All";

        _selectedCategoryKey = categoryKeyOrId;
        foreach (var cat in Categories)
        {
            cat.IsSelected = string.Equals(cat.Key, categoryKeyOrId, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(cat.Label, categoryKeyOrId, StringComparison.OrdinalIgnoreCase);
        }
        ApplyFilters();
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value ?? string.Empty))
                ApplyFilters();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

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

    public string ProductCountLabel
    {
        get => _productCountLabel;
        private set => SetField(ref _productCountLabel, value);
    }

    public bool HasProducts
    {
        get => _hasProducts;
        private set => SetField(ref _hasProducts, value);
    }

    public bool ShowEmpty => !HasProducts && !IsBusy;

    public ICommand SelectCategoryCommand { get; }
    public ICommand OpenCartCommand { get; }
    public ICommand OpenFilterCommand { get; }
    public ICommand OpenSortCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ToggleWishlistCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand OpenProductCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _adminCategories.EnsureLoadedAsync();
            await _adminProducts.EnsureLoadedAsync();
            await MobileCatalogSeeder.EnsureSampleProductsAsync(_db, _catalog);
            await _cart.RestoreAsync(_auth.Email);
            BuildCategories();
            ApplyFilters();
            CartCount = _cart.TotalCount;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmpty));
        }
    }

    public void Detach()
    {
        _catalog.OnChange -= OnCatalogChanged;
        _adminCategories.OnChange -= OnCategoriesChanged;
        _cart.OnChange -= OnCartChanged;
    }

    private void OnCatalogChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BuildCategories();
            ApplyFilters();
        });

    private void OnCategoriesChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BuildCategories();
            ApplyFilters();
        });

    private void OnCartChanged() =>
        MainThread.BeginInvokeOnMainThread(() => CartCount = _cart.TotalCount);

    private void BuildCategories()
    {
        Categories.Clear();
        Categories.Add(new ShopCategoryItem
        {
            Key = "All",
            Label = "All",
            Icon = string.Empty,
            IsSelected = string.Equals(_selectedCategoryKey, "All", StringComparison.OrdinalIgnoreCase)
        });

        foreach (var cat in _adminCategories.All.Where(c => c.IsActive))
        {
            var isSelected = string.Equals(_selectedCategoryKey, cat.Id, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(_selectedCategoryKey, cat.Name, StringComparison.OrdinalIgnoreCase);
            Categories.Add(new ShopCategoryItem
            {
                Key = cat.Id,
                Label = cat.Name,
                Icon = HomeViewModel.DeriveCategoryIcon(cat.Name),
                IsSelected = isSelected
            });
        }
    }

    private void OnSelectCategory(ShopCategoryItem? item)
    {
        if (item is null) return;
        SetSelectedCategory(item.Key);
    }

    private void ApplyFilters()
    {
        IEnumerable<Product> query = _catalog.Products;

        if (!string.IsNullOrWhiteSpace(_searchQuery))
            query = _catalog.Search(query, _searchQuery);

        if (!string.Equals(_selectedCategoryKey, "All", StringComparison.OrdinalIgnoreCase))
        {
            var matchedCategory = _adminCategories.All.FirstOrDefault(c =>
                c.Id.Equals(_selectedCategoryKey, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(_selectedCategoryKey, StringComparison.OrdinalIgnoreCase) ||
                c.Slug.Equals(_selectedCategoryKey, StringComparison.OrdinalIgnoreCase));

            var targetName = matchedCategory?.Name ?? _selectedCategoryKey;
            var targetId = matchedCategory?.Id ?? _selectedCategoryKey;
            var targetSlug = matchedCategory?.Slug ?? _selectedCategoryKey;

            query = query.Where(p =>
                p.Category.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Equals(targetId, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Equals(targetSlug, StringComparison.OrdinalIgnoreCase));
        }

        query = _filterMode switch
        {
            "In Stock" => query.Where(p => p.InStock && p.Stock > 0),
            "On Sale" => query.Where(p =>
                p.OriginalPrice is decimal o && o > p.Price ||
                string.Equals(p.Badge, "Sale", StringComparison.OrdinalIgnoreCase)),
            "Best Sellers" => query.Where(p =>
                p.IsBestSeller ||
                string.Equals(p.Badge, "Best Seller", StringComparison.OrdinalIgnoreCase)),
            _ => query
        };

        query = _sortMode switch
        {
            "Price: Low to High" => query.OrderBy(p => p.Price),
            "Price: High to Low" => query.OrderByDescending(p => p.Price),
            "Newest" => query.OrderByDescending(p => p.IsNewArrival || p.IsFreshDrop)
                .ThenByDescending(p => p.Id),
            "Best Selling" => query.OrderByDescending(p => p.Sold).ThenByDescending(p => p.Rating),
            "Highest Rated" => query.OrderByDescending(p => p.Rating).ThenByDescending(p => p.Sold),
            _ => query.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Name)
        };

        var list = query.ToList();
        Products.Clear();
        foreach (var product in list)
            Products.Add(product);

        ProductCountLabel = list.Count == 1 ? "1 product" : $"{list.Count} products";
        HasProducts = list.Count > 0;
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private async Task ShowFilterAsync()
    {
        var page = HostPage ?? Shell.Current;
        var choice = await page.DisplayActionSheetAsync(
            "Filter products",
            "Cancel",
            null,
            "All",
            "In Stock",
            "On Sale",
            "Best Sellers");

        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
            return;

        _filterMode = choice;
        ApplyFilters();
    }

    private async Task ShowSortAsync()
    {
        var page = HostPage ?? Shell.Current;
        var choice = await page.DisplayActionSheetAsync(
            "Sort products",
            "Cancel",
            null,
            "Default",
            "Price: Low to High",
            "Price: High to Low",
            "Newest",
            "Best Selling",
            "Highest Rated");

        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
            return;

        _sortMode = choice;
        ApplyFilters();
    }

    private void ClearFilters()
    {
        _searchQuery = string.Empty;
        _selectedCategoryKey = "All";
        _filterMode = "All";
        _sortMode = "Default";
        OnPropertyChanged(nameof(SearchQuery));
        foreach (var cat in Categories)
            cat.IsSelected = cat.Key == "All";
        ApplyFilters();
    }

    private async Task OnToggleWishlistAsync(Product? product)
    {
        if (product is null) return;
        _wishlist.Toggle(product.Id);
        try
        {
            if (!string.IsNullOrWhiteSpace(_auth.Email))
                await _db.SaveWishlistAsync(_auth.Email, _wishlist.Ids);
        }
        catch
        {
        }

        _toast.Show(_wishlist.Contains(product.Id)
            ? $"Saved {product.Name} to Wishlist."
            : $"Removed {product.Name} from Wishlist.");
        ApplyFilters();
    }

    private async Task OnOpenProductAsync(Product? product)
    {
        if (product is null) return;
        await GoAsync($"product?id={product.Id}");
    }

    private async Task OnAddToCartAsync(Product? product)
    {
        if (product is null) return;
        if (product.HasSizeVariants)
        {
            await OnOpenProductAsync(product);
            return;
        }

        _cart.Add(product, 1, product.Colors.FirstOrDefault(), null);
        await _cart.PersistAsync(_auth.Email);
        _toast.Show($"Added {product.Name} to cart!");
        CartCount = _cart.TotalCount;
    }

    private static async Task GoAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
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
