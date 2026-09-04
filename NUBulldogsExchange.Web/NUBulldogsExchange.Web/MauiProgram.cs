using Microsoft.Extensions.Logging;
using NUBulldogsExchange.Web.Services;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Web
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
                });

            // Hybrid clients talk to the Web.Web API (central SQLite), not a local marketplace DB.
            var apiBase = DeviceInfo.Platform == DevicePlatform.Android
                ? "http://10.0.2.2:5016/"
                : "http://localhost:5016/";

            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddHttpClient<IAppDatabase, HttpAppDatabase>(client =>
            {
                client.BaseAddress = new Uri(apiBase);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

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

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
