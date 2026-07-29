namespace PriceAggregator.Core;

public class ProductDto
{
    public long ExternalId { get; set; }
    public string Title { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal RegularPrice { get; set; }
    public string ProductUrl { get; set; } = "";
    public string Brand { get; set; } = "";
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public string? MidCategory { get; set; }
    public string? SubCategory { get; set; }
    public string? Color { get; set; }
    public string? Dimensions { get; set; }
    /// <summary>IKEA functionName / subTitle (e.g. plastik sandalye).</summary>
    public string? ProductType { get; set; }
    /// <summary>Extracted material keyword when present in title/type (plastik, ahşap, …).</summary>
    public string? Material { get; set; }
}