using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile
{
    public partial class App : Application
    {
        private readonly AuthService _auth;

        public App(AuthService auth)
        {
            InitializeComponent();
            _auth = auth;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell()) { Title = "NU Bulldogs Exchange" };

#if WINDOWS && DEBUG
            // Phone-like preview size for Windows Machine / XAML Live Preview only.
            window.Width = 400;
            window.Height = 820;
            window.X = 80;
            window.Y = 40;
#endif

            window.Created += (_, _) =>
            {
                MainThread.BeginInvokeOnMainThread(async () => await RestoreMobileSessionAsync());
            };

            return window;
        }

        private async Task RestoreMobileSessionAsync()
        {
            var token = await MobileAuthGuard.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return;

            try
            {
                var result = await _auth.RestoreFromTokenAsync(token, rememberMe: true);
                if (!result.Success || result.User is null)
                {
                    await MobileAuthGuard.ClearAsync();
                    return;
                }

                var denied = await MobileAuthGuard.EnforceAsync(_auth, result.User);
                if (denied is null)
                    return;

                await MobileAuthGuard.ClearAsync();
                try
                {
                    await Shell.Current.GoToAsync("login");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await _auth.LogoutAsync();
                await MobileAuthGuard.ClearAsync();
            }
        }
    }
}
