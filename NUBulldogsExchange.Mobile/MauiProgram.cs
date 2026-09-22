using Microsoft.Extensions.Logging;
using NUBulldogsExchange.Mobile.Pages;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Mobile.ViewModels;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IFormFactor, FormFactor>();

            // Direct SQLite on Windows; HTTP API on Android/iOS.
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                builder.Services.AddSingleton<DatabaseService>(_ =>
                    new DatabaseService(ResolveWindowsDatabasePath()));
                builder.Services.AddSingleton<IAppDatabase>(sp => sp.GetRequiredService<DatabaseService>());
            }
            else
            {
                builder.Services.AddHttpClient<IAppDatabase, HttpAppDatabase>(client =>
                {
                    client.BaseAddress = new Uri(MobileWebUrls.ApiBase);
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
            }

            builder.Services.AddSingleton<ProductCatalogService>();
            builder.Services.AddSingleton<CartService>();
            builder.Services.AddSingleton<WishlistService>();
            builder.Services.AddSingleton<ToastService>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<OrderService>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<AdminProductService>();
            builder.Services.AddSingleton<AdminCategoryService>();
            builder.Services.AddSingleton<AdminOrderService>();
            builder.Services.AddSingleton<AdminSettingsService>();
            builder.Services.AddSingleton<AdminInventoryService>();
            builder.Services.AddSingleton<AdminCustomerService>();
            builder.Services.AddSingleton<AdminStaffService>();
            builder.Services.AddSingleton<AdminPromotionService>();
            builder.Services.AddSingleton<AdminReportService>();
            builder.Services.AddSingleton<AdminNotificationService>();

            builder.Services.AddSingleton<HomeViewModel>();
            builder.Services.AddTransient<ShopViewModel>();
            builder.Services.AddTransient<WishlistViewModel>();
            builder.Services.AddTransient<OrdersViewModel>();
            builder.Services.AddTransient<AccountViewModel>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<CartViewModel>();
            builder.Services.AddTransient<CheckoutViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ShopPage>();
            builder.Services.AddTransient<WishlistPage>();
            builder.Services.AddTransient<OrdersPage>();
            builder.Services.AddTransient<AccountPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<CartPage>();
            builder.Services.AddTransient<CheckoutPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        /// <summary>
        /// Prefer the shared Web.Web App_Data database when running from the repo,
        /// so Mobile Windows preview uses the same storefront data as the web API.
        /// </summary>
        private static string? ResolveWindowsDatabasePath()
        {
            try
            {
                var current = new DirectoryInfo(AppContext.BaseDirectory);
                while (current is not null)
                {
                    var webDb = Path.Combine(current.FullName,
                        "NUBulldogsExchange.Web", "NUBulldogsExchange.Web.Web", "App_Data", "NUBulldogsExchange.db");
                    if (File.Exists(webDb))
                        return webDb;

                    var webDbAlt = Path.Combine(current.FullName,
                        "NUBulldogsExchange.Web.Web", "App_Data", "NUBulldogsExchange.db");
                    if (File.Exists(webDbAlt))
                        return webDbAlt;

                    current = current.Parent;
                }
            }
            catch
            {
            }

            return null; // DatabaseService default resolver
        }
    }
}
