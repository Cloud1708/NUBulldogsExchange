using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile;

public partial class MainPage : ContentPage
{
    private readonly HomeViewModel _vm;

    public MainPage(HomeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        // Keep subscriptions alive for the singleton ViewModel / services.
        base.OnDisappearing();
    }
}
