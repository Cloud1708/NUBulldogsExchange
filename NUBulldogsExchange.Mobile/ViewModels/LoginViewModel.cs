using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

[QueryProperty(nameof(Registered), "registered")]
[QueryProperty(nameof(RegisteredEmail), "email")]
public sealed class LoginViewModel : INotifyPropertyChanged, IQueryAttributable
{
    private readonly AuthService _auth;
    private readonly WishlistService _wishlist;
    private readonly IAppDatabase _db;
    private Page? _host;

    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe = true;
    private bool _isPasswordHidden = true;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private bool _showAccountCreatedNotice;

    public LoginViewModel(AuthService auth, WishlistService wishlist, IAppDatabase db)
    {
        _auth = auth;
        _wishlist = wishlist;
        _db = db;

        LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
        TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
        ContinueAsGuestCommand = new Command(async () => await GoHomeAsync());
        ForgotPasswordCommand = new Command(async () => await OnForgotPasswordAsync());
        StaffPortalCommand = new Command(async () => await OpenStaffPortalAsync());
        CreateAccountCommand = new Command(async () =>
        {
            ShowAccountCreatedNotice = false;
            await GoAsync("register");
        });
        BackCommand = new Command(async () => await GoBackAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public bool ShowAccountCreatedNotice
    {
        get => _showAccountCreatedNotice;
        set => SetField(ref _showAccountCreatedNotice, value);
    }

    public string Registered
    {
        set => ShowAccountCreatedNotice = value is "1" or "true" or "True";
    }

    public string RegisteredEmail
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                Email = Uri.UnescapeDataString(value).Trim();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("registered", out var regVal) && regVal is not null)
        {
            var str = regVal.ToString();
            ShowAccountCreatedNotice = str is "1" or "true" or "True";
        }

        if (query.TryGetValue("email", out var emailVal) && emailVal is not null)
        {
            var emailStr = emailVal.ToString();
            if (!string.IsNullOrWhiteSpace(emailStr))
                Email = Uri.UnescapeDataString(emailStr).Trim();
        }
    }

    public string Email
    {
        get => _email;
        set => SetField(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetField(ref _rememberMe, value);
    }

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        private set
        {
            if (SetField(ref _isPasswordHidden, value))
                OnPropertyChanged(nameof(PasswordToggleGlyph));
        }
    }

    public string PasswordToggleGlyph => IsPasswordHidden ? Helpers.MaterialIconCodes.VisibilityOff : Helpers.MaterialIconCodes.Visibility;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((Command)LoginCommand).ChangeCanExecute();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ICommand LoginCommand { get; }
    public ICommand TogglePasswordCommand { get; }
    public ICommand ContinueAsGuestCommand { get; }
    public ICommand ForgotPasswordCommand { get; }
    public ICommand StaffPortalCommand { get; }
    public ICommand CreateAccountCommand { get; }
    public ICommand BackCommand { get; }

    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = string.Empty;
        ShowAccountCreatedNotice = false;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email is required.";
            return;
        }

        if (!AuthValidation.IsValidEmail(Email))
        {
            ErrorMessage = "Enter a valid email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Password is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Email.Trim(), Password, RememberMe);
            Password = string.Empty;

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Invalid email or password.";
                return;
            }

            var denied = await MobileAuthGuard.EnforceAsync(_auth, result.User);
            if (denied is not null)
            {
                ErrorMessage = denied;
                await ShowDeniedAsync(denied);
                return;
            }

            if (result.User is not null)
            {
                await MobileAuthGuard.PersistAsync(result.User);
                await MobileWishlistSync.SyncWithServerAsync(_wishlist, result.User.Email, _db);
            }

            await NavigateAfterAuthAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to sign in. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowDeniedAsync(string message)
    {
        var page = HostPage ?? Shell.Current;
        if (message == MobileAuthGuard.AccessDeniedMessage)
        {
            await page.DisplayAlertAsync(
                MobileAuthGuard.AccessDeniedTitle,
                MobileAuthGuard.AccessDeniedMessage,
                "OK");
            return;
        }

        if (message == MobileAuthGuard.InactiveMessage)
        {
            await page.DisplayAlertAsync("Account Inactive", message, "OK");
            return;
        }

        await page.DisplayAlertAsync("Unable to sign in", message, "OK");
    }

    private async Task OnForgotPasswordAsync()
    {
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync(
            "Forgot Password",
            "Password reset is handled through campus support or the NU Bulldogs Exchange web portal. Contact the merchandise desk for assistance.",
            "OK");
    }

    private async Task OpenStaffPortalAsync()
    {
        try
        {
            await Launcher.Default.OpenAsync(MobileWebUrls.AdminPortalUri);
        }
        catch (Exception)
        {
            var page = HostPage ?? Shell.Current;
            await page.DisplayAlertAsync(
                "Staff / Admin Portal",
                "Unable to open the web admin portal. Make sure the Web server is running.",
                "OK");
        }
    }

    private static Task NavigateAfterAuthAsync() =>
        MobileCheckoutIntent.NavigateAfterAuthAsync();

    private static async Task GoHomeAsync()
    {
        try { await Shell.Current.GoToAsync("//home"); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static async Task GoAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//account");
        }
        catch
        {
            await Shell.Current.GoToAsync("//account");
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
