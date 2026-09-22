using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Models;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class CartViewModel : INotifyPropertyChanged
{
    private readonly CartService _cart;
    private readonly WishlistService _wishlist;
    private readonly AuthService _auth;
    private readonly ToastService _toast;
    private readonly AdminPromotionService _promotions;
    private readonly IAppDatabase _db;
    private Page? _host;

    private readonly HashSet<string> _selectedKeys = [];
    private readonly HashSet<string> _knownKeys = [];

    private string _itemCountLabel = "0 items";
    private string _selectAllLabel = "Select All (0)";
    private bool _allSelected;
    private bool _updatingSelectAll;
    private bool _hasItems;
    private bool _isBusy;
    private bool _applyingPromo;
    private string _promoCode = string.Empty;
    private string _promoMessage = string.Empty;
    private bool _promoApplied;
    private bool _promoSuccess;
    private decimal _discount;
    private string _subtotalText = "₱0";
    private string _discountText = "—";
    private string _deliveryFeeText = "₱0";
    private string _totalText = "₱0";
    private string _checkoutButtonText = "Proceed to Checkout · ₱0";
    private bool _canCheckout;

    /// <summary>Campus pickup is free in the existing storefront — do not invent a delivery fee.</summary>
    private const decimal DeliveryFee = 0m;

    public CartViewModel(
        CartService cart,
        WishlistService wishlist,
        AuthService auth,
        ToastService toast,
        AdminPromotionService promotions,
        IAppDatabase db)
    {
        _cart = cart;
        _wishlist = wishlist;
        _auth = auth;
        _toast = toast;
        _promotions = promotions;
        _db = db;

        IncreaseQuantityCommand = new Command<CartLineItem>(async line => await ChangeQuantityAsync(line, +1));
        DecreaseQuantityCommand = new Command<CartLineItem>(async line => await ChangeQuantityAsync(line, -1));
        RemoveCommand = new Command<CartLineItem>(async line => await RemoveAsync(line));
        SaveCommand = new Command<CartLineItem>(async line => await SaveAsync(line));
        ApplyPromoCommand = new Command(async () => await ApplyPromoAsync(), () => !ApplyingPromo);
        RemovePromoCommand = new Command(RemovePromo);
        CheckoutCommand = new Command(async () => await CheckoutAsync(), () => CanCheckout && !IsBusy);
        StartShoppingCommand = new Command(async () => await GoAsync("//shop"));
        BackCommand = new Command(async () => await GoBackAsync());

        _cart.OnChange += OnCartChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public ObservableCollection<CartLineItem> Lines { get; } = [];

    public string ItemCountLabel
    {
        get => _itemCountLabel;
        private set => SetField(ref _itemCountLabel, value);
    }

    public string SelectAllLabel
    {
        get => _selectAllLabel;
        private set => SetField(ref _selectAllLabel, value);
    }

    public bool AllSelected
    {
        get => _allSelected;
        set
        {
            if (_updatingSelectAll) return;
            if (!SetField(ref _allSelected, value)) return;

            _updatingSelectAll = true;
            try
            {
                foreach (var line in Lines)
                    line.IsSelected = value;

                _selectedKeys.Clear();
                if (value)
                {
                    foreach (var line in Lines)
                        _selectedKeys.Add(line.Key);
                }

                RecalculateTotals();
            }
            finally
            {
                _updatingSelectAll = false;
            }
        }
    }

    public bool HasItems
    {
        get => _hasItems;
        private set
        {
            if (SetField(ref _hasItems, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasCartItems));
                OnPropertyChanged(nameof(IsCartEmpty));
                OnPropertyChanged(nameof(ShowItemCount));
            }
        }
    }

    public bool IsEmpty => !HasItems;
    public bool HasCartItems => HasItems;
    public bool IsCartEmpty => !HasItems;
    public bool ShowItemCount => HasItems;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((Command)CheckoutCommand).ChangeCanExecute();
        }
    }

    public bool ApplyingPromo
    {
        get => _applyingPromo;
        private set
        {
            if (SetField(ref _applyingPromo, value))
                ((Command)ApplyPromoCommand).ChangeCanExecute();
        }
    }

    public string PromoCode
    {
        get => _promoCode;
        set
        {
            if (!SetField(ref _promoCode, value)) return;
            if (_promoApplied)
                RemovePromo();
            else
                PromoMessage = string.Empty;
        }
    }

    public string PromoMessage
    {
        get => _promoMessage;
        private set
        {
            if (SetField(ref _promoMessage, value))
                OnPropertyChanged(nameof(HasPromoMessage));
        }
    }

    public bool HasPromoMessage => !string.IsNullOrWhiteSpace(PromoMessage);

    public bool PromoApplied
    {
        get => _promoApplied;
        private set => SetField(ref _promoApplied, value);
    }

    public bool PromoSuccess
    {
        get => _promoSuccess;
        private set => SetField(ref _promoSuccess, value);
    }

    public string SubtotalText
    {
        get => _subtotalText;
        private set => SetField(ref _subtotalText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        private set => SetField(ref _discountText, value);
    }

    public string DeliveryFeeText
    {
        get => _deliveryFeeText;
        private set => SetField(ref _deliveryFeeText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetField(ref _totalText, value);
    }

    public string CheckoutButtonText
    {
        get => _checkoutButtonText;
        private set => SetField(ref _checkoutButtonText, value);
    }

    public bool CanCheckout
    {
        get => _canCheckout;
        private set
        {
            if (SetField(ref _canCheckout, value))
                ((Command)CheckoutCommand).ChangeCanExecute();
        }
    }

    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ApplyPromoCommand { get; }
    public ICommand RemovePromoCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand StartShoppingCommand { get; }
    public ICommand BackCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _cart.OnChange -= OnCartChanged;
            _cart.OnChange += OnCartChanged;

            await _cart.RestoreAsync(_auth.Email);

            if (!string.IsNullOrWhiteSpace(_cart.AppliedPromoCode))
            {
                _promoCode = _cart.AppliedPromoCode;
                OnPropertyChanged(nameof(PromoCode));
                _promoApplied = true;
                _discount = _cart.AppliedDiscount;
                PromoApplied = true;
                PromoSuccess = true;
            }

            RefreshLines();
            RecalculateTotals();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Detach()
    {
        _cart.OnChange -= OnCartChanged;
        foreach (var line in Lines)
            line.SelectionChanged -= OnLineSelectionChanged;
    }

    private void OnCartChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshLines();
            RecalculateTotals();
            if (PromoApplied && !string.IsNullOrWhiteSpace(PromoCode))
                _ = RevalidatePromoAsync();
        });

    private void RefreshLines()
    {
        var cartKeys = _cart.Items.Select(i => i.Key).ToHashSet();
        _selectedKeys.RemoveWhere(k => !cartKeys.Contains(k));
        _knownKeys.RemoveWhere(k => !cartKeys.Contains(k));

        foreach (var key in cartKeys)
        {
            if (_knownKeys.Add(key))
                _selectedKeys.Add(key);
        }

        foreach (var line in Lines)
            line.SelectionChanged -= OnLineSelectionChanged;

        Lines.Clear();
        foreach (var item in _cart.Items)
        {
            var line = new CartLineItem(item, _selectedKeys.Contains(item.Key));
            line.SelectionChanged += OnLineSelectionChanged;
            Lines.Add(line);
        }

        var qtyCount = _cart.TotalCount;
        ItemCountLabel = qtyCount == 1 ? "1 item" : $"{qtyCount} items";
        var lineCount = Lines.Count;
        SelectAllLabel = lineCount == 1 ? "Select All (1)" : $"Select All ({lineCount})";
        HasItems = lineCount > 0;
        if (!HasItems)
            ItemCountLabel = string.Empty;
        OnPropertyChanged(nameof(ShowItemCount));
        OnPropertyChanged(nameof(HasCartItems));
        OnPropertyChanged(nameof(IsCartEmpty));

        SyncAllSelectedFlag();
    }

    private void OnLineSelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingSelectAll) return;
        if (sender is not CartLineItem line) return;

        if (line.IsSelected)
            _selectedKeys.Add(line.Key);
        else
            _selectedKeys.Remove(line.Key);

        SyncAllSelectedFlag();
        RecalculateTotals();
        if (PromoApplied && !string.IsNullOrWhiteSpace(PromoCode))
            _ = RevalidatePromoAsync();
    }

    private void SyncAllSelectedFlag()
    {
        var value = Lines.Count > 0 && Lines.All(l => l.IsSelected);
        _updatingSelectAll = true;
        try
        {
            if (_allSelected != value)
            {
                _allSelected = value;
                OnPropertyChanged(nameof(AllSelected));
            }
        }
        finally
        {
            _updatingSelectAll = false;
        }
    }

    private IEnumerable<CartLineItem> SelectedLines =>
        Lines.Where(l => l.IsSelected);

    private void RecalculateTotals()
    {
        var subtotal = SelectedLines.Sum(l => l.Product.Price * l.Quantity);
        var discount = PromoApplied ? Math.Min(_discount, subtotal) : 0m;
        var total = Math.Max(0, subtotal - discount + DeliveryFee);

        SubtotalText = $"₱{subtotal:N0}";
        DiscountText = discount > 0 ? $"-₱{discount:N0}" : "—";
        DeliveryFeeText = DeliveryFee > 0 ? $"₱{DeliveryFee:N0}" : "₱0";
        TotalText = $"₱{total:N0}";
        CheckoutButtonText = $"Proceed to Checkout · ₱{total:N0}";
        CanCheckout = SelectedLines.Any();
    }

    private async Task ChangeQuantityAsync(CartLineItem? line, int delta)
    {
        if (line is null || IsBusy) return;

        var next = line.Quantity + delta;
        if (next < 1) return;

        var max = line.Product.Stock > 0 ? line.Product.Stock : int.MaxValue;
        if (next > max)
        {
            _toast.Show("Not enough stock available.");
            return;
        }

        _cart.UpdateQuantity(line.Key, next);
        await _cart.PersistAsync(_auth.Email);
        line.NotifyQuantityChanged();
        RecalculateTotals();
    }

    private async Task RemoveAsync(CartLineItem? line)
    {
        if (line is null) return;
        _selectedKeys.Remove(line.Key);
        _cart.Remove(line.Key);
        await _cart.PersistAsync(_auth.Email);
        _toast.Show("Item removed from cart.");
    }

    private async Task SaveAsync(CartLineItem? line)
    {
        if (line is null) return;

        if (!_auth.IsLoggedIn)
        {
            _toast.Show("Sign in to save items to your wishlist.");
            await GoAsync("login");
            return;
        }

        if (!_wishlist.Contains(line.Product.Id))
            _wishlist.Toggle(line.Product.Id);

        try
        {
            if (!string.IsNullOrWhiteSpace(_auth.Email))
                await _db.SaveWishlistAsync(_auth.Email, _wishlist.Ids);
        }
        catch
        {
        }

        _toast.Show($"Saved {line.Name} to wishlist.");
    }

    private async Task ApplyPromoAsync()
    {
        if (ApplyingPromo || !SelectedLines.Any())
        {
            if (!SelectedLines.Any())
                PromoMessage = "Select at least one item to apply a promo.";
            return;
        }

        ApplyingPromo = true;
        PromoMessage = string.Empty;
        try
        {
            await _promotions.EnsureLoadedAsync();

            var items = SelectedLines.Select(l => new PromoCartItem
            {
                ProductId = l.Product.Id,
                Quantity = l.Quantity,
                UnitPrice = l.Product.Price,
                Category = l.Product.Category
            });

            var (success, message, promo, discount) = await _promotions.ValidateForCartAsync(
                PromoCode,
                items,
                _auth.Email,
                _auth.UserId > 0 ? _auth.UserId : null);

            PromoApplied = success;
            PromoSuccess = success;
            _discount = discount;
            PromoMessage = success
                ? $"Promo code {(promo?.Code ?? PromoCode.Trim().ToUpperInvariant())} applied. You saved ₱{discount:N0}."
                : message;

            if (success && promo is not null)
            {
                _promoCode = promo.Code;
                OnPropertyChanged(nameof(PromoCode));
                _cart.SetPromo(promo.Code, discount);
            }
            else
            {
                _cart.ClearPromo();
            }

            RecalculateTotals();
        }
        catch (Exception)
        {
            PromoApplied = false;
            PromoSuccess = false;
            _discount = 0;
            _cart.ClearPromo();
            PromoMessage = "Unable to validate promo code. Please try again.";
            RecalculateTotals();
        }
        finally
        {
            ApplyingPromo = false;
        }
    }

    private async Task RevalidatePromoAsync()
    {
        if (ApplyingPromo || string.IsNullOrWhiteSpace(PromoCode) || !SelectedLines.Any())
        {
            if (!SelectedLines.Any())
                RemovePromo();
            return;
        }

        try
        {
            await _promotions.EnsureLoadedAsync();
            var items = SelectedLines.Select(l => new PromoCartItem
            {
                ProductId = l.Product.Id,
                Quantity = l.Quantity,
                UnitPrice = l.Product.Price,
                Category = l.Product.Category
            });

            var (success, message, promo, discount) = await _promotions.ValidateForCartAsync(
                PromoCode,
                items,
                _auth.Email,
                _auth.UserId > 0 ? _auth.UserId : null);

            if (success && promo is not null)
            {
                PromoApplied = true;
                PromoSuccess = true;
                _discount = discount;
                _promoCode = promo.Code;
                OnPropertyChanged(nameof(PromoCode));
                _cart.SetPromo(promo.Code, discount);
            }
            else
            {
                PromoApplied = false;
                PromoSuccess = false;
                _discount = 0;
                _cart.ClearPromo();
                PromoMessage = message;
            }

            RecalculateTotals();
        }
        catch
        {
            // Keep current UI state if revalidation fails transiently.
        }
    }

    private void RemovePromo()
    {
        PromoApplied = false;
        PromoSuccess = false;
        _discount = 0;
        PromoMessage = string.Empty;
        _cart.ClearPromo();
        RecalculateTotals();
    }

    private async Task CheckoutAsync()
    {
        if (!CanCheckout || IsBusy) return;

        IsBusy = true;
        try
        {
            // Keep only selected lines in the shared CartService (cart is not cleared).
            var keep = SelectedLines.Select(l => l.Key).ToHashSet();
            foreach (var item in _cart.Items.Where(i => !keep.Contains(i.Key)).ToList())
                _cart.Remove(item.Key);

            if (PromoApplied && !string.IsNullOrWhiteSpace(PromoCode))
            {
                await RevalidatePromoAsync();
                if (!PromoApplied && _auth.IsLoggedIn)
                    return;
                if (PromoApplied)
                    _cart.SetPromo(PromoCode, _discount);
            }
            else
            {
                _cart.ClearPromo();
            }

            await _cart.PersistAsync(_auth.Email);

            if (!_auth.IsLoggedIn)
                MobileCheckoutIntent.SetPending();
            else
                MobileCheckoutIntent.Clear();

            // Always open Checkout — guest sees Login-to-Continue gate; auth sees form.
            await GoAsync("checkout");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task GoAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//home");
        }
        catch
        {
            await Shell.Current.GoToAsync("//home");
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
