using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell()) { Title = "NU Bulldogs Exchange" };

#if WINDOWS && DEBUG
            // Phone-like preview size for Windows Machine / XAML Live Preview only.
            window.Width = 400;
            window.Height = 820;
            window.X = 80;
            window.Y = 40;
#endif

            return window;
        }
    }
}
