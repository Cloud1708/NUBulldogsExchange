using Microsoft.Extensions.Logging;
using NUBulldogsExchange.Mobile.Pages;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Mobile.ViewModels;
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

            // ============================================================
            // Direct Supabase connection
            // ============================================================
            var supabaseOptions = new SupabaseOptions(
                SupabaseClientConfig.Url,
                SupabaseClientConfig.PublishableKey);

            builder.Services.AddSingleton(supabaseOptions);
            builder.Services.AddSingleton<SupabaseSessionState>();

            builder.Services.AddSingleton<SupabaseAppDatabase>(sp =>
            {
                var options = sp.GetRequiredService<SupabaseOptions>();
                var http = new HttpClient
                {
                    BaseAddress = new Uri(options.Url),
                    Timeout = TimeSpan.FromSeconds(30),
                    DefaultRequestVersion = System.Net.HttpVersion.Version11,
                    DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower
                };

                return new SupabaseAppDatabase(
                    http,
                    options,
                    sp.GetRequiredService<SupabaseSessionState>());
            });

            builder.Services.AddSingleton<IAppDatabase>(sp =>
                sp.GetRequiredService<SupabaseAppDatabase>());

            // Existing shared services
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

            // ViewModels
            builder.Services.AddSingleton<HomeViewModel>();
            builder.Services.AddTransient<ShopViewModel>();
            builder.Services.AddTransient<WishlistViewModel>();
            builder.Services.AddTransient<OrdersViewModel>();
            builder.Services.AddTransient<AccountViewModel>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<CartViewModel>();
            builder.Services.AddTransient<CheckoutViewModel>();
            builder.Services.AddTransient<ProductDetailsViewModel>();

            // Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ShopPage>();
            builder.Services.AddTransient<WishlistPage>();
            builder.Services.AddTransient<OrdersPage>();
            builder.Services.AddTransient<AccountPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<CartPage>();
            builder.Services.AddTransient<CheckoutPage>();
            builder.Services.AddTransient<ProductDetailsPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
