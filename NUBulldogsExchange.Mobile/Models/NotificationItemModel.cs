using System.ComponentModel;
using System.Runtime.CompilerServices;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Models;

public sealed class NotificationItemModel : INotifyPropertyChanged
{
    private bool _isRead;

    public MockNotification Notification { get; init; } = new();

    public string Id => Notification.Id;
    public string Title => Notification.Title;
    public string Message => Notification.Message;
    public string TimeAgo => Notification.TimeAgo;
    public string? RelatedId => Notification.RelatedId;
    public string? RelatedHref => Notification.RelatedHref;

    public bool IsRead
    {
        get => _isRead || Notification.IsRead;
        set
        {
            if (_isRead == value && Notification.IsRead == value) return;
            _isRead = value;
            Notification.IsRead = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUnread));
            OnPropertyChanged(nameof(CardBackgroundColor));
            OnPropertyChanged(nameof(CardBorderColor));
        }
    }

    public bool IsUnread => !IsRead;

    public Color CardBackgroundColor => IsUnread ? Color.FromArgb("#F8FAFC") : Colors.White;
    public Color CardBorderColor => IsUnread ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#E2E8F0");

    public string Glyph => Notification.Icon?.ToLowerInvariant() switch
    {
        "check-circle" or "check" => Helpers.MaterialIconCodes.CheckCircle,
        "truck" or "delivery" => Helpers.MaterialIconCodes.LocalShipping,
        "box" or "package" => Helpers.MaterialIconCodes.Inventory,
        "star" => Helpers.MaterialIconCodes.Star,
        "tag" or "discount" or "percent" => Helpers.MaterialIconCodes.LocalOffer,
        "x-circle" or "x" or "cancel" => Helpers.MaterialIconCodes.Cancel,
        _ => Helpers.MaterialIconCodes.Notifications
    };

    public Color IconBackgroundColor => Notification.Tone?.ToLowerInvariant() switch
    {
        "green" => Color.FromArgb("#DCFCE7"),
        "blue" => Color.FromArgb("#DBEAFE"),
        "gold" or "yellow" => Color.FromArgb("#FEF3C7"),
        "red" => Color.FromArgb("#FEE2E2"),
        "purple" => Color.FromArgb("#F3E8FF"),
        _ => Color.FromArgb("#EEF2FF")
    };

    public Color IconTextColor => Notification.Tone?.ToLowerInvariant() switch
    {
        "green" => Color.FromArgb("#15803D"),
        "blue" => Color.FromArgb("#1D4ED8"),
        "gold" or "yellow" => Color.FromArgb("#B45309"),
        "red" => Color.FromArgb("#B91C1C"),
        "purple" => Color.FromArgb("#7E22CE"),
        _ => Color.FromArgb("#00205B")
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
