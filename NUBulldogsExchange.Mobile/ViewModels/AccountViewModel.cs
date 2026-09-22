using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class AccountViewModel : INotifyPropertyChanged
{
    private readonly AuthService _auth;
    private Page? _host;

    private bool _isLoggedIn;
    private string _displayName = "Guest";
    private string _email = string.Empty;
    private string _studentIdLabel = string.Empty;
    private string _initial = "?";
    private string _statusMessage = string.Empty;
    private bool _showLoginForm;
    private bool _showRegisterForm;
    private bool _showChangePasswordForm;
    private bool _showEditProfileForm;

    private string _loginEmail = string.Empty;
    private string _loginPassword = string.Empty;
    private string _regFirstName = string.Empty;
    private string _regLastName = string.Empty;
    private string _regEmail = string.Empty;
    private string _regPassword = string.Empty;
    private string _regConfirm = string.Empty;
    private string _editFirstName = string.Empty;
    private string _editLastName = string.Empty;
    private string _editPhone = string.Empty;
    private string _editStudentId = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmNewPassword = string.Empty;

    public AccountViewModel(AuthService auth)
    {
        _auth = auth;

        ShowLoginFormCommand = new Command(async () => await GoAsync("login"));
        ShowRegisterFormCommand = new Command(async () => await GoAsync("register"));
        CancelFormsCommand = new Command(HideAllForms);
        LoginCommand = new Command(async () => await LoginAsync());
        RegisterCommand = new Command(async () => await RegisterAsync());
        LogoutCommand = new Command(async () => await LogoutAsync());
        EditProfileCommand = new Command(OpenEditProfile);
        SaveProfileCommand = new Command(async () => await SaveProfileAsync());
        OpenChangePasswordCommand = new Command(() =>
        {
            ShowLoginForm = false;
            ShowRegisterForm = false;
            ShowEditProfileForm = false;
            ShowChangePasswordForm = true;
            StatusMessage = string.Empty;
        });
        SavePasswordCommand = new Command(async () => await SavePasswordAsync());

        GoOrdersCommand = new Command(async () => await GoAsync("//orders"));
        GoWishlistCommand = new Command(async () => await GoAsync("//wishlist"));
        GoMyProfileCommand = new Command(OpenEditProfile);
        GoNotificationsCommand = new Command(async () => await InfoAsync("Notifications", "Notification center is available after you place orders."));
        GoAddressesCommand = new Command(async () => await InfoAsync("Addresses", string.IsNullOrWhiteSpace(_auth.CurrentUser?.Address)
            ? "No address on file yet. Use Edit to add one."
            : _auth.CurrentUser!.Address));
        GoHelpCommand = new Command(async () => await InfoAsync("Help Center", "For assistance with orders or account issues, contact campus support or the NU Bulldogs Exchange desk."));
        GoTermsCommand = new Command(async () => await InfoAsync("Terms & Conditions", "By using NU Bulldogs Exchange you agree to campus merchandise purchase policies, pickup rules, and return guidelines."));
        GoPrivacyCommand = new Command(async () => await InfoAsync("Privacy Policy", "We use your account details only to process orders, manage pickups, and secure your session. We do not sell personal data."));

        _auth.OnChange += () => MainThread.BeginInvokeOnMainThread(RefreshFromAuth);
        RefreshFromAuth();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        private set
        {
            if (SetField(ref _isLoggedIn, value))
                OnPropertyChanged(nameof(IsGuest));
        }
    }

    public bool IsGuest => !IsLoggedIn;

    public string DisplayName
    {
        get => _displayName;
        private set => SetField(ref _displayName, value);
    }

    public string Email
    {
        get => _email;
        private set => SetField(ref _email, value);
    }

    public string StudentIdLabel
    {
        get => _studentIdLabel;
        private set => SetField(ref _studentIdLabel, value);
    }

    public bool HasStudentId => !string.IsNullOrWhiteSpace(StudentIdLabel);

    public string Initial
    {
        get => _initial;
        private set => SetField(ref _initial, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool ShowLoginForm
    {
        get => _showLoginForm;
        private set => SetField(ref _showLoginForm, value);
    }

    public bool ShowRegisterForm
    {
        get => _showRegisterForm;
        private set => SetField(ref _showRegisterForm, value);
    }

    public bool ShowChangePasswordForm
    {
        get => _showChangePasswordForm;
        private set => SetField(ref _showChangePasswordForm, value);
    }

    public bool ShowEditProfileForm
    {
        get => _showEditProfileForm;
        private set => SetField(ref _showEditProfileForm, value);
    }

    public string LoginEmail
    {
        get => _loginEmail;
        set => SetField(ref _loginEmail, value);
    }

    public string LoginPassword
    {
        get => _loginPassword;
        set => SetField(ref _loginPassword, value);
    }

    public string RegFirstName
    {
        get => _regFirstName;
        set => SetField(ref _regFirstName, value);
    }

    public string RegLastName
    {
        get => _regLastName;
        set => SetField(ref _regLastName, value);
    }

    public string RegEmail
    {
        get => _regEmail;
        set => SetField(ref _regEmail, value);
    }

    public string RegPassword
    {
        get => _regPassword;
        set => SetField(ref _regPassword, value);
    }

    public string RegConfirm
    {
        get => _regConfirm;
        set => SetField(ref _regConfirm, value);
    }

    public string EditFirstName
    {
        get => _editFirstName;
        set => SetField(ref _editFirstName, value);
    }

    public string EditLastName
    {
        get => _editLastName;
        set => SetField(ref _editLastName, value);
    }

    public string EditPhone
    {
        get => _editPhone;
        set => SetField(ref _editPhone, value);
    }

    public string EditStudentId
    {
        get => _editStudentId;
        set => SetField(ref _editStudentId, value);
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetField(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetField(ref _newPassword, value);
    }

    public string ConfirmNewPassword
    {
        get => _confirmNewPassword;
        set => SetField(ref _confirmNewPassword, value);
    }

    public ICommand ShowLoginFormCommand { get; }
    public ICommand ShowRegisterFormCommand { get; }
    public ICommand CancelFormsCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand EditProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand OpenChangePasswordCommand { get; }
    public ICommand SavePasswordCommand { get; }
    public ICommand GoOrdersCommand { get; }
    public ICommand GoWishlistCommand { get; }
    public ICommand GoMyProfileCommand { get; }
    public ICommand GoNotificationsCommand { get; }
    public ICommand GoAddressesCommand { get; }
    public ICommand GoHelpCommand { get; }
    public ICommand GoTermsCommand { get; }
    public ICommand GoPrivacyCommand { get; }

    public void RefreshFromAuth()
    {
        IsLoggedIn = _auth.IsLoggedIn;
        if (IsLoggedIn && _auth.CurrentUser is not null)
        {
            var user = _auth.CurrentUser;
            DisplayName = string.IsNullOrWhiteSpace(user.Name)
                ? $"{user.FirstName} {user.LastName}".Trim()
                : user.Name;
            Email = user.Email;
            Initial = user.Initial;
            StudentIdLabel = string.IsNullOrWhiteSpace(user.StudentId)
                ? string.Empty
                : $"Student ID: {user.StudentId}";
            HideAllForms();
        }
        else
        {
            DisplayName = "Guest";
            Email = "Login to access your account.";
            Initial = "?";
            StudentIdLabel = string.Empty;
        }

        OnPropertyChanged(nameof(HasStudentId));
    }

    private void HideAllForms()
    {
        ShowLoginForm = false;
        ShowRegisterForm = false;
        ShowChangePasswordForm = false;
        ShowEditProfileForm = false;
    }

    private void OpenEditProfile()
    {
        if (!_auth.IsLoggedIn || _auth.CurrentUser is null) return;
        var user = _auth.CurrentUser;
        EditFirstName = user.FirstName;
        EditLastName = user.LastName;
        EditPhone = user.Phone;
        EditStudentId = user.StudentId;
        ShowLoginForm = false;
        ShowRegisterForm = false;
        ShowChangePasswordForm = false;
        ShowEditProfileForm = true;
        StatusMessage = string.Empty;
    }

    private async Task LoginAsync()
    {
        StatusMessage = "Signing in...";
        try
        {
            var result = await _auth.LoginAsync(LoginEmail.Trim(), LoginPassword, rememberMe: true);
            if (result.Success)
            {
                var denied = await MobileAuthGuard.EnforceAsync(_auth, result.User);
                if (denied is not null)
                {
                    LoginPassword = string.Empty;
                    StatusMessage = denied;
                    if (denied == MobileAuthGuard.AccessDeniedMessage)
                    {
                        await InfoAsync(
                            MobileAuthGuard.AccessDeniedTitle,
                            MobileAuthGuard.AccessDeniedMessage);
                    }
                    RefreshFromAuth();
                    return;
                }

                LoginPassword = string.Empty;
                if (result.User is not null)
                    await MobileAuthGuard.PersistAsync(result.User);
                StatusMessage = $"Welcome, {_auth.FirstName}!";
                RefreshFromAuth();
            }
            else
            {
                StatusMessage = result.Error ?? "Unable to sign in.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to reach the database or API.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task RegisterAsync()
    {
        StatusMessage = "Creating account...";
        try
        {
            var result = await _auth.RegisterAsync(new RegisterRequest
            {
                FirstName = RegFirstName.Trim(),
                LastName = RegLastName.Trim(),
                Email = RegEmail.Trim(),
                Password = RegPassword,
                ConfirmPassword = RegConfirm
            });

            if (result.Success)
            {
                RegPassword = string.Empty;
                RegConfirm = string.Empty;
                StatusMessage = "Account created. Please sign in with your new credentials.";
                ShowLoginForm = true;
                ShowRegisterForm = false;
                Email = RegEmail.Trim();
                RefreshFromAuth();
            }
            else
            {
                StatusMessage = result.Error ?? "Unable to register.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to reach the database or API.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await MobileAuthGuard.ClearAsync();
        StatusMessage = "Signed out.";
        RefreshFromAuth();
    }

    private async Task SaveProfileAsync()
    {
        StatusMessage = "Saving profile...";
        try
        {
            var result = await _auth.UpdateProfileAsync(new UpdateProfileRequest
            {
                FirstName = EditFirstName.Trim(),
                LastName = EditLastName.Trim(),
                PhoneNumber = EditPhone.Trim(),
                StudentId = EditStudentId.Trim(),
                Address = _auth.CurrentUser?.Address ?? string.Empty,
                College = _auth.CurrentUser?.College ?? string.Empty,
                ProfileImage = _auth.CurrentUser?.ProfileImage ?? string.Empty
            });

            StatusMessage = result.Success
                ? "Profile updated."
                : result.Error ?? "Unable to update profile.";
            if (result.Success)
                RefreshFromAuth();
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to update profile.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task SavePasswordAsync()
    {
        StatusMessage = "Updating password...";
        try
        {
            var result = await _auth.ChangePasswordAsync(new ChangePasswordRequest
            {
                CurrentPassword = CurrentPassword,
                NewPassword = NewPassword,
                ConfirmNewPassword = ConfirmNewPassword
            });

            if (result.Success)
            {
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmNewPassword = string.Empty;
                ShowChangePasswordForm = false;
                StatusMessage = "Password changed successfully.";
            }
            else
            {
                StatusMessage = result.Error ?? "Unable to change password.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to change password.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task InfoAsync(string title, string message)
    {
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync(title, message, "OK");
    }

    private static async Task GoAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
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
