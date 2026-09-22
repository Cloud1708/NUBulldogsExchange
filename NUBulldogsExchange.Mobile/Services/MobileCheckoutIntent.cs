namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Remembers that the user started checkout as a guest so Login/Register
/// can return them to Checkout without clearing the cart.
/// </summary>
public static class MobileCheckoutIntent
{
    public static bool PendingCheckout { get; private set; }

    public static void SetPending() => PendingCheckout = true;

    public static void Clear() => PendingCheckout = false;

    public static bool Consume()
    {
        if (!PendingCheckout) return false;
        PendingCheckout = false;
        return true;
    }

    /// <summary>
    /// After successful Login/Register, resume Checkout if that was the intent;
    /// otherwise go to Account.
    /// </summary>
    public static async Task NavigateAfterAuthAsync()
    {
        if (!Consume())
        {
            await Shell.Current.GoToAsync("//account");
            return;
        }

        try
        {
            var stack = Shell.Current.Navigation.NavigationStack;
            for (var i = stack.Count - 2; i >= 0; i--)
            {
                if (stack[i] is Pages.CheckoutPage)
                {
                    var pops = stack.Count - 1 - i;
                    for (var p = 0; p < pops; p++)
                        await Shell.Current.GoToAsync("..");
                    return;
                }
            }

            await Shell.Current.GoToAsync("checkout");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            try { await Shell.Current.GoToAsync("checkout"); }
            catch { await Shell.Current.GoToAsync("//account"); }
        }
    }
}
