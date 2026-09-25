namespace NUBulldogsExchange.Web.Shared.Services;

public interface IProductImageStore
{
    Task<string?> SaveAsync(byte[] bytes, string extension, string contentType);
}
