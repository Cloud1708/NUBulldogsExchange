using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.Services
{
    public class FormFactor : IFormFactor
    {
        public string GetFormFactor()
        {
            return "Mobile";
        }

        public string GetPlatform()
        {
            return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
        }
    }
}
