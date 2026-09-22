using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Pages;

public partial class ProductDetailsPage : ContentPage
{
    public ProductDetailsPage(ProductDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
