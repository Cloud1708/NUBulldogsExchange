using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile;

public partial class MainPage : ContentPage
{
    private const string TokenKey = "nube-session-token";
    private ApiClient? _api;
    private string? _sessionToken;
    private MockUser? _user;
    private bool _registerMode;

    private readonly Entry _firstNameEntry = new() { Placeholder = "First name", IsVisible = false };
    private readonly Entry _lastNameEntry = new() { Placeholder = "Last name", IsVisible = false };
    private readonly Entry _emailEntry = new() { Placeholder = "Email", Keyboard = Keyboard.Email };
    private readonly Entry _phoneEntry = new() { Placeholder = "Phone (optional)", Keyboard = Keyboard.Telephone, IsVisible = false };
    private readonly Entry _passwordEntry = new() { Placeholder = "Password", IsPassword = true };
    private readonly Entry _confirmEntry = new() { Placeholder = "Confirm password", IsPassword = true, IsVisible = false };
    private readonly Button _signInButton = new() { Text = "Sign in" };
    private readonly Button _registerToggleButton = new() { Text = "Register" };
    private readonly Button _createAccountButton = new() { Text = "Create account", IsVisible = false };
    private readonly Button _logoutButton = new() { Text = "Logout", IsVisible = false };
    private readonly Label _accountLabel = new() { FontSize = 14, TextColor = Colors.Gray };

    public MainPage()
    {
        InitializeComponent();
        BuildAuthUi();
    }

    private void BuildAuthUi()
    {
        if (Content is not Grid grid || grid.Children.Count == 0 || grid.Children[0] is not VerticalStackLayout header)
            return;

        _accountLabel.Text = "Shared catalog from the Web API";
        if (header.Children.Count > 1 && header.Children[1] is Label existing)
            existing.IsVisible = false;

        _signInButton.Clicked += OnSignInClicked;
        _registerToggleButton.Clicked += OnToggleRegisterClicked;
        _createAccountButton.Clicked += OnRegisterClicked;
        _logoutButton.Clicked += OnLogoutClicked;

        header.Add(_accountLabel);
        header.Add(_firstNameEntry);
        header.Add(_lastNameEntry);
        header.Add(_emailEntry);
        header.Add(_phoneEntry);
        header.Add(_passwordEntry);
        header.Add(_confirmEntry);
        header.Add(_signInButton);
        header.Add(_registerToggleButton);
        header.Add(_createAccountButton);
        header.Add(_logoutButton);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _api ??= Handler?.MauiContext?.Services.GetService<ApiClient>();
        if (_api is null)
        {
            StatusLabel.Text = "API client is not registered.";
            return;
        }

        await RestoreSessionAsync();
        await LoadAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private void OnToggleRegisterClicked(object? sender, EventArgs e)
    {
        _registerMode = !_registerMode;
        _registerToggleButton.Text = _registerMode ? "Back to sign in" : "Register";
        UpdateAuthUi();
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        if (_api is null) return;
        _signInButton.IsEnabled = false;
        _signInButton.Text = "Signing in...";
        try
        {
            var result = await _api.LoginAsync(new LoginRequest
            {
                Email = _emailEntry.Text ?? "",
                Password = _passwordEntry.Text ?? "",
                RememberMe = true
            });
            await ApplyAuthResultAsync(result);
        }
        catch
        {
            StatusLabel.Text = "Unable to reach Web API. Start NUBulldogsExchange.Web.Web on port 5016 first.";
        }
        finally
        {
            _signInButton.IsEnabled = true;
            _signInButton.Text = "Sign in";
        }
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        if (_api is null) return;
        _createAccountButton.IsEnabled = false;
        _createAccountButton.Text = "Registering...";
        try
        {
            var result = await _api.RegisterAsync(new RegisterRequest
            {
                FirstName = _firstNameEntry.Text ?? "",
                LastName = _lastNameEntry.Text ?? "",
                Email = _emailEntry.Text ?? "",
                PhoneNumber = _phoneEntry.Text ?? "",
                Password = _passwordEntry.Text ?? "",
                ConfirmPassword = _confirmEntry.Text ?? ""
            });
            await ApplyAuthResultAsync(result, created: true);
        }
        catch
        {
            StatusLabel.Text = "Unable to reach Web API. Start NUBulldogsExchange.Web.Web on port 5016 first.";
        }
        finally
        {
            _createAccountButton.IsEnabled = true;
            _createAccountButton.Text = "Create account";
        }
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (_api is not null && !string.IsNullOrWhiteSpace(_sessionToken))
            await _api.LogoutAsync(_sessionToken);

        _sessionToken = null;
        _user = null;
        try
        {
            SecureStorage.Default.Remove(TokenKey);
        }
        catch
        {
        }

        UpdateAuthUi();
        StatusLabel.Text = "Signed out.";
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_api is null) return;

        var name = await DisplayPromptAsync("New product", "Product name", "Create", "Cancel");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var priceText = await DisplayPromptAsync("Price", "Price in PHP", "Create", "Cancel", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(priceText, out var price) || price < 0)
        {
            await DisplayAlertAsync("Invalid price", "Enter a valid price.", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Creating product…";
            await _api.CreateProductAsync(new Product
            {
                Name = name.Trim(),
                Category = "Accessories",
                Section = "accessories",
                Price = price,
                Stock = 10,
                Sku = $"MOB-{DateTime.UtcNow:HHmmss}",
                Description = name.Trim(),
                FullDescription = name.Trim(),
                ImageUrl = string.Empty,
                InStock = true,
                IsNewArrival = true
            });
            await LoadAsync();
            StatusLabel.Text = "Product created via API.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Failed to create product. Is the Web API running?";
            Console.Error.WriteLine(ex);
        }
    }

    private async Task RestoreSessionAsync()
    {
        if (_api is null) return;
        try
        {
            _sessionToken = await SecureStorage.Default.GetAsync(TokenKey);
            if (string.IsNullOrWhiteSpace(_sessionToken))
                return;

            var result = await _api.ValidateSessionAsync(_sessionToken);
            if (result.Success && result.User is not null)
            {
                _user = result.User;
                _sessionToken = result.SessionToken ?? _sessionToken;
            }
            else
            {
                _sessionToken = null;
                _user = null;
                SecureStorage.Default.Remove(TokenKey);
            }
        }
        catch
        {
        }

        UpdateAuthUi();
    }

    private async Task ApplyAuthResultAsync(AuthResult result, bool created = false)
    {
        if (!result.Success || result.User is null || string.IsNullOrWhiteSpace(result.SessionToken))
        {
            StatusLabel.Text = result.Error ?? "Unable to complete the request.";
            return;
        }

        _user = result.User;
        _sessionToken = result.SessionToken;
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, _sessionToken);
        }
        catch
        {
        }

