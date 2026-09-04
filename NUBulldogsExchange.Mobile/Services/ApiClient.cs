using System.Net.Http.Json;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Services;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Product>> GetProductsAsync() =>
        await _http.GetFromJsonAsync<List<Product>>("api/catalog/products") ?? [];

    public async Task<Product?> GetProductAsync(int id) =>
        await _http.GetFromJsonAsync<Product>($"api/products/{id}");

    public async Task<Product> CreateProductAsync(Product product)
    {
        var response = await _http.PostAsJsonAsync("api/products", product);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Product>())!;
    }

    public async Task<Product> UpdateProductAsync(Product product)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{product.Id}", product);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Product>())!;
    }

    public async Task DeleteProductAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AdminCategory>> GetCategoriesAsync() =>
        await _http.GetFromJsonAsync<List<AdminCategory>>("api/categories") ?? [];

    public async Task<List<AdminOrder>> GetOrdersAsync() =>
        await _http.GetFromJsonAsync<List<AdminOrder>>("api/orders") ?? [];

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register", request);
        return await ReadAuthAsync(response, "Unable to create your account. Please try again.");
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        return await ReadAuthAsync(response, "Unable to sign in. Please try again.");
    }

    public async Task LogoutAsync(string sessionToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        if (!string.IsNullOrWhiteSpace(sessionToken))
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
        try
        {
            await _http.SendAsync(message);
        }
        catch
        {
        }
    }

    public async Task<AuthResult> ValidateSessionAsync(string sessionToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
        var response = await _http.SendAsync(message);
        return await ReadAuthAsync(response, "Your session has expired. Please sign in again.");
    }

    public async Task<List<AdminCustomer>> GetCustomersAsync() =>
        await _http.GetFromJsonAsync<List<AdminCustomer>>("api/customers") ?? [];

    public async Task<PromoValidationResult> ValidatePromotionAsync(PromoValidationRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/promotions/validate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PromoValidationResult>()
               ?? new PromoValidationResult { Valid = false, Message = "Unable to validate promo code." };
    }

    public async Task<List<ActivePromotionDto>> GetActivePromotionsAsync() =>
        await _http.GetFromJsonAsync<List<ActivePromotionDto>>("api/promotions/active") ?? [];

    private static async Task<AuthResult> ReadAuthAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            if (result is not null)
            {
                if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
                    result.Error = fallback;
                return result;
            }
        }
        catch
        {
        }

        return new AuthResult { Success = false, Error = fallback };
    }
}
