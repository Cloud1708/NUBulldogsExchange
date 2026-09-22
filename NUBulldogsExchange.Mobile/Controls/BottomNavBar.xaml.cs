namespace NUBulldogsExchange.Mobile.Controls;

public partial class BottomNavBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(
            nameof(ActiveTab),
            typeof(string),
            typeof(BottomNavBar),
            "home",
            propertyChanged: OnActiveTabChanged);

    public BottomNavBar()
    {
        InitializeComponent();
    }

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not BottomNavBar nav) return;
        nav.OnPropertyChanged(nameof(HomeColor));
        nav.OnPropertyChanged(nameof(ShopColor));
        nav.OnPropertyChanged(nameof(WishlistColor));
        nav.OnPropertyChanged(nameof(OrdersColor));
        nav.OnPropertyChanged(nameof(AccountColor));
        nav.OnPropertyChanged(nameof(IsShopActive));
        nav.OnPropertyChanged(nameof(IsWishlistActive));
        nav.OnPropertyChanged(nameof(IsOrdersActive));
        nav.OnPropertyChanged(nameof(IsAccountActive));
        nav.OnPropertyChanged(nameof(WishlistGlyph));
        nav.OnPropertyChanged(nameof(WishlistIconColor));
    }

    private static readonly Color Active = Color.FromArgb("#00205B");
    private static readonly Color Inactive = Color.FromArgb("#94A3B8");
    private static readonly Color WishlistPink = Color.FromArgb("#EF4444");

    public Color HomeColor => IsActive("home") ? Active : Inactive;
    public Color ShopColor => IsActive("shop") ? Active : Inactive;
    public Color WishlistColor => IsActive("wishlist") ? Active : Inactive;
    public Color OrdersColor => IsActive("orders") ? Active : Inactive;
    public Color AccountColor => IsActive("account") ? Active : Inactive;
    public bool IsShopActive => IsActive("shop");
    public bool IsWishlistActive => IsActive("wishlist");
    public bool IsOrdersActive => IsActive("orders");
    public bool IsAccountActive => IsActive("account");
    public string WishlistGlyph => IsWishlistActive ? "♥" : "♡";
    public Color WishlistIconColor => IsWishlistActive ? WishlistPink : Inactive;

    private bool IsActive(string tab) =>
        string.Equals(ActiveTab, tab, StringComparison.OrdinalIgnoreCase);

    private async void OnHome(object? sender, TappedEventArgs e) => await GoAsync("//home");
    private async void OnShop(object? sender, TappedEventArgs e) => await GoAsync("//shop");
    private async void OnWishlist(object? sender, TappedEventArgs e) => await GoAsync("//wishlist");
    private async void OnOrders(object? sender, TappedEventArgs e) => await GoAsync("//orders");
    private async void OnAccount(object? sender, TappedEventArgs e) => await GoAsync("//account");

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
}
