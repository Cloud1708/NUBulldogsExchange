namespace NUBulldogsExchange.Web.Shared.Data;

/// <summary>
/// Shared mapping between operational order fields and customer-facing labels.
/// Does not rename stored Admin statuses.
/// </summary>
public static class OrderFlow
{
    public const string CampusPickup = "Campus Pickup";
    public const string Delivery = "Delivery";
    public const string PickupLocation = "NU Lipa Campus";
    public const string PickupHoursDays = "Mon–Sat";
    public const string PickupHoursTime = "8AM–5PM";

    public const string ToPay = "To Pay";
    public const string ToProcess = "To Process";
    public const string ReadyForPickup = "Ready for Pickup";
    public const string ToReceive = "To Receive";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] CustomerTabs =
    [
        "All",
        ToPay,
        ToProcess,
        ReadyForPickup,
        ToReceive,
        Completed,
        Cancelled
    ];

    public static readonly string[] PickupAdminStatuses =
    [
        "Pending",
        "Processing",
        ReadyForPickup,
        Completed,
        Cancelled
    ];

    public static readonly string[] DeliveryAdminStatuses =
    [
        "Pending",
        "Processing",
        "Out for Delivery",
        "Delivered",
        Cancelled
    ];

    public static readonly string[] AdminStatusFilters =
    [
        "All Statuses",
        "Pending",
        "Processing",
        ReadyForPickup,
        "Out for Delivery",
        "Delivered",
        Completed,
        Cancelled
    ];

    public static readonly string[] PaymentStatusOptions =
    [
        "Pending",
        "Paid",
        "Failed",
        "Cancelled",
        "Refunded"
    ];

    public static readonly string[] PaymentMethodFilters =
    [
        "All Methods",
        "Cash on Pickup",
        "Cash on Delivery",
        "GCash",
        "Maya",
        "Credit Card"
    ];

    public static bool IsCampusPickup(string? fulfillment) =>
        string.IsNullOrWhiteSpace(fulfillment) ||
        fulfillment.Equals(CampusPickup, StringComparison.OrdinalIgnoreCase);

    public static bool IsDelivery(string? fulfillment) =>
        fulfillment?.Equals(Delivery, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsOnlinePaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return false;

        return method.Equals("GCash", StringComparison.OrdinalIgnoreCase)
            || method.Equals("Maya", StringComparison.OrdinalIgnoreCase)
            || method.Equals("Credit Card", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPaymentPending(string? paymentStatus) =>
        string.Equals(paymentStatus, "Pending", StringComparison.OrdinalIgnoreCase);

    public static string CheckoutPaymentStatus(string? paymentMethod, bool markOnlinePaid) =>
        markOnlinePaid && IsOnlinePaymentMethod(paymentMethod) ? "Paid" : "Pending";

    public static bool PaymentSnapshotMatches(AdminOrder? order, string? paymentMethod, string? paymentStatus) =>
        order is not null
        && !string.IsNullOrWhiteSpace(order.PaymentMethod)
        && order.PaymentMethod.Equals(paymentMethod?.Trim(), StringComparison.OrdinalIgnoreCase)
        && order.PaymentStatus.Equals(paymentStatus?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string GetCustomerOrderCategory(
        string? fulfillment,
        string? status,
        string? paymentStatus,
        string? paymentMethod)
    {
        if (EqualsStatus(status, Cancelled))
            return Cancelled;

        if (IsOnlinePaymentMethod(paymentMethod) && IsPaymentPending(paymentStatus))
            return ToPay;

        if (IsDelivery(fulfillment))
        {
            if (EqualsStatus(status, "Out for Delivery") || EqualsStatus(status, "Shipped"))
                return ToReceive;

            if (EqualsStatus(status, "Delivered") || EqualsStatus(status, Completed))
                return Completed;

            return ToProcess;
        }

        if (EqualsStatus(status, ReadyForPickup))
            return ReadyForPickup;

        if (EqualsStatus(status, Completed))
            return Completed;

        return ToProcess;
    }

    public static string GetCustomerOrderCategory(AdminOrder order) =>
        GetCustomerOrderCategory(order.Fulfillment, order.Status, order.PaymentStatus, order.PaymentMethod);

    public static string GetCustomerOrderCategory(MockOrder order) =>
        GetCustomerOrderCategory(order.Fulfillment, order.Status, order.PaymentStatus, order.PaymentMethod);

    public static bool CanCustomerCancel(string? fulfillment, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        if (EqualsStatus(status, Cancelled)
            || EqualsStatus(status, Completed)
            || EqualsStatus(status, "Delivered")
            || EqualsStatus(status, ReadyForPickup)
            || EqualsStatus(status, "Out for Delivery")
            || EqualsStatus(status, "Shipped"))
            return false;

        return EqualsStatus(status, "Pending")
            || EqualsStatus(status, "Processing")
            || EqualsStatus(status, "Confirmed")
            || EqualsStatus(status, "Preparing");
    }

    public static bool CanCustomerCancel(MockOrder order) =>
        CanCustomerCancel(order.Fulfillment, order.Status);

    public static bool CanCustomerCancel(AdminOrder order) =>
        CanCustomerCancel(order.Fulfillment, order.Status);

    public static string[] AllowedAdminStatuses(string? fulfillment) =>
        IsDelivery(fulfillment) ? DeliveryAdminStatuses : PickupAdminStatuses;

    public static IEnumerable<string> AdminStatusOptions(string? fulfillment, string? currentStatus)
    {
        var allowed = AllowedAdminStatuses(fulfillment).ToList();
        if (!string.IsNullOrWhiteSpace(currentStatus)
            && !allowed.Any(s => s.Equals(currentStatus, StringComparison.OrdinalIgnoreCase)))
        {
            allowed.Insert(0, currentStatus);
        }

        return allowed;
    }

    public static string CustomerStatusKey(string? category) => category switch
    {
        ToPay => "to-pay",
        ToProcess => "to-process",
        ReadyForPickup => "ready",
        ToReceive => "to-receive",
        Completed => "completed",
        Cancelled => "cancelled",
        _ => "pending"
    };

    public static string OperationalStatusKey(string? status) => status switch
    {
        ReadyForPickup => "ready",
        "Out for Delivery" or "Shipped" => "out-for-delivery",
        "Delivered" => "delivered",
        "Processing" => "processing",
        "Pending" => "pending",
        Completed => "completed",
        Cancelled => "cancelled",
        "Confirmed" => "confirmed",
        "Preparing" => "processing",
        _ => "pending"
    };

    public static string[] TimelineSteps(string? fulfillment) =>
        DetailTimelineSteps(fulfillment);

    public static string[] DetailTimelineSteps(string? fulfillment) =>
        IsDelivery(fulfillment)
            ? ["Pending", "Confirmed", "Processing", "Out for Delivery", "Delivered"]
            : ["Pending", "Confirmed", "Processing", "Ready for Pickup", "Completed"];

    /// <summary>
    /// 0 = Pending, 1 = Confirmed (visual), 2 = Processing,
    /// 3 = Ready/Out, 4 = Completed/Delivered, -1 = cancelled.
    /// Confirmed is a UI step only when the stored status is still Pending/Confirmed.
    /// </summary>
    public static int TimelineProgress(string? fulfillment, string? status)
    {
        if (EqualsStatus(status, Cancelled))
            return -1;

        if (IsDelivery(fulfillment))
        {
            if (EqualsStatus(status, "Delivered") || EqualsStatus(status, Completed))
                return 4;
            if (EqualsStatus(status, "Out for Delivery") || EqualsStatus(status, "Shipped"))
                return 3;
            if (EqualsStatus(status, "Processing") || EqualsStatus(status, "Preparing"))
                return 2;
            if (EqualsStatus(status, "Confirmed"))
                return 1;
            return 0;
        }

        if (EqualsStatus(status, Completed))
            return 4;
        if (EqualsStatus(status, ReadyForPickup))
            return 3;
        if (EqualsStatus(status, "Processing") || EqualsStatus(status, "Preparing"))
            return 2;
        if (EqualsStatus(status, "Confirmed"))
            return 1;
        return 0;
    }

    public static string PaymentHeadline(string? paymentStatus) =>
        string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? "Paid" : "Pending";

    public static string PaymentDetail(string? paymentMethod, string? paymentStatus)
    {
        if (string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            return "Payment received";

        if (paymentMethod?.Equals("Cash on Pickup", StringComparison.OrdinalIgnoreCase) == true)
            return "Pay upon pickup";

        if (paymentMethod?.Equals("Cash on Delivery", StringComparison.OrdinalIgnoreCase) == true)
            return "Pay upon delivery";

        if (IsOnlinePaymentMethod(paymentMethod))
            return "Complete your payment";

        return string.IsNullOrWhiteSpace(paymentMethod) ? "Payment pending" : paymentMethod;
    }

    public static (string Title, string Message) StatusPanel(MockOrder order)
    {
        if (EqualsStatus(order.Status, Cancelled))
            return ("Order Cancelled", $"Order {order.Id} has been cancelled.");

        if (IsDelivery(order.Fulfillment))
        {
            if (EqualsStatus(order.Status, "Out for Delivery") || EqualsStatus(order.Status, "Shipped"))
                return ("Out for Delivery", "Your order is already on the way and will be delivered soon.");

            if (EqualsStatus(order.Status, "Delivered") || EqualsStatus(order.Status, Completed))
                return ("Delivered", "Your order has been successfully delivered.");

            if (EqualsStatus(order.Status, "Processing")
                || EqualsStatus(order.Status, "Confirmed")
                || EqualsStatus(order.Status, "Preparing"))
                return ("Order is being prepared", "Your order is currently being prepared for delivery.");

            return ("Order Placed", "We've received your order and will begin preparing it for delivery.");
        }

        if (EqualsStatus(order.Status, ReadyForPickup))
            return (
                "Ready for Pickup!",
                $"Bring your order ID {order.Id} and a valid school ID to the NU Lipa pickup location to claim your order.");

        if (EqualsStatus(order.Status, Completed))
            return ("Order Completed", "This pickup order has already been claimed.");

        if (EqualsStatus(order.Status, "Processing")
            || EqualsStatus(order.Status, "Confirmed")
            || EqualsStatus(order.Status, "Preparing"))
            return ("Order is being prepared", "Your order is currently being prepared for pickup.");

        return ("Order Placed", "We've received your order and will notify you when it's ready for pickup.");
    }

    public static string? CustomerNotificationTitle(string? fulfillment, string newStatus)
    {
        if (EqualsStatus(newStatus, Cancelled))
            return "Order Cancelled";

        if (IsDelivery(fulfillment))
        {
            if (EqualsStatus(newStatus, "Out for Delivery") || EqualsStatus(newStatus, "Shipped"))
                return "Order Out for Delivery";
            if (EqualsStatus(newStatus, "Delivered"))
                return "Order Delivered";
            return null;
        }

        if (EqualsStatus(newStatus, ReadyForPickup))
            return "Order Ready for Pickup";
        if (EqualsStatus(newStatus, Completed))
            return "Order Completed";
        return null;
    }

    public static string? CustomerNotificationMessage(string orderId, string? fulfillment, string newStatus)
    {
        if (EqualsStatus(newStatus, Cancelled))
            return $"Your order {orderId} has been cancelled.";

        if (IsDelivery(fulfillment))
        {
            if (EqualsStatus(newStatus, "Out for Delivery") || EqualsStatus(newStatus, "Shipped"))
                return $"Your order {orderId} is now out for delivery.";
            if (EqualsStatus(newStatus, "Delivered"))
                return $"Your order {orderId} has been delivered.";
            return null;
        }

        if (EqualsStatus(newStatus, ReadyForPickup))
            return $"Your order {orderId} is ready for pickup at {PickupLocation}.";
        if (EqualsStatus(newStatus, Completed))
            return $"Your order {orderId} has been completed.";
        return null;
    }

    public static IEnumerable<string> PaymentStatusChoices(string? current)
    {
        var list = PaymentStatusOptions.ToList();
        if (!string.IsNullOrWhiteSpace(current)
            && !list.Any(s => s.Equals(current, StringComparison.OrdinalIgnoreCase)))
        {
            list.Insert(0, current);
        }

        return list;
    }

    public static string FormatShippingAddress(AdminOrder order) =>
        FormatShippingAddress(
            order.ShippingAddressLine,
            order.ShippingBarangay,
            order.ShippingCity,
            order.ShippingProvince,
            order.ShippingPostalCode);

    public static string FormatShippingAddress(MockOrder order) =>
        FormatShippingAddress(
            order.ShippingAddressLine,
            order.ShippingBarangay,
            order.ShippingCity,
            order.ShippingProvince,
            order.ShippingPostalCode);

    public static string FormatShippingShort(string? barangay, string? city, string? province)
    {
        var parts = new[] { barangay, city, province }.Where(v => !string.IsNullOrWhiteSpace(v));
        return string.Join(", ", parts);
    }

    private static string FormatShippingAddress(
        string? line,
        string? barangay,
        string? city,
        string? province,
        string? postal)
    {
        var cityLine = string.Join(" ", new[] { city, province, postal }.Where(v => !string.IsNullOrWhiteSpace(v)));
        var parts = new[] { line, barangay, cityLine }.Where(v => !string.IsNullOrWhiteSpace(v));
        return string.Join(", ", parts);
    }

    private static bool EqualsStatus(string? value, string expected) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Equals(expected, StringComparison.OrdinalIgnoreCase);
}
