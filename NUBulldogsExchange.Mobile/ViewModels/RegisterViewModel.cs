using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class RegisterViewModel : INotifyPropertyChanged
{
    private readonly AuthService _auth;
    private Page? _host;

    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _studentId = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _hasAcceptedTerms;
    private bool _isPasswordHidden = true;
    private bool _isConfirmPasswordHidden = true;
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public RegisterViewModel(AuthService auth)
    {
        _auth = auth;
        RegisterCommand = new Command(async () => await RegisterAsync(), () => !IsBusy);
        TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
        ToggleConfirmPasswordCommand = new Command(() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden);
        GoLoginCommand = new Command(async () => await GoAsync("login"));
        BackCommand = new Command(async () => await GoBackAsync());
        OpenTermsCommand = new Command(async () => await ShowTermsAsync());
        OpenPrivacyCommand = new Command(async () => await ShowPrivacyAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public string FirstName
    {
        get => _firstName;
        set => SetField(ref _firstName, value);
    }

    public string LastName
    {
        get => _lastName;
        set => SetField(ref _lastName, value);
    }

    public string Email
    {
        get => _email;
        set => SetField(ref _email, value);
    }

    public string PhoneNumber
    {
        get => _phoneNumber;
        set => SetField(ref _phoneNumber, value);
    }

    public string StudentId
    {
        get => _studentId;
        set => SetField(ref _studentId, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetField(ref _confirmPassword, value);
    }

    public bool HasAcceptedTerms
    {
        get => _hasAcceptedTerms;
        set => SetField(ref _hasAcceptedTerms, value);
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

    public bool IsConfirmPasswordHidden
    {
        get => _isConfirmPasswordHidden;
        private set
        {
            if (SetField(ref _isConfirmPasswordHidden, value))
                OnPropertyChanged(nameof(ConfirmPasswordToggleGlyph));
        }
    }

    public string PasswordToggleGlyph => IsPasswordHidden ? "👁" : "🙈";
    public string ConfirmPasswordToggleGlyph => IsConfirmPasswordHidden ? "👁" : "🙈";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((Command)RegisterCommand).ChangeCanExecute();
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

    public ICommand RegisterCommand { get; }
    public ICommand TogglePasswordCommand { get; }
    public ICommand ToggleConfirmPasswordCommand { get; }
    public ICommand GoLoginCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand OpenTermsCommand { get; }
    public ICommand OpenPrivacyCommand { get; }

    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(FirstName))
        {
            ErrorMessage = "First name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "Last name is required.";
            return;
        }

        if (!AuthValidation.IsValidEmail(Email))
        {
            ErrorMessage = "Enter a valid email address.";
            return;
        }

        if (!AuthValidation.IsValidPhone(PhoneNumber))
        {
            ErrorMessage = "Please enter a valid contact number.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < AuthValidation.MinPasswordLength)
        {
            ErrorMessage = $"Password must be at least {AuthValidation.MinPasswordLength} characters.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ErrorMessage = "Confirm password is required.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (!HasAcceptedTerms)
        {
            ErrorMessage = "Please agree to the Terms and Conditions and Privacy Policy.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _auth.RegisterAsync(new RegisterRequest
            {
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                Email = Email.Trim(),
                PhoneNumber = PhoneNumber.Trim(),
                Password = Password,
                ConfirmPassword = ConfirmPassword
            });
            Password = string.Empty;
            ConfirmPassword = string.Empty;

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Unable to create your account.";
                return;
            }

            // StudentId is not part of RegisterRequest; save via profile update after auto-login.
            if (!string.IsNullOrWhiteSpace(StudentId) && _auth.IsLoggedIn)
            {
                await _auth.UpdateProfileAsync(new UpdateProfileRequest
                {
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    StudentId = StudentId.Trim(),
                    ProfileImage = _auth.CurrentUser?.ProfileImage ?? string.Empty,
                    Address = _auth.CurrentUser?.Address ?? string.Empty
                });
            }

            await MobileCheckoutIntent.NavigateAfterAuthAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to register. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowTermsAsync()
    {
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync(
            "Terms & Conditions",
            "By using NU Bulldogs Exchange you agree to campus merchandise purchase policies, pickup rules, and return guidelines.",
            "OK");
    }

    private async Task ShowPrivacyAsync()
    {
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync(
            "Privacy Policy",
            "We use your account details only to process orders, manage pickups, and secure your session. We do not sell personal data.",
            "OK");
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
