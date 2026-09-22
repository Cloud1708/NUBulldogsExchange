using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Admin product photos are stored as <c>data:image/...;base64,...</c> data URIs.
/// MAUI Image cannot load those via FromUri / string Source, so they must be
/// decoded into a stream (or left as http/https UriImageSource).
/// </summary>
public static class ProductImageHelper
{
    // 1x1 transparent PNG so recycled cards never keep a previous product photo.
    private static readonly byte[] TransparentPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    private static readonly ImageSource EmptySource =
        ImageSource.FromStream(() => new MemoryStream(TransparentPng, writable: false));

    public static ImageSource FromProduct(Product? product)
    {
        if (product is null)
            return EmptySource;

        foreach (var candidate in EnumerateCandidates(product))
        {
            var source = TryCreate(candidate);
            if (source is not null)
                return source;
        }

        return EmptySource;
    }

    public static ImageSource FromUrl(string? url)
    {
        return TryCreate(url) ?? EmptySource;
    }

    private static IEnumerable<string> EnumerateCandidates(Product product)
    {
        if (!string.IsNullOrWhiteSpace(product.ImageUrl))
            yield return product.ImageUrl;

        if (product.Images is null)
            yield break;

        foreach (var image in product.Images)
        {
            if (!string.IsNullOrWhiteSpace(image) &&
                !image.Equals(product.ImageUrl, StringComparison.Ordinal))
            {
                yield return image;
            }
        }
    }

    private static ImageSource? TryCreate(string? raw)
    {
        var url = raw?.Trim();
        if (string.IsNullOrWhiteSpace(url) ||
            url.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return FromDataUri(url);

        if (url.StartsWith("//", StringComparison.Ordinal))
            url = "https:" + url;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new UriImageSource
                {
                    Uri = new Uri(url),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(7)
                };
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static ImageSource? FromDataUri(string dataUri)
    {
        var comma = dataUri.IndexOf(',');
        if (comma <= 5)
            return null;

        var header = dataUri[..comma];
        // MAUI's raster decoder cannot render SVG placeholders used on the web.
        if (header.Contains("svg", StringComparison.OrdinalIgnoreCase))
            return null;

        var payload = dataUri[(comma + 1)..].Trim();
        if (payload.Length == 0)
            return null;

        try
        {
            byte[] bytes;
            if (header.Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                payload = payload.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(" ", string.Empty);
                var pad = payload.Length % 4;
                if (pad > 0)
                    payload = payload.PadRight(payload.Length + (4 - pad), '=');

                bytes = Convert.FromBase64String(payload);
            }
            else
            {
                bytes = System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            }

            if (bytes.Length == 0)
                return null;

            return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
        }
        catch
        {
            return null;
        }
    }
}
