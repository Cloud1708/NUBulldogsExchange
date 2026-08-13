using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Web.Web.Services
{
    public class FormFactor : IFormFactor
    {
        public string GetFormFactor()
        {
            return "Web";
        }

        public string GetPlatform()
        {
            return Environment.OSVersion.ToString();
        }
    }
}
