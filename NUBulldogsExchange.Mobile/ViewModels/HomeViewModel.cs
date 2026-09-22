using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Models;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class HomeViewModel : INotifyPropertyChanged
{
    private readonly ProductCatalogService _catalog;
    private readonly AuthService _auth;
    private readonly CartService _cart;
    private readonly WishlistService _wishlist;
    private readonly NotificationService _notifications;
    private readonly ToastService _toast;
    private readonly IAppDatabase _db;
    private readonly AdminProductService _adminProducts;

    private string _greetingTitle = "Welcome! 👋";
    private string _greetingSubtitle = "Browse NU Bulldogs merchandise.";
    private string _searchQuery = string.Empty;
    private bool _isBusy;
    private int _cartCount;
    private int _notificationCount;
    private string? _statusMessage;
    private bool _hasFeatured;
    private bool _hasFresh;

    public HomeViewModel(
        ProductCatalogService catalog,
        AuthService auth,
        CartService cart,
        WishlistService wishlist,
        NotificationService notifications,
        ToastService toast,
        IAppDatabase db,
        AdminProductService adminProducts)
    {
        _catalog = catalog;
        _auth = auth;
        _cart = cart;
        _wishlist = wishlist;
        _notifications = notifications;
        _toast = toast;
        _db = db;
        _adminProducts = adminProducts;

        RefreshCommand = new Command(async () => await LoadAsync());
        SearchCommand = new Command(OnSearch);
        OpenShopCommand = new Command(async () => await GoAsync("shop"));
        OpenCartCommand = new Command(async () => await GoAsync("cart"));
        OpenNotificationsCommand = new Command(async () => await GoAsync("account"));
        OpenCategoryCommand = new Command<CategoryChip>(async c => await OnCategoryAsync(c));
        OpenProductCommand = new Command<Product>(async p => await OnProductAsync(p));
        ToggleWishlistCommand = new Command<Product>(async p => await OnToggleWishlistAsync(p));
        AddToCartCommand = new Command<Product>(async p => await OnAddToCartAsync(p));
        ShopNowCommand = new Command<HeroBannerItem>(async h => await OnHeroAsync(h));
        ExploreCollectionCommand = new Command(async () => await GoAsync("shop"));
        SeeAllFeaturedCommand = new Command(async () => await GoAsync("shop"));
        SeeAllFreshCommand = new Command(async () => await GoAsync("shop"));
        SeeAllFavoritesCommand = new Command(async () => await GoAsync("shop"));
        SeeAllCategoriesCommand = new Command(async () => await GoAsync("shop"));

        _catalog.OnChange += OnServicesChanged;
        _auth.OnChange += OnServicesChanged;
        _cart.OnChange += OnServicesChanged;
        _wishlist.OnChange += OnServicesChanged;
        _notifications.OnChange += OnServicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryChip> Categories { get; } = [];
    public ObservableCollection<HeroBannerItem> HeroBanners { get; } = [];
    public ObservableCollection<ProductPair> FeaturedRows { get; } = [];
    public ObservableCollection<Product> FreshDrops { get; } = [];
    public ObservableCollection<ProductPair> FavoriteRows { get; } = [];

    public string GreetingTitle
    {
        get => _greetingTitle;
        private set => SetField(ref _greetingTitle, value);
    }

    public string GreetingSubtitle
    {
        get => _greetingSubtitle;
        private set => SetField(ref _greetingSubtitle, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetField(ref _searchQuery, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public int CartCount
    {
        get => _cartCount;
        private set => SetField(ref _cartCount, value);
    }

    public int NotificationCount
    {
        get => _notificationCount;
        private set => SetField(ref _notificationCount, value);
    }

    public bool HasNotifications => NotificationCount > 0;
    public bool HasCartItems => CartCount > 0;

    public bool HasFeatured
    {
        get => _hasFeatured;
        private set => SetField(ref _hasFeatured, value);
    }

    public bool HasFresh
    {
        get => _hasFresh;
        private set => SetField(ref _hasFresh, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand OpenShopCommand { get; }
    public ICommand OpenCartCommand { get; }
    public ICommand OpenNotificationsCommand { get; }
    public ICommand OpenCategoryCommand { get; }
    public ICommand OpenProductCommand { get; }
    public ICommand ToggleWishlistCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand ShopNowCommand { get; }
    public ICommand ExploreCollectionCommand { get; }
    public ICommand SeeAllFeaturedCommand { get; }
    public ICommand SeeAllFreshCommand { get; }
    public ICommand SeeAllFavoritesCommand { get; }
    public ICommand SeeAllCategoriesCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await _adminProducts.EnsureLoadedAsync();
            await MobileCatalogSeeder.EnsureSampleProductsAsync(_db, _catalog);
            await _cart.RestoreAsync(_auth.Email);
            await _notifications.EnsureLoadedAsync(_auth.Email);

            if (!string.IsNullOrWhiteSpace(_auth.Email))
            {
                try
                {
                    var ids = await _db.GetWishlistAsync(_auth.Email);
                    foreach (var id in ids)
                    {
                        if (!_wishlist.Contains(id))
                            _wishlist.Toggle(id);
                    }
                }
                catch
                {
                }
            }

            RebuildCollections();
            RefreshHeader();
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to load catalog. Check your connection.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Detach()
    {
        _catalog.OnChange -= OnServicesChanged;
        _auth.OnChange -= OnServicesChanged;
        _cart.OnChange -= OnServicesChanged;
        _wishlist.OnChange -= OnServicesChanged;
        _notifications.OnChange -= OnServicesChanged;
    }

    private void OnServicesChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RebuildCollections();
            RefreshHeader();
        });

    private void RefreshHeader()
    {
        if (_auth.IsLoggedIn && !string.IsNullOrWhiteSpace(_auth.FirstName) &&
            !_auth.FirstName.Equals("Guest", StringComparison.OrdinalIgnoreCase))
        {
            GreetingTitle = $"Hello, {_auth.FirstName}! 👋";
            GreetingSubtitle = "Ready to show your Bulldog pride?";
        }
        else
        {
            GreetingTitle = "Welcome! 👋";
            GreetingSubtitle = "Browse NU Bulldogs merchandise.";
        }

        CartCount = _cart.TotalCount;
        NotificationCount = _notifications.UnreadCount;
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(HasCartItems));
    }

    private void RebuildCollections()
    {
        Categories.Clear();
        foreach (var chip in BuildCategories())
            Categories.Add(chip);

        HeroBanners.Clear();
        foreach (var banner in BuildHeroBanners())
            HeroBanners.Add(banner);

        FeaturedRows.Clear();
        var featured = _catalog.Featured.Take(6).ToList();
        foreach (var row in ToPairs(featured))
            FeaturedRows.Add(row);
        HasFeatured = FeaturedRows.Count > 0;

        FreshDrops.Clear();
        foreach (var product in _catalog.FreshDrops.Take(8))
            FreshDrops.Add(product);
        HasFresh = FreshDrops.Count > 0;

        FavoriteRows.Clear();
        foreach (var row in ToPairs(_catalog.Favorites.Take(4)))
            FavoriteRows.Add(row);
    }

    private static IEnumerable<CategoryChip> BuildCategories()
    {
        // Use product-level taxonomy (AdminProductService.Categories), not the
        // parent Categories table rows (Apparel / Accessories).
        var preferred = AdminProductService.Categories;
        var palette = new Dictionary<string, (string Icon, Color Bg)>(StringComparer.OrdinalIgnoreCase)
        {
            ["T-Shirts"] = ("👕", Color.FromArgb("#E8F8F5")),
            ["Polo Shirts"] = ("👔", Color.FromArgb("#EBF3FF")),
            ["Hoodies"] = ("🧥", Color.FromArgb("#FEF3E8")),
            ["Jackets"] = ("🧣", Color.FromArgb("#F3E8FE")),
            ["Caps"] = ("🧢", Color.FromArgb("#FEF9E8")),
            ["Bags"] = ("🎒", Color.FromArgb("#FCE8F3")),
            ["Tumblers"] = ("🥤", Color.FromArgb("#EEF2FF")),
            ["Accessories"] = ("🏷️", Color.FromArgb("#E6F9F6")),
            ["School Supplies"] = ("📚", Color.FromArgb("#F0FDF4")),
        };

        return preferred.Select(name =>
        {
            palette.TryGetValue(name, out var style);
            return new CategoryChip
            {
                Name = ShortCategoryName(name),
                Icon = string.IsNullOrEmpty(style.Icon) ? "🏷️" : style.Icon,
                Background = style.Bg == default ? Color.FromArgb("#F1F5F9") : style.Bg,
                Slug = name
            };
        });
    }

    private static string ShortCategoryName(string name) => name switch
    {
        "T-Shirts" => "Shirts",
        "Polo Shirts" => "Polo Shirts",
        "Hoodies" => "Hoodies",
        "Jackets" => "Jackets",
        "School Supplies" => "Supplies",
        _ => name
    };

    private static IEnumerable<HeroBannerItem> BuildHeroBanners() =>
    [
        new HeroBannerItem
        {
            Title = "Bulldog Essentials",
            Subtitle = "Gear up for campus life.",
            ButtonText = "Shop Now",
            ImageUrl = "https://images.unsplash.com/photo-1556821840-3a63f95609a7?w=500&auto=format&fit=crop&q=80",
            Route = "shop"
        },
        new HeroBannerItem
        {
            Title = "New Semester. New Style.",
            Subtitle = "Explore the latest NU apparel.",
            ButtonText = "Shop Now",
            ImageUrl = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=500&auto=format&fit=crop&q=80",
            Route = "shop"
        },
        new HeroBannerItem
        {
            Title = "Campus Ready",
            Subtitle = "Bags, tumblers, and essentials.",
            ButtonText = "Explore",
            ImageUrl = "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=500&auto=format&fit=crop&q=80",
            Route = "shop"
        }
    ];

    private static IEnumerable<ProductPair> ToPairs(IEnumerable<Product> products)
    {
        var list = products.ToList();
        for (var i = 0; i < list.Count; i += 2)
        {
            yield return new ProductPair
            {
                Left = list[i],
                Right = i + 1 < list.Count ? list[i + 1] : null
            };
        }
    }

    private void OnSearch() => _ = GoAsync("shop");

    private async Task OnCategoryAsync(CategoryChip? chip)
    {
        if (chip is null) return;
        await GoAsync("shop");
    }

    private async Task OnProductAsync(Product? product)
    {
        if (product is null) return;
        await GoAsync("shop");
    }

    private async Task OnHeroAsync(HeroBannerItem? hero) =>
        await GoAsync(hero?.Route ?? "shop");

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
    }

    private async Task OnAddToCartAsync(Product? product)
    {
        if (product is null) return;
        _cart.Add(product, 1, product.Colors.FirstOrDefault(), product.Sizes.FirstOrDefault());
        await _cart.PersistAsync(_auth.Email);
        _toast.Show($"Added {product.Name} to cart!");
        RefreshHeader();
    }

    private static async Task GoAsync(string route)
    {
        try
        {
            // Tab roots need "//"; push routes (cart/login/register/checkout) stay relative.
            var path = route.StartsWith("//", StringComparison.Ordinal) || route.StartsWith("..", StringComparison.Ordinal)
                ? route
                : IsShellTab(route) ? $"//{route}" : route;

            await Shell.Current.GoToAsync(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static bool IsShellTab(string route) =>
        route is "home" or "shop" or "wishlist" or "orders" or "account";

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
