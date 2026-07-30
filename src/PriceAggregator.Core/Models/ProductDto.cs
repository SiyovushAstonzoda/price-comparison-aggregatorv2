namespace PriceAggregator.Core.Models;

public class ProductDto
{
    public string ExternalId { get; set; } = "";
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal RegularPrice { get; set; }
    public string ProductUrl { get; set; } = "";
    public string Brand { get; set; }
    public string? Barcode { get; set; }
    public string? SourceCategory { get; set; }
}
