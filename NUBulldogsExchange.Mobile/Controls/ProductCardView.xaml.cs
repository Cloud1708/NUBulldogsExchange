using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.Controls;

public partial class ProductCardView : ContentView
{
    public static readonly BindableProperty ProductProperty =
        BindableProperty.Create(nameof(Product), typeof(Product), typeof(ProductCardView), propertyChanged: OnProductChanged);

    public static readonly BindableProperty ToggleWishlistCommandProperty =
        BindableProperty.Create(nameof(ToggleWishlistCommand), typeof(ICommand), typeof(ProductCardView));

    public static readonly BindableProperty AddToCartCommandProperty =
        BindableProperty.Create(nameof(AddToCartCommand), typeof(ICommand), typeof(ProductCardView));

    public static readonly BindableProperty OpenProductCommandProperty =
        BindableProperty.Create(nameof(OpenProductCommand), typeof(ICommand), typeof(ProductCardView));

    public static readonly BindableProperty DisplayImageProperty =
        BindableProperty.Create(nameof(DisplayImage), typeof(ImageSource), typeof(ProductCardView));

    private WishlistService? _subscribedWishlist;

    public ProductCardView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (OpenProductCommand?.CanExecute(Product) == true)
                OpenProductCommand.Execute(Product);
        };
        GestureRecognizers.Add(tap);
    }

    public Product? Product
    {
        get => (Product?)GetValue(ProductProperty);
        set => SetValue(ProductProperty, value);
    }

    public ICommand? ToggleWishlistCommand
    {
        get => (ICommand?)GetValue(ToggleWishlistCommandProperty);
        set => SetValue(ToggleWishlistCommandProperty, value);
    }

    public ICommand? AddToCartCommand
    {
        get => (ICommand?)GetValue(AddToCartCommandProperty);
        set => SetValue(AddToCartCommandProperty, value);
    }

    public ICommand? OpenProductCommand
    {
        get => (ICommand?)GetValue(OpenProductCommandProperty);
        set => SetValue(OpenProductCommandProperty, value);
    }

    public bool HasBadge => !string.IsNullOrWhiteSpace(Product?.Badge);
    public string BadgeText => Product?.Badge ?? string.Empty;

    public Color BadgeColor => (Product?.Badge ?? string.Empty) switch
    {
        "Sale" => Color.FromArgb("#EF4444"),
        "New" => Color.FromArgb("#22C55E"),
        "Limited" => Color.FromArgb("#8B5CF6"),
        _ => Color.FromArgb("#F5C518")
    };

    public Color BadgeTextColor => (Product?.Badge ?? string.Empty) switch
    {
        "Sale" or "New" or "Limited" => Colors.White,
        _ => Color.FromArgb("#00205B")
    };

    public string RatingText
    {
        get
        {
            var rating = Product?.Rating > 0 ? Product!.Rating : 4.8;
            return rating.ToString("0.0");
        }
    }

    public string SoldText
    {
        get
        {
            var sold = Product?.Sold > 0 ? Product!.Sold : 120;
            return $"{sold:N0} sold";
        }
    }

    public string PriceText => Product is null ? "₱0" : $"₱{Product.Price:N0}";

    public bool HasOriginalPrice =>
        Product?.OriginalPrice is decimal original && original > Product.Price;

    public string OriginalPriceText =>
        HasOriginalPrice ? $"₱{Product!.OriginalPrice!.Value:N0}" : string.Empty;

    public ImageSource DisplayImage
    {
        get => (ImageSource?)GetValue(DisplayImageProperty) ?? ProductImageHelper.FromProduct(Product);
        private set => SetValue(DisplayImageProperty, value);
    }

    public bool IsWishlisted
    {
        get
        {
            if (Product is null) return false;
            var wishlist = ResolveWishlist();
            return wishlist?.Contains(Product.Id) == true;
        }
    }

    public string WishlistGlyph => IsWishlisted ? Helpers.MaterialIconCodes.Favorite : Helpers.MaterialIconCodes.FavoriteBorder;
    public Color WishlistColor => IsWishlisted ? Color.FromArgb("#EF4444") : Color.FromArgb("#94A3B8");

    private WishlistService? ResolveWishlist()
    {
        return MobileWishlistSync.CurrentWishlist
            ?? Handler?.MauiContext?.Services.GetService<WishlistService>()
            ?? Application.Current?.Handler?.MauiContext?.Services.GetService<WishlistService>()
            ?? IPlatformApplication.Current?.Services?.GetService<WishlistService>();
    }

    private void SubscribeWishlist()
    {
        var ws = ResolveWishlist();
        if (ws is not null && _subscribedWishlist != ws)
        {
            if (_subscribedWishlist is not null)
                _subscribedWishlist.OnChange -= OnWishlistChanged;

            _subscribedWishlist = ws;
            _subscribedWishlist.OnChange += OnWishlistChanged;
        }
    }

    private void UnsubscribeWishlist()
    {
        if (_subscribedWishlist is not null)
        {
            _subscribedWishlist.OnChange -= OnWishlistChanged;
            _subscribedWishlist = null;
        }
    }

    private void OnWishlistChanged()
    {
        if (MainThread.IsMainThread)
        {
            RefreshWishlist();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(RefreshWishlist);
        }
    }

    public void RefreshWishlist()
    {
        OnPropertyChanged(nameof(IsWishlisted));
        OnPropertyChanged(nameof(WishlistGlyph));
        OnPropertyChanged(nameof(WishlistColor));
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        SubscribeWishlist();
        RefreshWishlist();
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        SubscribeWishlist();
        RefreshWishlist();
    }

    private void OnViewUnloaded(object? sender, EventArgs e)
    {
        UnsubscribeWishlist();
    }

    private static void OnProductChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProductCardView card)
        {
            card.SubscribeWishlist();
            card.RefreshDerived();
        }
    }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(HasBadge));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeColor));
        OnPropertyChanged(nameof(BadgeTextColor));
        OnPropertyChanged(nameof(RatingText));
        OnPropertyChanged(nameof(SoldText));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(HasOriginalPrice));
        OnPropertyChanged(nameof(OriginalPriceText));
        DisplayImage = ProductImageHelper.FromProduct(Product);
        RefreshWishlist();
    }

    private void OnWishlistTapped(object? sender, TappedEventArgs e)
    {
        if (ToggleWishlistCommand?.CanExecute(Product) == true)
            ToggleWishlistCommand.Execute(Product);
        RefreshWishlist();
    }

    private void OnAddTapped(object? sender, TappedEventArgs e)
    {
        if (AddToCartCommand?.CanExecute(Product) == true)
            AddToCartCommand.Execute(Product);
    }
}