        _passwordEntry.Text = string.Empty;
        _confirmEntry.Text = string.Empty;
        UpdateAuthUi();
        StatusLabel.Text = created
            ? "Account created successfully."
            : $"Welcome back, {_user.FirstName}.";
    }

    private void UpdateAuthUi()
    {
        var signedIn = _user is not null;
        _emailEntry.IsVisible = !signedIn;
        _passwordEntry.IsVisible = !signedIn;
        _signInButton.IsVisible = !signedIn && !_registerMode;
        _registerToggleButton.IsVisible = !signedIn;
        _createAccountButton.IsVisible = !signedIn && _registerMode;
        _firstNameEntry.IsVisible = !signedIn && _registerMode;
        _lastNameEntry.IsVisible = !signedIn && _registerMode;
        _phoneEntry.IsVisible = !signedIn && _registerMode;
        _confirmEntry.IsVisible = !signedIn && _registerMode;
        _logoutButton.IsVisible = signedIn;
        _accountLabel.Text = signedIn
            ? $"Signed in as {_user!.Name} ({_user.Email})"
            : "Shared catalog from the Web API";
    }

    private async Task LoadAsync()
    {
        if (_api is null) return;

        try
        {
            StatusLabel.Text = "Loading from API…";
            var products = await _api.GetProductsAsync();
            ProductsList.ItemsSource = products;
            StatusLabel.Text = products.Count == 0
                ? "No listings available."
                : $"{products.Count} product(s) from shared database.";
        }
        catch (Exception ex)
        {
            ProductsList.ItemsSource = Array.Empty<Product>();
            StatusLabel.Text = "Unable to reach Web API. Start NUBulldogsExchange.Web.Web on port 5016 first.";
            Console.Error.WriteLine(ex);
        }
    }
}
