namespace NUBulldogsExchange.Web.Shared.Data;

public class BreadcrumbItem
{
    public string Label { get; set; } = string.Empty;
    public string? Href { get; set; }

    public BreadcrumbItem()
    {
    }

    public BreadcrumbItem(string label, string? href = null)
    {
        Label = label;
        Href = href;
    }
}
