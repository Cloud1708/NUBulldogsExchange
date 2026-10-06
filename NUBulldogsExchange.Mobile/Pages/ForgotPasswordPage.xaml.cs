using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Pages;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly ForgotPasswordViewModel _vm;

    public ForgotPasswordPage(ForgotPasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Dispose();
    }
}
