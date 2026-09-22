using System.Windows.Input;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Controls;

public partial class WishlistProductCardView : ContentView
{
    public static readonly BindableProperty ProductProperty =
        BindableProperty.Create(nameof(Product), typeof(Product), typeof(WishlistProductCardView),
            propertyChanged: OnProductChanged);

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(WishlistProductCardView));

    public static readonly BindableProperty AddToCartCommandProperty =
        BindableProperty.Create(nameof(AddToCartCommand), typeof(ICommand), typeof(WishlistProductCardView));

    public static readonly BindableProperty OpenProductCommandProperty =
        BindableProperty.Create(nameof(OpenProductCommand), typeof(ICommand), typeof(WishlistProductCardView));

    public WishlistProductCardView()
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

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
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

    public bool HasRating => Product is not null && Product.Rating > 0;

    public string RatingText =>
        Product is null || Product.Rating <= 0 ? string.Empty : Product.Rating.ToString("0.0");

    public string PriceText => Product is null ? "₱0" : $"₱{Product.Price:N0}";

    public ImageSource DisplayImage
    {
        get
        {
            var fallback = "https://placehold.co/400x400/F1F5F9/00205B?text=NU";
            var url = Product?.ImageUrl?.Trim();
            if (string.IsNullOrWhiteSpace(url) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                url.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return ImageSource.FromUri(new Uri(fallback));
            }

            try
            {
                return ImageSource.FromUri(new Uri(url));
            }
            catch
            {
                return ImageSource.FromUri(new Uri(fallback));
            }
        }
    }

    private static void OnProductChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is WishlistProductCardView card)
            card.RefreshDerived();
    }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(HasRating));
        OnPropertyChanged(nameof(RatingText));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(DisplayImage));
    }

    private void OnHeartTapped(object? sender, TappedEventArgs e)
    {
        if (RemoveCommand?.CanExecute(Product) == true)
            RemoveCommand.Execute(Product);
    }

    private void OnAddTapped(object? sender, TappedEventArgs e)
    {
        if (AddToCartCommand?.CanExecute(Product) == true)
            AddToCartCommand.Execute(Product);
    }
}
