namespace PriceAggregator.Core.Data;

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

    // Real category data from the source store's API, when available.
    // CategoryName is the leaf/most-specific category; Parent/Root are its ancestors.
    public string? CategoryName { get; set; }
    public string? ParentCategoryName { get; set; }
    public string? RootCategoryName { get; set; }

    // The store's own structured size/unit-price data, when its API provides it (Migros/
    // MacroCenter/Mion do — see MigrosScraper.ParseUnitInfo). Preferred over SizeParser's
    // title-regex guess when present; null for sources that don't expose it.
    public string? SourceUnitType { get; set; }
    public decimal? SourceUnitAmount { get; set; }
    public decimal? SourceUnitPrice { get; set; }

    // Source-provided product attributes for filtering (e.g. "Renk" from Evidea/İkea's own
    // structured color fields). Keyed by attribute name; null for sources with none.
    public Dictionary<string, string>? Attributes { get; set; }
}