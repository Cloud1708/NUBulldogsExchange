namespace NUBulldogsExchange.Web.Shared.Data;

public class ProductPriceHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ProductId { get; set; }
    public long? VariantId { get; set; }
    public decimal PreviousPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal? PromoPrice { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = "Admin";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Display label — base product pricing is shared across variants.</summary>
    public string VariantLabel { get; set; } = "—";

    public string DateLabel => CreatedAt.ToLocalTime().ToString("MMM d, yyyy");
}
