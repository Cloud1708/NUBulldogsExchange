using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Models;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class OrderStatusChip : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Key { get; init; } = "All";
    public string Label { get; init; } = "All";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextColor)));
        }
    }

    public Color BackgroundColor => IsSelected
        ? Color.FromArgb("#00205B")
        : Color.FromArgb("#F1F5F9");

    public Color TextColor => IsSelected
        ? Colors.White
        : Color.FromArgb("#475569");

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class OrderDetailTimelineStep
{
    public int Index { get; init; }
    public string Label { get; init; } = string.Empty;
    public bool IsDone { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsPending { get; init; }
    public bool IsCancelled { get; init; }
    public bool ShowConnectingLine { get; init; }

    public Color DotColor => IsCancelled ? Color.FromArgb("#EF4444")
        : (IsDone || IsCurrent) ? Color.FromArgb("#00205B")
        : Color.FromArgb("#CBD5E1");

    public Color LineColor => IsDone ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");

    public Color LabelColor => IsCancelled ? Color.FromArgb("#EF4444")
        : IsCurrent ? Color.FromArgb("#00205B")
        : IsDone ? Color.FromArgb("#1E293B")
        : Color.FromArgb("#94A3B8");

    public FontAttributes LabelFontAttributes => (IsCurrent || IsDone) ? FontAttributes.Bold : FontAttributes.None;

    public string StepIcon => IsCancelled ? Helpers.MaterialIconCodes.Close
        : IsDone ? Helpers.MaterialIconCodes.Check
        : "";
    public bool HasIcon => IsCancelled || IsDone;
    public string StepNumber => (Index + 1).ToString();
}

public sealed class OrderDetailItemModel : INotifyPropertyChanged
{
    public required MockOrderItem Item { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public int ProductId => Item.ProductId;
    public long OrderItemId => Item.OrderItemId;
    public string Name => Item.Name;
    public string ImageUrl => Item.ImageUrl;
    public decimal Price => Item.Price;
    public int Quantity => Item.Quantity;
    public decimal Total => Item.Price * Item.Quantity;
    public string PriceAndQtyText => $"Qty: {Item.Quantity} × ₱{Item.Price:N0}";
    public string TotalText => $"₱{Total:N0}";

    public string VariantText
    {
        get
        {
            if (Item.HasDisplayColor && Item.HasDisplaySize)
                return $"Color: {Item.Color} • Size: {Item.Size}";
            if (Item.HasDisplaySize)
                return $"Size: {Item.Size}";
            if (Item.HasDisplayColor)
                return $"Color: {Item.Color}";
            return string.Empty;
        }
    }

    public bool HasVariantText => !string.IsNullOrWhiteSpace(VariantText);

    public bool CanWriteReview { get; set; }

    public bool IsReviewed
    {
        get => Item.IsReviewed;
        set
        {
            if (Item.IsReviewed == value) return;
            Item.IsReviewed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowWriteReviewButton));
            OnPropertyChanged(nameof(ShowReviewedBadge));
        }
    }

    public bool ShowWriteReviewButton => CanWriteReview && !IsReviewed;
    public bool ShowReviewedBadge => CanWriteReview && IsReviewed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>UI projection for an order card matching customer order design.</summary>
public sealed class OrderCardModel : INotifyPropertyChanged
{
    public required MockOrder Order { get; init; }
    public string DisplayId => Order.Id.TrimStart('#');
    public string CustomerCategory => Order.CustomerCategory;
    public string DateAndItemsText => $"{Order.FormattedDate} • {Order.ItemCountLabel}";
    public string DateText => Order.FormattedDate;
    public string Status => Order.Status;
    public string TotalText => $"₱{Order.Total:N0}";
    public IReadOnlyList<MockOrderItem> Items => Order.Items;
    public bool CanCancel => Order.CanCancel;
    public bool CanConfirmReceived => Order.CanConfirmReceived;

    public string FulfillmentBadgeText => Order.IsDelivery ? "Delivery" : "Campus Pickup";
    public string FulfillmentIcon => Order.IsDelivery ? Helpers.MaterialIconCodes.LocalShipping : Helpers.MaterialIconCodes.LocationOn;
    public string FulfillmentLabel => Order.IsDelivery ? "Delivery" : "Campus Pickup";
    public string FulfillmentLocationText =>
        string.IsNullOrWhiteSpace(Order.FulfillmentLocationLabel)
            ? (Order.IsDelivery ? "Delivery address on file" : OrderFlow.PickupLocation)
            : Order.FulfillmentLocationLabel;

    public string PaymentMethodText => $"Payment: {(string.IsNullOrWhiteSpace(Order.PaymentMethod) ? "—" : Order.PaymentMethod)}";
    public string PaymentStatusText => $"Status: {Order.PaymentStatus}";

    public Color BadgeBackground => Order.CustomerCategory switch
    {
        OrderFlow.ToPay => Color.FromArgb("#FEF3C7"),
        OrderFlow.ToProcess => Color.FromArgb("#DBEAFE"),
        OrderFlow.ReadyForPickup => Color.FromArgb("#E0F2FE"),
        OrderFlow.ToReceive => Color.FromArgb("#E0F2FE"),
        OrderFlow.Completed => Color.FromArgb("#DCFCE7"),
        OrderFlow.Cancelled => Color.FromArgb("#FEE2E2"),
        "Pending" => Color.FromArgb("#FEF3C7"),
        "Confirmed" => Color.FromArgb("#DBEAFE"),
        "Processing" => Color.FromArgb("#F3E8FF"),
        _ => Color.FromArgb("#F1F5F9")
    };

    public Color BadgeTextColor => Order.CustomerCategory switch
    {
        OrderFlow.ToPay => Color.FromArgb("#D97706"),
        OrderFlow.ToProcess => Color.FromArgb("#2563EB"),
        OrderFlow.ReadyForPickup => Color.FromArgb("#0284C7"),
        OrderFlow.ToReceive => Color.FromArgb("#0369A1"),
        OrderFlow.Completed => Color.FromArgb("#16A34A"),
        OrderFlow.Cancelled => Color.FromArgb("#EF4444"),
        "Pending" => Color.FromArgb("#D97706"),
        "Confirmed" => Color.FromArgb("#2563EB"),
        "Processing" => Color.FromArgb("#7C3AED"),
        _ => Color.FromArgb("#475569")
    };

    public bool ShowTrack => Order.Status is "Pending" or "Confirmed" or "Processing" or "Ready for Pickup"
        or "Out for Delivery" or "Preparing" or "Shipped";
    public bool ShowBuyAgain => Order.Status is "Completed" or "Delivered";
    public bool ShowReview => Order.Status is "Completed" or "Delivered";
    public bool CanWriteReview => Order.CanWriteReview && Order.HasUnreviewedItems;
    public bool ShowReviewButton => Order.CanWriteReview && Order.HasUnreviewedItems;
    public bool ShowReviewedBadge => Order.CanWriteReview && Order.AllItemsReviewed;
    public bool ShowDetails => true;

    public void RefreshReviewState() => RefreshState();

    public void RefreshState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanWriteReview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowReviewButton)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowReviewedBadge)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConfirmReceived)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCancel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomerCategory)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BadgeBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BadgeTextColor)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class OrdersViewModel : INotifyPropertyChanged
{
    private readonly IAppDatabase _db;
    private readonly OrderService _orders;
    private readonly AuthService _auth;
    private readonly CartService _cart;
    private readonly ProductCatalogService _catalog;
    private readonly ToastService _toast;
    private readonly NotificationService _notifications;

    private string _selectedStatus = "All";
    private bool _hasOrders;
    private bool _isBusy;
    private Page? _host;

    // Order Details sheet state
    private bool _isOrderDetailsVisible;
    private MockOrder? _selectedOrder;
    private OrderCardModel? _selectedOrderCard;
    private string _detailsStatusTitle = string.Empty;
    private string _detailsStatusMessage = string.Empty;
    private string _detailsStatusIcon = Helpers.MaterialIconCodes.LocalShipping;
    private string _detailsSubtotalText = "₱0";

    // Review sheet state
    private bool _isReviewSheetVisible;
    private OrderCardModel? _reviewingCard;
    private MockOrder? _reviewingOrder;
    private ReviewableItemModel? _selectedReviewItem;
    private int _reviewRating = 5;
    private string _reviewTitle = string.Empty;
    private string _reviewComment = string.Empty;
    private string _reviewError = string.Empty;
    private bool _isSubmittingReview;

    public OrdersViewModel(
        IAppDatabase db,
        OrderService orders,
        AuthService auth,
        CartService cart,
        ProductCatalogService catalog,
        ToastService toast,
        NotificationService notifications)
    {
        _db = db;
        _orders = orders;
        _auth = auth;
        _cart = cart;
        _catalog = catalog;
        _toast = toast;
        _notifications = notifications;

        SelectStatusCommand = new Command<OrderStatusChip>(OnSelectStatus);
        StartShoppingCommand = new Command(async () => await GoAsync("//shop"));
        ViewOrderCommand = new Command<OrderCardModel>(o => OnViewOrder(o));
        CancelOrderCommand = new Command<OrderCardModel>(async o => await OnCancelOrderAsync(o));
        DetailsCommand = new Command<OrderCardModel>(o => OnViewOrder(o));
        TrackCommand = new Command<OrderCardModel>(o => OnTrack(o));
        BuyAgainCommand = new Command<OrderCardModel>(async o => await OnBuyAgainAsync(o));
        ReviewCommand = new Command<OrderCardModel>(o => OnReview(o));
        ConfirmReceivedCommand = new Command<OrderCardModel?>(async o => await OnConfirmReceivedAsync(o));
        CloseOrderDetailsCommand = new Command(OnCloseOrderDetails);
        WriteItemReviewCommand = new Command<OrderDetailItemModel>(OnWriteItemReview);
        CancelOrderFromDetailsCommand = new Command(async () => await OnCancelOrderAsync(null));

        SelectReviewItemCommand = new Command<ReviewableItemModel>(OnSelectReviewItem);
        SetRatingCommand = new Command<string>(s => { if (int.TryParse(s, out var r)) SetRating(r); });
        SubmitReviewCommand = new Command(async () => await OnSubmitReviewAsync(), () => !IsSubmittingReview);
        CloseReviewSheetCommand = new Command(OnCloseReviewSheet);

        _orders.OnChange += () => MainThread.BeginInvokeOnMainThread(ApplyFilter);
        BuildStatusChips();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public ObservableCollection<OrderStatusChip> StatusChips { get; } = [];
    public ObservableCollection<OrderCardModel> FilteredOrders { get; } = [];
    public ObservableCollection<ReviewableItemModel> ReviewItems { get; } = [];
    public ObservableCollection<OrderDetailTimelineStep> TimelineSteps { get; } = [];
    public ObservableCollection<OrderDetailItemModel> DetailItems { get; } = [];

    public bool IsOrderDetailsVisible
    {
        get => _isOrderDetailsVisible;
        set => SetField(ref _isOrderDetailsVisible, value);
    }

    public MockOrder? SelectedOrder
    {
        get => _selectedOrder;
        private set
        {
            if (SetField(ref _selectedOrder, value))
            {
                OnPropertyChanged(nameof(DetailsDisplayId));
                OnPropertyChanged(nameof(DetailsDateAndItemsText));
                OnPropertyChanged(nameof(DetailsCustomerCategory));
                OnPropertyChanged(nameof(DetailsBadgeBackground));
                OnPropertyChanged(nameof(DetailsBadgeTextColor));
                OnPropertyChanged(nameof(DetailsIsDelivery));
                OnPropertyChanged(nameof(DetailsRecipientName));
                OnPropertyChanged(nameof(DetailsRecipientPhone));
                OnPropertyChanged(nameof(DetailsShippingAddress));
                OnPropertyChanged(nameof(DetailsPickupLocation));
                OnPropertyChanged(nameof(DetailsPickupHours));
                OnPropertyChanged(nameof(DetailsPaymentMethod));
                OnPropertyChanged(nameof(DetailsPaymentStatus));
                OnPropertyChanged(nameof(DetailsPaymentHeadline));
                OnPropertyChanged(nameof(DetailsPaymentDetail));
                OnPropertyChanged(nameof(DetailsHasDiscount));
                OnPropertyChanged(nameof(DetailsDiscountText));
                OnPropertyChanged(nameof(DetailsFeeLabel));
                OnPropertyChanged(nameof(DetailsFeeText));
                OnPropertyChanged(nameof(DetailsTotalText));
                OnPropertyChanged(nameof(DetailsCanConfirmReceived));
                OnPropertyChanged(nameof(DetailsCanCancel));
            }
        }
    }

    public string DetailsDisplayId => SelectedOrder?.Id.TrimStart('#') ?? string.Empty;
    public string DetailsDateAndItemsText => SelectedOrder != null ? $"{SelectedOrder.FormattedDate} • {SelectedOrder.ItemCountLabel}" : string.Empty;
    public string DetailsCustomerCategory => SelectedOrder?.CustomerCategory ?? string.Empty;
    public Color DetailsBadgeBackground => _selectedOrderCard?.BadgeBackground ?? Color.FromArgb("#F1F5F9");
    public Color DetailsBadgeTextColor => _selectedOrderCard?.BadgeTextColor ?? Color.FromArgb("#475569");

    public string DetailsStatusTitle
    {
        get => _detailsStatusTitle;
        set => SetField(ref _detailsStatusTitle, value);
    }

    public string DetailsStatusMessage
    {
        get => _detailsStatusMessage;
        set => SetField(ref _detailsStatusMessage, value);
    }

    public string DetailsStatusIcon
    {
        get => _detailsStatusIcon;
        set => SetField(ref _detailsStatusIcon, value);
    }

    public bool DetailsIsDelivery => SelectedOrder?.IsDelivery == true;
    public string DetailsRecipientName => SelectedOrder?.RecipientName ?? "—";
    public string DetailsRecipientPhone => string.IsNullOrWhiteSpace(SelectedOrder?.RecipientPhone) ? "—" : SelectedOrder.RecipientPhone;
    public string DetailsShippingAddress => string.IsNullOrWhiteSpace(SelectedOrder?.ShippingAddressLabel) ? "Delivery address on file" : SelectedOrder.ShippingAddressLabel;
    public string DetailsPickupLocation => OrderFlow.PickupLocation;
    public string DetailsPickupHours => $"{OrderFlow.PickupHoursDays} ({OrderFlow.PickupHoursTime})";

    public string DetailsPaymentMethod => string.IsNullOrWhiteSpace(SelectedOrder?.PaymentMethod) ? "—" : SelectedOrder.PaymentMethod;
    public string DetailsPaymentStatus => SelectedOrder?.PaymentStatus ?? "—";
    public string DetailsPaymentHeadline => OrderFlow.PaymentHeadline(SelectedOrder?.PaymentStatus);
    public string DetailsPaymentDetail => OrderFlow.PaymentDetail(SelectedOrder?.PaymentMethod, SelectedOrder?.PaymentStatus);

    public string DetailsSubtotalText
    {
        get => _detailsSubtotalText;
        set => SetField(ref _detailsSubtotalText, value);
    }

    public bool DetailsHasDiscount => (SelectedOrder?.DiscountAmount ?? 0) > 0;
    public string DetailsDiscountText => $"-₱{SelectedOrder?.DiscountAmount ?? 0:N0}";
    public string DetailsFeeLabel => (SelectedOrder?.IsDelivery == true) ? "Delivery Fee" : "Fulfillment Fee";
    public string DetailsFeeText => (SelectedOrder?.ShippingFee ?? 0) > 0 ? $"₱{SelectedOrder!.ShippingFee:N0}" : "Free";
    public string DetailsTotalText => $"₱{SelectedOrder?.Total ?? 0:N0}";
    public bool DetailsCanConfirmReceived => SelectedOrder?.CanConfirmReceived == true;
    public bool DetailsCanCancel => SelectedOrder?.CanCancel == true;

    public bool IsReviewSheetVisible
    {
        get => _isReviewSheetVisible;
        set => SetField(ref _isReviewSheetVisible, value);
    }

    public MockOrder? ReviewingOrder
    {
        get => _reviewingOrder;
        private set
        {
            if (SetField(ref _reviewingOrder, value))
                OnPropertyChanged(nameof(ReviewOrderTitle));
        }
    }

    public string ReviewOrderTitle => ReviewingOrder is not null ? $"Order {ReviewingOrder.Id.TrimStart('#')}" : "Write Review";

    public ReviewableItemModel? SelectedReviewItem
    {
        get => _selectedReviewItem;
        set
        {
            if (SetField(ref _selectedReviewItem, value))
            {
                OnPropertyChanged(nameof(HasSelectedReviewItem));
                OnPropertyChanged(nameof(SelectedReviewItemName));
            }
        }
    }

    public bool HasSelectedReviewItem => SelectedReviewItem is not null;
    public string SelectedReviewItemName => SelectedReviewItem?.Name ?? "Selected Product";
    public bool HasMultipleReviewItems => ReviewItems.Count > 1;

    public int ReviewRating
    {
        get => _reviewRating;
        set
        {
            if (SetField(ref _reviewRating, Math.Clamp(value, 1, 5)))
            {
                OnPropertyChanged(nameof(RatingStarsLabel));
                OnPropertyChanged(nameof(Star1Color));
                OnPropertyChanged(nameof(Star2Color));
                OnPropertyChanged(nameof(Star3Color));
                OnPropertyChanged(nameof(Star4Color));
                OnPropertyChanged(nameof(Star5Color));
            }
        }
    }

    public string RatingStarsLabel => ReviewRating switch
    {
        5 => "5 Stars - Excellent!",
        4 => "4 Stars - Good",
        3 => "3 Stars - Average",
        2 => "2 Stars - Poor",
        1 => "1 Star - Terrible",
        _ => $"{ReviewRating} Stars"
    };

    public Color Star1Color => ReviewRating >= 1 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#CBD5E1");
    public Color Star2Color => ReviewRating >= 2 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#CBD5E1");
    public Color Star3Color => ReviewRating >= 3 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#CBD5E1");
    public Color Star4Color => ReviewRating >= 4 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#CBD5E1");
    public Color Star5Color => ReviewRating >= 5 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#CBD5E1");

    public string ReviewTitle
    {
        get => _reviewTitle;
        set => SetField(ref _reviewTitle, value);
    }

    public string ReviewComment
    {
        get => _reviewComment;
        set => SetField(ref _reviewComment, value);
    }

    public string ReviewError
    {
        get => _reviewError;
        set
        {
            if (SetField(ref _reviewError, value))
                OnPropertyChanged(nameof(HasReviewError));
        }
    }

    public bool HasReviewError => !string.IsNullOrWhiteSpace(ReviewError);

    public bool IsSubmittingReview
    {
        get => _isSubmittingReview;
        private set
        {
            if (SetField(ref _isSubmittingReview, value))
                ((Command)SubmitReviewCommand).ChangeCanExecute();
        }
    }

    public bool HasOrders
    {
        get => _hasOrders;
        private set
        {
            if (SetField(ref _hasOrders, value))
                OnPropertyChanged(nameof(HasNoOrders));
        }
    }

    public bool HasNoOrders => !HasOrders;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public ICommand SelectStatusCommand { get; }
    public ICommand StartShoppingCommand { get; }
    public ICommand ViewOrderCommand { get; }
    public ICommand CancelOrderCommand { get; }
    public ICommand DetailsCommand { get; }
    public ICommand TrackCommand { get; }
    public ICommand BuyAgainCommand { get; }
    public ICommand ReviewCommand { get; }
    public ICommand ConfirmReceivedCommand { get; }
    public ICommand CloseOrderDetailsCommand { get; }
    public ICommand WriteItemReviewCommand { get; }
    public ICommand CancelOrderFromDetailsCommand { get; }
    public ICommand SelectReviewItemCommand { get; }
    public ICommand SetRatingCommand { get; }
    public ICommand SubmitReviewCommand { get; }
    public ICommand CloseReviewSheetCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            // Force refresh so returning to the page picks up new orders.
            await ForceReloadAsync();
            ApplyFilter();
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

    private async Task ForceReloadAsync()
    {
        await _orders.EnsureLoadedAsync(_auth.Email);

        if (_orders.Orders.Count == 0 && string.IsNullOrWhiteSpace(_auth.Email))
            await _orders.EnsureLoadedAsync(null);
    }

    private void BuildStatusChips()
    {
        var statuses = OrderFlow.CustomerTabs;

        StatusChips.Clear();
        foreach (var status in statuses)
        {
            StatusChips.Add(new OrderStatusChip
            {
                Key = status,
                Label = status,
                IsSelected = string.Equals(status, _selectedStatus, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private void OnSelectStatus(OrderStatusChip? chip)
    {
        if (chip is null) return;
        _selectedStatus = chip.Key;
        foreach (var c in StatusChips)
            c.IsSelected = string.Equals(c.Key, chip.Key, StringComparison.OrdinalIgnoreCase);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _orders.Filter(_selectedStatus)
            .Select(o => new OrderCardModel { Order = o })
            .ToList();

        FilteredOrders.Clear();
        foreach (var card in filtered)
            FilteredOrders.Add(card);

        HasOrders = FilteredOrders.Count > 0;
    }

    public void PopulateOrderDetails(MockOrder order, OrderCardModel? card)
    {
        SelectedOrder = order;
        _selectedOrderCard = card;

        var steps = OrderFlow.DetailTimelineSteps(order.Fulfillment);
        var progress = OrderFlow.TimelineProgress(order.Fulfillment, order.Status);
        TimelineSteps.Clear();

        for (int i = 0; i < steps.Length; i++)
        {
            var isDone = progress >= 0 && i < progress;
            var isCurrent = progress >= 0 && i == progress;
            var isCancelled = progress < 0;

            TimelineSteps.Add(new OrderDetailTimelineStep
            {
                Index = i,
                Label = steps[i],
                IsDone = isDone,
                IsCurrent = isCurrent,
                IsPending = !isDone && !isCurrent && !isCancelled,
                IsCancelled = isCancelled,
                ShowConnectingLine = i < steps.Length - 1
            });
        }

        DetailItems.Clear();
        foreach (var item in order.Items)
        {
            DetailItems.Add(new OrderDetailItemModel
            {
                Item = item,
                OrderId = order.Id,
                CanWriteReview = order.CanWriteReview,
                IsReviewed = item.IsReviewed
            });
        }

        var panel = OrderFlow.StatusPanel(order);
        DetailsStatusTitle = panel.Title;
        DetailsStatusMessage = panel.Message;
        DetailsStatusIcon = progress < 0 ? Helpers.MaterialIconCodes.Close
            : order.IsDelivery ? Helpers.MaterialIconCodes.LocalShipping
            : Helpers.MaterialIconCodes.LocationOn;

        var itemSubtotal = order.Items.Count > 0
            ? order.Items.Sum(i => i.Price * i.Quantity)
            : order.Subtotal;
        DetailsSubtotalText = $"₱{itemSubtotal:N0}";

        OnPropertyChanged(nameof(DetailsDisplayId));
        OnPropertyChanged(nameof(DetailsDateAndItemsText));
        OnPropertyChanged(nameof(DetailsCustomerCategory));
        OnPropertyChanged(nameof(DetailsBadgeBackground));
        OnPropertyChanged(nameof(DetailsBadgeTextColor));
        OnPropertyChanged(nameof(DetailsStatusTitle));
        OnPropertyChanged(nameof(DetailsStatusMessage));
        OnPropertyChanged(nameof(DetailsStatusIcon));
        OnPropertyChanged(nameof(DetailsIsDelivery));
        OnPropertyChanged(nameof(DetailsRecipientName));
        OnPropertyChanged(nameof(DetailsRecipientPhone));
        OnPropertyChanged(nameof(DetailsShippingAddress));
        OnPropertyChanged(nameof(DetailsPickupLocation));
        OnPropertyChanged(nameof(DetailsPickupHours));
        OnPropertyChanged(nameof(DetailsPaymentMethod));
        OnPropertyChanged(nameof(DetailsPaymentStatus));
        OnPropertyChanged(nameof(DetailsPaymentHeadline));
        OnPropertyChanged(nameof(DetailsPaymentDetail));
        OnPropertyChanged(nameof(DetailsSubtotalText));
        OnPropertyChanged(nameof(DetailsHasDiscount));
        OnPropertyChanged(nameof(DetailsDiscountText));
        OnPropertyChanged(nameof(DetailsFeeLabel));
        OnPropertyChanged(nameof(DetailsFeeText));
        OnPropertyChanged(nameof(DetailsTotalText));
        OnPropertyChanged(nameof(DetailsCanConfirmReceived));
        OnPropertyChanged(nameof(DetailsCanCancel));
    }

    private void OnViewOrder(OrderCardModel? card)
    {
        if (card is null) return;
        PopulateOrderDetails(card.Order, card);
        IsOrderDetailsVisible = true;
    }

    private void OnTrack(OrderCardModel? card)
    {
        if (card is null) return;
        PopulateOrderDetails(card.Order, card);
        IsOrderDetailsVisible = true;
    }

    private void OnCloseOrderDetails()
    {
        IsOrderDetailsVisible = false;
    }

    private async Task OnConfirmReceivedAsync(OrderCardModel? card = null)
    {
        var order = card?.Order ?? SelectedOrder;
        if (order is null) return;
        var displayId = order.Id.TrimStart('#');

        var page = HostPage ?? Shell.Current;
        bool confirm = await page.DisplayAlertAsync(
            "Confirm Order Received",
            "Are you sure you have already received this order?\n\nOnce confirmed, this order will be moved to Completed.",
            "Confirm Received",
            "Cancel");

        if (!confirm) return;

        try
        {
            var updated = await _orders.ConfirmReceivedAsync(order.Id);
            _toast.Show("Order marked as received.");

            await _notifications.AddAsync(new MockNotification
            {
                Title = "Order Completed",
                Message = $"Your order {displayId} has been confirmed as received and completed.",
                TimeAgo = OrderFlow.FormatNotificationTime(DateTime.Now),
                Icon = "check-circle",
                Tone = "green",
                IsRead = false,
                RelatedId = order.Id,
                RelatedHref = "/orders"
            });

            if (_selectedStatus == OrderFlow.ToReceive)
            {
                var completedChip = StatusChips.FirstOrDefault(c => c.Key == OrderFlow.Completed);
                if (completedChip != null)
                {
                    OnSelectStatus(completedChip);
                }
                else
                {
                    ApplyFilter();
                }
            }
            else
            {
                ApplyFilter();
            }

            if (IsOrderDetailsVisible && SelectedOrder?.Id == order.Id)
            {
                var updatedCard = FilteredOrders.FirstOrDefault(c => c.Order.Id == order.Id)
                    ?? new OrderCardModel { Order = updated };
                PopulateOrderDetails(updated, updatedCard);
            }
        }
        catch (Exception ex)
        {
            _toast.Show(string.IsNullOrWhiteSpace(ex.Message) ? "Unable to confirm this order." : ex.Message);
        }
    }

    private async Task OnCancelOrderAsync(OrderCardModel? card)
    {
        var order = card?.Order ?? SelectedOrder;
        if (order is null) return;
        if (!order.CanCancel)
        {
            _toast.Show("This order can no longer be cancelled.");
            return;
        }

        var displayId = order.Id.TrimStart('#');

        var page = HostPage ?? Shell.Current;
        bool confirm = await page.DisplayAlertAsync(
            "Cancel Order",
            $"Are you sure you want to cancel order {displayId}?",
            "Yes, Cancel",
            "No");

        if (!confirm) return;

        try
        {
            IsBusy = true;

            await Task.Run(async () =>
            {
                var cleanId = order.Id.Trim().TrimStart('#');

                // 1. Fetch AdminOrder from Supabase database
                AdminOrder? admin = await _db.GetOrderByIdAsync(order.Id);
                if (admin is null && cleanId != order.Id)
                {
                    admin = await _db.GetOrderByIdAsync(cleanId);
                }
                if (admin is null && !order.Id.StartsWith("#", StringComparison.Ordinal))
                {
                    admin = await _db.GetOrderByIdAsync("#" + order.Id);
                }

                if (admin is not null)
                {
                    admin.Status = "Cancelled";
                    // UpsertOrderAsync delegates to the secure cancel_order RPC
                    await _db.UpsertOrderAsync(admin);

                    // Restore in-memory catalog stock counts
                    if (admin.Items != null && admin.Items.Count > 0)
                    {
                        _catalog.ApplyCancellation(admin.Items);
                    }
                }
                else if (order.Items != null && order.Items.Count > 0)
                {
                    _catalog.ApplyCancellation(order.Items.Select(i => new AdminOrderItem
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        Size = i.Size
                    }));
                }

                // 2. Ensure in-memory MockOrder reflects Cancelled status
                var memOrder = _orders.Orders.FirstOrDefault(o =>
                    string.Equals(o.Id, order.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.Id.TrimStart('#'), cleanId, StringComparison.OrdinalIgnoreCase));

                if (memOrder is not null)
                {
                    memOrder.Status = "Cancelled";
                }
                order.Status = "Cancelled";

                // 3. Reload from server to synchronize orders if logged in
                if (_auth.IsLoggedIn && !string.IsNullOrWhiteSpace(_auth.Email))
                {
                    try
                    {
                        await _orders.ReloadAsync(_auth.Email);
                    }
                    catch (Exception reloadEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to reload orders after cancellation: {reloadEx}");
                    }
                }
            });

            // 4. Send cancellation notification
            try
            {
                await _notifications.AddAsync(new MockNotification
                {
                    Title = "Order Cancelled",
                    Message = $"Order {displayId} has been cancelled.",
                    TimeAgo = OrderFlow.FormatNotificationTime(DateTime.Now),
                    Icon = "x-circle",
                    Tone = "red",
                    IsRead = false,
                    RelatedId = order.Id,
                    RelatedHref = "/orders"
                });
            }
            catch (Exception notifEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add cancellation notification: {notifEx}");
            }

            _toast.Show($"Order {displayId} cancelled.");

            // 5. Update UI
            card?.RefreshState();
            ApplyFilter();

            // Refresh details bottom sheet if it's currently showing this order
            if (IsOrderDetailsVisible && (SelectedOrder?.Id == order.Id || SelectedOrder?.Id.TrimStart('#') == displayId))
            {
                var updated = _orders.Orders.FirstOrDefault(o =>
                    string.Equals(o.Id, order.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.Id.TrimStart('#'), displayId, StringComparison.OrdinalIgnoreCase)) ?? order;
                var updatedCard = FilteredOrders.FirstOrDefault(c =>
                    string.Equals(c.Order.Id, order.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Order.Id.TrimStart('#'), displayId, StringComparison.OrdinalIgnoreCase))
                    ?? card
                    ?? new OrderCardModel { Order = updated };

                PopulateOrderDetails(updated, updatedCard);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cancelling order: {ex}");
            _toast.Show(string.IsNullOrWhiteSpace(ex.Message) ? "Unable to cancel this order." : ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnBuyAgainAsync(OrderCardModel? card)
    {
        var order = card?.Order ?? SelectedOrder;
        if (order is null) return;
        await _catalog.EnsureLoadedAsync();
        var added = 0;
        foreach (var item in order.Items)
        {
            var product = _catalog.GetById(item.ProductId)
                          ?? _catalog.Products.FirstOrDefault(p =>
                              p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            if (product is null) continue;
            _cart.Add(product, Math.Max(1, item.Quantity), null, item.Size);
            added++;
        }

        if (added > 0)
        {
            await _cart.PersistAsync(_auth.Email);
            _toast.Show($"Added {added} item(s) to cart.");
        }
        else
        {
            _toast.Show("Unable to add items to cart.");
        }
    }

    private void OnSelectReviewItem(ReviewableItemModel? item)
    {
        if (item is null) return;
        foreach (var ri in ReviewItems)
            ri.IsSelected = (ri == item);
        SelectedReviewItem = item;
        ReviewError = string.Empty;
    }

    private void SetRating(int rating)
    {
        ReviewRating = rating;
    }

    private void OnCloseReviewSheet()
    {
        IsReviewSheetVisible = false;
        ReviewError = string.Empty;
    }

    private void OnWriteItemReview(OrderDetailItemModel? detailItem)
    {
        if (detailItem is null || SelectedOrder is null) return;
        var card = _selectedOrderCard ?? FilteredOrders.FirstOrDefault(c => c.Order.Id == SelectedOrder.Id)
            ?? new OrderCardModel { Order = SelectedOrder };
        OnReview(card, detailItem.OrderItemId, detailItem.ProductId);
    }

    private void OnReview(OrderCardModel? card, long preselectedOrderItemId = 0, int preselectedProductId = 0)
    {
        if (card is null) return;
        _reviewingCard = card;
        ReviewingOrder = card.Order;

        ReviewItems.Clear();

        foreach (var item in card.Order.Items)
        {
            ReviewItems.Add(new ReviewableItemModel
            {
                Item = item,
                OrderId = card.Order.Id,
                IsSelected = false,
                IsReviewed = item.IsReviewed
            });
        }

        ReviewableItemModel? defaultItem = null;
        if (preselectedOrderItemId > 0)
        {
            defaultItem = ReviewItems.FirstOrDefault(i => i.OrderItemId == preselectedOrderItemId);
        }

        if (defaultItem == null && preselectedProductId > 0)
        {
            defaultItem = ReviewItems.FirstOrDefault(i => i.ProductId == preselectedProductId && !i.IsReviewed)
                       ?? ReviewItems.FirstOrDefault(i => i.ProductId == preselectedProductId);
        }

        defaultItem ??= ReviewItems.FirstOrDefault(i => !i.IsReviewed) ?? ReviewItems.FirstOrDefault();
        if (defaultItem != null)
        {
            OnSelectReviewItem(defaultItem);
        }

        ReviewRating = 5;
        ReviewTitle = string.Empty;
        ReviewComment = string.Empty;
        ReviewError = string.Empty;
        IsReviewSheetVisible = true;
    }

    private async Task OnSubmitReviewAsync()
    {
        if (SelectedReviewItem is null || ReviewingOrder is null) return;

        if (SelectedReviewItem.IsReviewed)
        {
            ReviewError = "This item has already been reviewed.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReviewComment) || ReviewComment.Trim().Length < 5)
        {
            ReviewError = "Please write a review with at least 5 characters.";
            return;
        }

        IsSubmittingReview = true;
        ReviewError = string.Empty;

        try
        {
            await _orders.SubmitProductReviewAsync(
                ReviewingOrder.Id,
                SelectedReviewItem.OrderItemId,
                ReviewRating,
                string.IsNullOrWhiteSpace(ReviewTitle) ? null : ReviewTitle.Trim(),
                ReviewComment.Trim());

            // Mark ONLY this specific order item as reviewed
            SelectedReviewItem.IsReviewed = true;
            SelectedReviewItem.Item.IsReviewed = true;

            if (ReviewingOrder != null)
            {
                var match = ReviewingOrder.Items.FirstOrDefault(i => i.OrderItemId == SelectedReviewItem.OrderItemId);
                if (match != null)
                {
                    match.IsReviewed = true;
                }
            }

            // Update DetailItems if open
            var detailMatch = DetailItems.FirstOrDefault(d => d.OrderItemId == SelectedReviewItem.OrderItemId);
            if (detailMatch != null)
            {
                detailMatch.IsReviewed = true;
            }

            _reviewingCard?.RefreshReviewState();
            _toast.Show($"Review submitted for {SelectedReviewItem.Name}!");
            await _notifications.AddAsync(new MockNotification
            {
                Title = "Review Submitted",
                Message = $"Thanks for reviewing {SelectedReviewItem.Name}! Your review helps other Bulldogs.",
                TimeAgo = OrderFlow.FormatNotificationTime(DateTime.Now),
                Icon = "star",
                Tone = "gold",
                IsRead = false,
                RelatedId = ReviewingOrder?.Id ?? string.Empty,
                RelatedHref = "/orders"
            });

            // Check if there are other DIFFERENT unreviewed items in this order
            var nextUnreviewed = ReviewItems.FirstOrDefault(i => !i.IsReviewed);
            if (nextUnreviewed != null)
            {
                OnSelectReviewItem(nextUnreviewed);
                ReviewRating = 5;
                ReviewTitle = string.Empty;
                ReviewComment = string.Empty;
            }
            else
            {
                IsReviewSheetVisible = false;
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            ReviewError = ex.Message ?? "Failed to submit review. Please try again.";
        }
        finally
        {
            IsSubmittingReview = false;
        }
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
