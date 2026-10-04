using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly RegisterViewModel _vm;

    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.HostPage = this;
        BindingContext = _vm;

        PhoneNumberEntry.HandlerChanged += (s, e) => AttachWindowsHandler();
        if (PhoneNumberEntry.Handler != null)
        {
            AttachWindowsHandler();
        }
    }

    private void AttachWindowsHandler()
    {
#if WINDOWS
        if (PhoneNumberEntry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            textBox.BeforeTextChanging -= OnWindowsPhoneBeforeTextChanging;
            textBox.BeforeTextChanging += OnWindowsPhoneBeforeTextChanging;
        }
#endif
    }

#if WINDOWS
    private void OnWindowsPhoneBeforeTextChanging(Microsoft.UI.Xaml.Controls.TextBox sender, Microsoft.UI.Xaml.Controls.TextBoxBeforeTextChangingEventArgs args)
    {
        if (string.IsNullOrEmpty(args.NewText)) return;

        // Cancel input if it contains any non-digit character (e.g. letters) or exceeds 11 digits
        if (args.NewText.Any(c => !char.IsDigit(c)) || args.NewText.Length > 11)
        {
            args.Cancel = true;
        }
    }
#endif

    private void OnPhoneNumberTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry) return;

        var raw = e.NewTextValue ?? string.Empty;
        if (string.IsNullOrEmpty(raw)) return;

        var digitsOnly = new string(raw.Where(char.IsDigit).Take(11).ToArray());
        if (raw != digitsOnly)
        {
            entry.Text = digitsOnly;
            entry.CursorPosition = digitsOnly.Length;
        }
    }
}
