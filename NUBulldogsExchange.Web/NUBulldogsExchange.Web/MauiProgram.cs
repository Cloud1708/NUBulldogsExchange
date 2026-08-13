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

            // Add device-specific services used by the NUBulldogsExchange.Web.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<CartService>();
            builder.Services.AddSingleton<WishlistService>();
            builder.Services.AddSingleton<ToastService>();
            builder.Services.AddSingleton<AuthService>();

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
