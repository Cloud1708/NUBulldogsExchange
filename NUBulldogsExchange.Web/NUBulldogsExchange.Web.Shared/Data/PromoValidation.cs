namespace NUBulldogsExchange.Web.Shared.Data;

public class PromoCartItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? Category { get; set; }
}

public class PromoValidationRequest
{
    public string Code { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public int? UserId { get; set; }
    public List<PromoCartItem> Items { get; set; } = [];
}

public class PromoValidationResult
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? PromotionId { get; set; }
    public string? PromotionName { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal EligibleSubtotal { get; set; }
    public decimal Total { get; set; }
}

public class ActivePromotionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DiscountLabel { get; set; } = string.Empty;
    public decimal MinimumOrder { get; set; }
    public DateTime EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
}
