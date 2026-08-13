namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminNotificationItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "order"; // order | stock | system
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedId { get; set; }
    public string? RelatedLabel { get; set; }
    public string? RelatedHref { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Read { get; set; }

    public string IconName => Type switch
    {
        "stock" => "alert-triangle",
        "system" => "bell",
        _ => "package"
    };

    public string Tone => Type switch
    {
        "stock" => "gold",
        "system" => "purple",
        _ => "blue"
    };

    public string TimeLabel
    {
        get
        {
            var now = DateTime.Now;
            var span = now - Timestamp;

            if (span.TotalMinutes < 60)
            {
                var mins = Math.Max(1, (int)span.TotalMinutes);
                return mins == 1 ? "1 minute ago" : $"{mins} minutes ago";
            }

            if (span.TotalHours < 24)
            {
                var hours = (int)span.TotalHours;
                return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
            }

            if (Timestamp.Date == now.Date.AddDays(-1))
                return $"Yesterday, {Timestamp:h:mm tt}";

            var days = (int)span.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }
    }
}
