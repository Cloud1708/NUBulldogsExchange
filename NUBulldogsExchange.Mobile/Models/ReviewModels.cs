using System.ComponentModel;
using System.Runtime.CompilerServices;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Models;

public sealed class ProductReviewItemModel
{
    public ProductReview Review { get; init; } = new();
    public string PublicAuthor => Review.PublicAuthor;
    public string Initials => string.IsNullOrWhiteSpace(Review.Initials)
        ? (PublicAuthor.Length > 0 ? PublicAuthor[..1].ToUpperInvariant() : "U")
        : Review.Initials;
    public int Rating => Review.Rating;
    public string RatingStars => new string('\ue838', Math.Clamp(Review.Rating, 1, 5)) + new string('\ue83a', Math.Max(0, 5 - Review.Rating));
    public string DateFormatted => Review.Date == default ? "Recent" : Review.Date.ToString("MMM d, yyyy");
    public string Title => Review.Title ?? string.Empty;
    public bool HasTitle => !string.IsNullOrWhiteSpace(Review.Title);
    public string Comment => Review.Comment;
    public bool IsVerifiedPurchase => Review.IsVerifiedPurchase;
}

public sealed class ReviewableItemModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isReviewed;

    public MockOrderItem Item { get; init; } = new();
    public List<MockOrderItem> SiblingItems { get; init; } = [];
    public string OrderId { get; init; } = string.Empty;
    public long OrderItemId => Item.OrderItemId;
    public int ProductId => Item.ProductId;
    public string Name => Item.Name;
    public string ImageUrl => Item.ImageUrl;
    public string VariantText
    {
        get
        {
            var parts = new List<string>();
            if (Item.HasDisplayColor) parts.Add($"Color: {Item.Color}");
            if (Item.HasDisplaySize) parts.Add($"Size: {Item.Size}");
            return string.Join(" • ", parts);
        }
    }

    public bool IsReviewed
    {
        get => _isReviewed || Item.IsReviewed;
        set
        {
            if (_isReviewed == value) return;
            _isReviewed = value;
            Item.IsReviewed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanBeReviewed));
            OnPropertyChanged(nameof(StatusBadgeText));
            OnPropertyChanged(nameof(StatusBadgeColor));
            OnPropertyChanged(nameof(StatusBadgeTextColor));
        }
    }

    public bool CanBeReviewed => !IsReviewed;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BorderColor));
            OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    public Color BorderColor => IsSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public Color BackgroundColor => IsSelected ? Color.FromArgb("#F0F4FF") : Colors.White;

    public string StatusBadgeText => IsReviewed ? "Reviewed" : "Write Review";
    public Color StatusBadgeColor => IsReviewed ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FEF3C7");
    public Color StatusBadgeTextColor => IsReviewed ? Color.FromArgb("#16A34A") : Color.FromArgb("#D97706");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
