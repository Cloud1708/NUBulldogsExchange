using System.Windows.Input;
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

    public ProductCardView()
    {
        InitializeComponent();
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

    /// <summary>Resolves a displayable image, falling back to the shared placeholder.</summary>
    public ImageSource DisplayImage
    {
        get
        {
            var url = Product?.ImageUrl?.Trim();
            if (string.IsNullOrWhiteSpace(url) ||
                url.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                url.Equals("undefined", StringComparison.OrdinalIgnoreCase))
            {
                return ImageSource.FromUri(new Uri("https://placehold.co/400x400/F1F5F9/00205B?text=NU"));
            }

            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return ImageSource.FromUri(new Uri("https://placehold.co/400x400/F1F5F9/00205B?text=NU"));

            try
            {
                return ImageSource.FromUri(new Uri(url));
            }
            catch
            {
                return ImageSource.FromUri(new Uri("https://placehold.co/400x400/F1F5F9/00205B?text=NU"));
            }
        }
    }

    public bool IsWishlisted
    {
        get
        {
            if (Product is null) return false;
            var wishlist = Handler?.MauiContext?.Services.GetService<WishlistService>();
            return wishlist?.Contains(Product.Id) == true;
        }
    }

    public string WishlistGlyph => IsWishlisted ? "♥" : "♡";
    public Color WishlistColor => IsWishlisted ? Color.FromArgb("#EF4444") : Color.FromArgb("#A78BFA");

    private static void OnProductChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProductCardView card)
            card.RefreshDerived();
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
        OnPropertyChanged(nameof(DisplayImage));
        OnPropertyChanged(nameof(WishlistGlyph));
        OnPropertyChanged(nameof(WishlistColor));
        OnPropertyChanged(nameof(IsWishlisted));
    }

    private void OnWishlistTapped(object? sender, TappedEventArgs e)
    {
        if (ToggleWishlistCommand?.CanExecute(Product) == true)
            ToggleWishlistCommand.Execute(Product);
        RefreshDerived();
    }

    private void OnAddTapped(object? sender, TappedEventArgs e)
    {
        if (AddToCartCommand?.CanExecute(Product) == true)
            AddToCartCommand.Execute(Product);
    }
}
