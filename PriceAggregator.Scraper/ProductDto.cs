namespace PriceAggregator.Scraper;

public class ProductDto
{
    public long ExternalId { get; set; }
    public string Title { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal RegularPrice { get; set; }
    public string ProductUrl { get; set; } = "";
}