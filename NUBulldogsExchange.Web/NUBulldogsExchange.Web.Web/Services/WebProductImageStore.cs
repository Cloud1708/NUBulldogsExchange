using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Web.Web.Services;

public sealed class WebProductImageStore : IProductImageStore
{
    private readonly IWebHostEnvironment _env;

    public WebProductImageStore(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string?> SaveAsync(byte[] bytes, string extension, string contentType)
    {
        if (bytes.Length == 0)
            return null;

        var ext = string.IsNullOrWhiteSpace(extension) ? "jpg" : extension.Trim().TrimStart('.');
        var dir = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return $"/uploads/products/{fileName}";
    }
}
