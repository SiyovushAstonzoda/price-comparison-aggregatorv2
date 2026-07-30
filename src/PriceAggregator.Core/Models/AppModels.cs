namespace PriceAggregator.Core.Models;

public record Brand(int Id, string Name, string? LogoUrl, DateTime CreatedAt);

public record Category(int Id, string Name, int? ParentCategoryId, string Slug, bool IsActive);

public record Product(int Id, int BrandId, int CategoryId, string Name, string? Sku, string? ImageUrl, DateTime CreatedAt);

public record Seller(int Id, string Name, string? WebSiteUrl, decimal Rating, bool IsActive);

public record SellerProduct(int Id, int SellerId, string SellerProductCode, string ExternalTitle, string? ExternalUrl, string? ExternalImageUrl, decimal CurrentPrice, DateTime LastScrapedAt);

public record ProductMatchingLog(int Id, int SellerProductId, int ProductId, string Status, string MatchedBy, DateTime CreatedAt, string? Notes);

public record Offer(int Id, int ProductId, int SellerId, int SellerProductId, decimal Price, decimal CargoPrice, bool IsActive, DateTime LastUpdatedAt, decimal TotalCost);

public record OffersPriceHistory(int Id, int OfferId, decimal Price, DateTime RecordedAt);

// DTOs for frontend responses
public record CategoryDto(int Id, string Name, string Slug, string? Icon, int ProductCount, List<CategoryDto>? Children = null);

public class ProductListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public decimal LowestPrice { get; set; }
    public int OfferCount { get; set; }
    public string? ImageUrl { get; set; }
    public string? ProductUrl { get; set; }
}

public record OfferDto(string SellerName, decimal Price, decimal CargoPrice, decimal TotalCost, string? ExternalUrl, string? ExternalImageUrl, decimal SellerRating);

public class DealRow
{
    public int MasterProductId { get; set; }
    public string CanonicalTitle { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }
    public int PackQuantity { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? ProductUrl { get; set; }
}
