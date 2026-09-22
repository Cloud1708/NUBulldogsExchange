using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class WishlistViewModel : INotifyPropertyChanged
{
    private readonly ProductCatalogService _catalog;
    private readonly WishlistService _wishlist;
    private readonly CartService _cart;
    private readonly AuthService _auth;
    private readonly ToastService _toast;
    private readonly IAppDatabase _db;
    private readonly AdminProductService _adminProducts;

    private string _countLabel = string.Empty;
    private bool _hasItems;
    private bool _isBusy;

    public WishlistViewModel(
        ProductCatalogService catalog,
        WishlistService wishlist,
        CartService cart,
        AuthService auth,
        ToastService toast,
        IAppDatabase db,
        AdminProductService adminProducts)
    {
        _catalog = catalog;
        _wishlist = wishlist;
        _cart = cart;
        _auth = auth;
        _toast = toast;
        _db = db;
        _adminProducts = adminProducts;

        ExploreProductsCommand = new Command(async () => await GoAsync("//shop"));
        RemoveFromWishlistCommand = new Command<Product>(async p => await OnRemoveAsync(p));
        AddToCartCommand = new Command<Product>(async p => await OnAddToCartAsync(p));
        OpenProductCommand = new Command<Product>(_ => { });

        _wishlist.OnChange += OnWishlistChanged;
        _catalog.OnChange += OnWishlistChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Product> WishlistItems { get; } = [];

    public string CountLabel
    {
        get => _countLabel;
        private set => SetField(ref _countLabel, value);
    }

    public bool HasWishlistItems
    {
        get => _hasItems;
        private set
        {
            if (SetField(ref _hasItems, value))
                OnPropertyChanged(nameof(IsWishlistEmpty));
        }
    }

    public bool IsWishlistEmpty => !HasWishlistItems;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public ICommand ExploreProductsCommand { get; }
    public ICommand RemoveFromWishlistCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand OpenProductCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _adminProducts.EnsureLoadedAsync();
            await MobileCatalogSeeder.EnsureSampleProductsAsync(_db, _catalog);
            await _cart.RestoreAsync(_auth.Email);

            if (!string.IsNullOrWhiteSpace(_auth.Email))
            {
                try
                {
                    var ids = await _db.GetWishlistAsync(_auth.Email);
                    foreach (var id in ids)
                    {
                        if (!_wishlist.Contains(id))
                            _wishlist.Toggle(id);
                    }
                }
                catch
                {
                }
            }

            RefreshItems();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Detach()
    {
        _wishlist.OnChange -= OnWishlistChanged;
        _catalog.OnChange -= OnWishlistChanged;
    }

    private void OnWishlistChanged() =>
        MainThread.BeginInvokeOnMainThread(RefreshItems);

    private void RefreshItems()
    {
        var items = _catalog.Products
            .Where(p => _wishlist.Contains(p.Id))
            .ToList();

        WishlistItems.Clear();
        foreach (var product in items)
            WishlistItems.Add(product);

        var count = items.Count;
        CountLabel = count == 1 ? "1 saved item" : $"{count} saved items";
        HasWishlistItems = count > 0;
    }

    private async Task OnRemoveAsync(Product? product)
    {
        if (product is null) return;
        if (!_wishlist.Contains(product.Id))
            return;

        _wishlist.Toggle(product.Id);
        try
        {
            if (!string.IsNullOrWhiteSpace(_auth.Email))
                await _db.SaveWishlistAsync(_auth.Email, _wishlist.Ids);
        }
        catch
        {
        }

        _toast.Show($"Removed {product.Name} from Wishlist.");
        RefreshItems();
    }

    private async Task OnAddToCartAsync(Product? product)
    {
        if (product is null) return;
        _cart.Add(product, 1, product.Colors.FirstOrDefault(), product.Sizes.FirstOrDefault());
        await _cart.PersistAsync(_auth.Email);
        _toast.Show($"Added {product.Name} to cart!");
    }

    private static async Task GoAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
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
