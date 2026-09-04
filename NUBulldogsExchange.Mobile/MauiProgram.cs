using Microsoft.Extensions.Logging;
using NUBulldogsExchange.Mobile.Services;

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

            var apiBase = DeviceInfo.Platform == DevicePlatform.Android
                ? "http://10.0.2.2:5016/"
                : "http://localhost:5016/";

            builder.Services.AddHttpClient<ApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBase);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
