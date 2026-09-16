namespace NUBulldogsExchange.Web.Shared.Services
{
    public interface IFormFactor
    {
        public string GetFormFactor();
        public string GetPlatform();
        public bool IsMobile => GetFormFactor().Equals("Mobile", StringComparison.OrdinalIgnoreCase)
                             || GetFormFactor().Equals("Phone", StringComparison.OrdinalIgnoreCase);
    }
}
