using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Pages;

public partial class LoginPage : ContentPage, IQueryAttributable
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
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
}
