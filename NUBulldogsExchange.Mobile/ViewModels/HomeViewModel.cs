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
    private readonly AdminCategoryService _adminCategories;

    private string _greetingTitle = "Welcome! 👋";
    private string _greetingSubtitle = "Browse NU Bulldogs merchandise.";
    private string _searchQuery = string.Empty;
    private bool _isBusy;
    private int _cartCount;
    private int _notificationCount;
    private string? _statusMessage;
    private bool _hasFeatured;
    private bool _hasFresh;
    private bool _updatePending;

    public HomeViewModel(
        ProductCatalogService catalog,
        AuthService auth,
        CartService cart,
        WishlistService wishlist,
        NotificationService notifications,
        ToastService toast,
        IAppDatabase db,
        AdminProductService adminProducts,
        AdminCategoryService adminCategories)
    {
        _catalog = catalog;
        _auth = auth;
        _cart = cart;
        _wishlist = wishlist;
        _notifications = notifications;
        _toast = toast;
        _db = db;
        _adminProducts = adminProducts;
        _adminCategories = adminCategories;

        RefreshCommand = new Command(async () => await LoadAsync());
        SearchCommand = new Command(OnSearch);
        OpenShopCommand = new Command(async () => await GoAsync("//shop"));
        OpenCartCommand = new Command(async () => await GoAsync("cart"));
        OpenNotificationsCommand = new Command(async () => await GoAsync("//account"));
        OpenCategoryCommand = new Command<CategoryChip>(async c => await OnCategoryAsync(c));
        OpenProductCommand = new Command<Product>(async p => await OnProductAsync(p));
        ToggleWishlistCommand = new Command<Product>(async p => await OnToggleWishlistAsync(p));
        AddToCartCommand = new Command<Product>(async p => await OnAddToCartAsync(p));
        ShopNowCommand = new Command<HeroBannerItem>(async h => await OnHeroAsync(h));
        ExploreCollectionCommand = new Command(async () => await GoAsync("//shop"));
        SeeAllFeaturedCommand = new Command(async () => await GoAsync("//shop"));
        SeeAllFreshCommand = new Command(async () => await GoAsync("//shop"));
        SeeAllFavoritesCommand = new Command(async () => await GoAsync("//shop"));
        SeeAllCategoriesCommand = new Command(async () => await GoAsync("//shop"));

        // Initialize hero banners once
        foreach (var banner in BuildHeroBanners())
            HeroBanners.Add(banner);

        // Pre-populate immediately from memory so page loads instantly
        RebuildCollections();
        RefreshHeader();

        _catalog.OnChange += OnServicesChanged;
        _adminCategories.OnChange += OnServicesChanged;
        _auth.OnChange += OnServicesChanged;
        _cart.OnChange += OnServicesChanged;
        _wishlist.OnChange += OnServicesChanged;
        _notifications.OnChange += OnServicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryChip> Categories { get; } = [];
    public ObservableCollection<HeroBannerItem> HeroBanners { get; } = [];
    public ObservableCollection<ProductPair> FeaturedRows { get; } = [];
    public ObservableCollection<ProductPair> FreshRows { get; } = [];
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
        // Silent background load without flashing or busy spinners
        StatusMessage = null;
        try
        {
            await _adminCategories.EnsureLoadedAsync();
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
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void Detach()
    {
        _catalog.OnChange -= OnServicesChanged;
        _adminCategories.OnChange -= OnServicesChanged;
        _auth.OnChange -= OnServicesChanged;
        _cart.OnChange -= OnServicesChanged;
        _wishlist.OnChange -= OnServicesChanged;
        _notifications.OnChange -= OnServicesChanged;
    }

    private void OnServicesChanged()
    {
        if (_updatePending) return;
        _updatePending = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _updatePending = false;
            RebuildCollections();
            RefreshHeader();
        });
    }

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
        // Smooth in-place synchronization to prevent flashing/blinking
        var newCategories = BuildCategories().ToList();
        SyncList(Categories, newCategories, (a, b) =>
            a.Id == b.Id && a.Name == b.Name && a.ImageUrl == b.ImageUrl && a.Icon == b.Icon);

        var featured = _catalog.Featured.Take(6).ToList();
        var newFeaturedRows = ToPairs(featured).ToList();
        SyncList(FeaturedRows, newFeaturedRows, (a, b) =>
            a.Left?.Id == b.Left?.Id && a.Left?.Price == b.Left?.Price &&
            a.Right?.Id == b.Right?.Id && a.Right?.Price == b.Right?.Price);
        HasFeatured = FeaturedRows.Count > 0;

        var fresh = _catalog.FreshDrops.Take(6).ToList();
        var newFreshRows = ToPairs(fresh).ToList();
        SyncList(FreshRows, newFreshRows, (a, b) =>
            a.Left?.Id == b.Left?.Id && a.Left?.Price == b.Left?.Price &&
            a.Right?.Id == b.Right?.Id && a.Right?.Price == b.Right?.Price);
        HasFresh = FreshRows.Count > 0;

        var newFavorites = ToPairs(_catalog.Favorites.Take(4)).ToList();
        SyncList(FavoriteRows, newFavorites, (a, b) =>
            a.Left?.Id == b.Left?.Id && a.Right?.Id == b.Right?.Id);
    }

    private static void SyncList<T>(ObservableCollection<T> collection, IList<T> newItems, Func<T, T, bool> areEqual)
    {
        if (collection.Count == newItems.Count)
        {
            bool identical = true;
            for (int i = 0; i < collection.Count; i++)
            {
                if (!areEqual(collection[i], newItems[i]))
                {
                    identical = false;
                    break;
                }
            }
            if (identical) return; // Zero modification, zero flicker
        }

        collection.Clear();
        foreach (var item in newItems)
            collection.Add(item);
    }

    private IEnumerable<CategoryChip> BuildCategories()
    {
        return _adminCategories.All
            .Where(c => c.IsActive)
            .Select(c => new CategoryChip
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = c.ImageUrl,
                Icon = DeriveCategoryIcon(c.Name),
                Background = DeriveCategoryBg(c.Name)
            });
    }

    public static string DeriveCategoryIcon(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "🏷️";
        var n = name.ToLowerInvariant();
        if (n.Contains("t-shirt") || n.Contains("shirt") || n.Contains("tee")) return "👕";
        if (n.Contains("polo")) return "👔";
        if (n.Contains("hood")) return "🧥";
        if (n.Contains("jacket") || n.Contains("coat") || n.Contains("outer")) return "🧣";
        if (n.Contains("cap") || n.Contains("hat")) return "🧢";
        if (n.Contains("bag") || n.Contains("backpack") || n.Contains("tote")) return "🎒";
        if (n.Contains("tumbler") || n.Contains("bottle") || n.Contains("mug") || n.Contains("cup")) return "🥤";
        if (n.Contains("suppl") || n.Contains("book") || n.Contains("note") || n.Contains("pen")) return "📚";
        if (n.Contains("jersey") || n.Contains("sport")) return "🎽";
        if (n.Contains("shoe") || n.Contains("sock")) return "👟";
        if (n.Contains("lanyard") || n.Contains("id")) return "🪪";
        if (n.Contains("sticker") || n.Contains("pin") || n.Contains("badge")) return "✨";
        return "🏷️";
    }

    public static Color DeriveCategoryBg(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Color.FromArgb("#F1F5F9");
        var palettes = new[]
        {
            Color.FromArgb("#E8F8F5"),
            Color.FromArgb("#EBF3FF"),
            Color.FromArgb("#FEF3E8"),
            Color.FromArgb("#F3E8FE"),
            Color.FromArgb("#FEF9E8"),
            Color.FromArgb("#FCE8F3"),
            Color.FromArgb("#EEF2FF"),
            Color.FromArgb("#E6F9F6"),
            Color.FromArgb("#F0FDF4"),
        };
        var hash = Math.Abs(name.GetHashCode());
        return palettes[hash % palettes.Length];
    }

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

    private void OnSearch() => _ = GoAsync("//shop");

    private async Task OnCategoryAsync(CategoryChip? chip)
    {
        if (chip is null || string.IsNullOrWhiteSpace(chip.Id))
        {
            await GoAsync("//shop");
            return;
        }

        await GoAsync($"//shop?categoryId={Uri.EscapeDataString(chip.Id)}");
    }

    private async Task OnProductAsync(Product? product)
    {
        if (product is null) return;
        await GoAsync($"product?id={product.Id}");
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
        if (product.HasSizeVariants)
        {
            await OnProductAsync(product);
            return;
        }

        _cart.Add(product, 1, product.Colors.FirstOrDefault(), null);
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
