namespace NUBulldogsExchange.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("login", typeof(Pages.LoginPage));
            Routing.RegisterRoute("register", typeof(Pages.RegisterPage));
            Routing.RegisterRoute("cart", typeof(Pages.CartPage));
            Routing.RegisterRoute("checkout", typeof(Pages.CheckoutPage));
            Routing.RegisterRoute("product", typeof(Pages.ProductDetailsPage));
        }
    }
}
