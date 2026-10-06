using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Pages;

public partial class ProductDetailsPage : ContentPage
{
    private readonly ProductDetailsViewModel _vm;

    public ProductDetailsPage(ProductDetailsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.HostPage = this;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Detach();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsSizeGuideModalVisible)
        {
            _vm.IsSizeGuideModalVisible = false;
            return true;
        }
        if (_vm.IsWriteReviewSheetVisible)
        {
            _vm.IsWriteReviewSheetVisible = false;
            return true;
        }
        if (_vm.IsAllReviewsSheetVisible)
        {
            _vm.IsAllReviewsSheetVisible = false;
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
