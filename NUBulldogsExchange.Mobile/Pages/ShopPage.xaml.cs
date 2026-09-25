using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Pages;

public partial class ShopPage : ContentPage, IQueryAttributable
{
    private readonly ShopViewModel _vm;

    public ShopPage(ShopViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.HostPage = this;
        BindingContext = _vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _vm.ApplyQueryAttributes(query);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
