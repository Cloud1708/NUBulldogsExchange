using System.Windows.Input;
using NUBulldogsExchange.Mobile.ViewModels;

namespace NUBulldogsExchange.Mobile.Controls;

public partial class OrderCardView : ContentView
{
    public static readonly BindableProperty ViewOrderCommandProperty =
        BindableProperty.Create(nameof(ViewOrderCommand), typeof(ICommand), typeof(OrderCardView));

    public static readonly BindableProperty CancelOrderCommandProperty =
        BindableProperty.Create(nameof(CancelOrderCommand), typeof(ICommand), typeof(OrderCardView));

    public static readonly BindableProperty DetailsCommandProperty =
        BindableProperty.Create(nameof(DetailsCommand), typeof(ICommand), typeof(OrderCardView));

    public static readonly BindableProperty TrackCommandProperty =
        BindableProperty.Create(nameof(TrackCommand), typeof(ICommand), typeof(OrderCardView));

    public static readonly BindableProperty BuyAgainCommandProperty =
        BindableProperty.Create(nameof(BuyAgainCommand), typeof(ICommand), typeof(OrderCardView));

    public static readonly BindableProperty ReviewCommandProperty =
        BindableProperty.Create(nameof(ReviewCommand), typeof(ICommand), typeof(OrderCardView));

    public OrderCardView()
    {
        InitializeComponent();
    }

    public ICommand? ViewOrderCommand
    {
        get => (ICommand?)GetValue(ViewOrderCommandProperty);
        set => SetValue(ViewOrderCommandProperty, value);
    }

    public ICommand? CancelOrderCommand
    {
        get => (ICommand?)GetValue(CancelOrderCommandProperty);
        set => SetValue(CancelOrderCommandProperty, value);
    }

    public ICommand? DetailsCommand
    {
        get => (ICommand?)GetValue(DetailsCommandProperty);
        set => SetValue(DetailsCommandProperty, value);
    }

    public ICommand? TrackCommand
    {
        get => (ICommand?)GetValue(TrackCommandProperty);
        set => SetValue(TrackCommandProperty, value);
    }

    public ICommand? BuyAgainCommand
    {
        get => (ICommand?)GetValue(BuyAgainCommandProperty);
        set => SetValue(BuyAgainCommandProperty, value);
    }

    public ICommand? ReviewCommand
    {
        get => (ICommand?)GetValue(ReviewCommandProperty);
        set => SetValue(ReviewCommandProperty, value);
    }
}
