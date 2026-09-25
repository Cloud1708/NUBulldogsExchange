using NUBulldogsExchange.Web.Shared.Services;
using NUBulldogsExchange.Web.Web.Components;
using NUBulldogsExchange.Web.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ============================================================
// Supabase
// ============================================================
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Missing Supabase:Url in appsettings.json.");
var supabasePublishableKey = builder.Configuration["Supabase:PublishableKey"]
    ?? throw new InvalidOperationException("Missing Supabase:PublishableKey in appsettings.json.");

builder.Services.AddSingleton(new SupabaseOptions(
    supabaseUrl,
    supabasePublishableKey));

// IMPORTANT: scoped on Web so one logged-in browser circuit never shares
// its Supabase access token with another user.
builder.Services.AddScoped<SupabaseSessionState>();

builder.Services.AddHttpClient("Supabase", (sp, client) =>
{
    var options = sp.GetRequiredService<SupabaseOptions>();
    client.BaseAddress = new Uri(options.Url);
    client.Timeout = TimeSpan.FromSeconds(30);
    // Avoid HTTP/2 negotiation issues that surface as HttpRequestException.
    client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
    client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
});

builder.Services.AddSingleton<IProductImageStore, WebProductImageStore>();

builder.Services.AddScoped<SupabaseAppDatabase>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SupabaseAppDatabase(
        factory.CreateClient("Supabase"),
        sp.GetRequiredService<SupabaseOptions>(),
        sp.GetRequiredService<SupabaseSessionState>(),
        sp.GetService<IProductImageStore>());
});

builder.Services.AddScoped<IAppDatabase>(sp =>
    sp.GetRequiredService<SupabaseAppDatabase>());

// ============================================================
// Existing application services
// ============================================================
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AdminProductService>();
builder.Services.AddScoped<AdminCategoryService>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddScoped<AdminSettingsService>();
builder.Services.AddScoped<AdminInventoryService>();
builder.Services.AddScoped<AdminCustomerService>();
builder.Services.AddScoped<AdminStaffService>();
builder.Services.AddScoped<AdminPromotionService>();
builder.Services.AddScoped<AdminReportService>();
builder.Services.AddScoped<AdminNotificationService>();

var app = builder.Build();

// Supabase already owns/initializes the database, so there is no local
// SQLite DatabaseService.InitializeAsync() call here.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.UseStaticFiles();
app.MapStaticAssets();

// No custom /api or PHP bulldogs_api is required for this direct-Supabase setup.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(NUBulldogsExchange.Web.Shared._Imports).Assembly);

app.Run();
