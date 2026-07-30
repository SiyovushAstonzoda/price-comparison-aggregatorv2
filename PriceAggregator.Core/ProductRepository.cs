using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class ProductRepository
{
    private readonly string _connectionString;
    public ProductRepository(string connectionString) => _connectionString = connectionString;

    // Ham mağaza ürününü SellerProducts'a yazar; katalog eşleştirmesi MatchingService'tedir.
    public async Task<int?> SaveAsync(string source, ProductDto product)
    {
        try
        {
            using var db = new SqlConnection(_connectionString);
            var sellerId = await db.QuerySingleOrDefaultAsync<int?>(
                "SELECT TOP (1) Id FROM dbo.Sellers WHERE Name = @Name", new { Name = source });
            if (!sellerId.HasValue)
                sellerId = await db.QuerySingleAsync<int>(
                    "INSERT INTO dbo.Sellers (Name, WebSiteUrl) OUTPUT INSERTED.Id VALUES (@Name, @Url)",
                    new { Name = source, Url = SellerUrl(source) });

            return await db.QuerySingleAsync<int>(@"
                MERGE dbo.SellerProducts AS target
                USING (SELECT @SellerId AS SellerId, @Code AS SellerProductCode) AS source
                ON target.SellerId = source.SellerId AND target.SellerProductCode = source.SellerProductCode
                WHEN MATCHED THEN UPDATE SET ExternalTitle=@Title, ExternalUrl=@Url,
                    ExternalImageUrl=@ImageUrl, CurrentPrice=@Price, LastScrapedAt=GETDATE()
                WHEN NOT MATCHED THEN INSERT (SellerId, SellerProductCode, ExternalTitle, ExternalUrl, ExternalImageUrl, CurrentPrice)
                    VALUES (@SellerId, @Code, @Title, @Url, @ImageUrl, @Price)
                OUTPUT INSERTED.Id;",
                new { SellerId = sellerId.Value, Code = product.ExternalId.ToString(), Title = product.Title,
                    Url = product.ProductUrl, product.ImageUrl, Price = product.Price });
        }
        catch (SqlException ex)
        {
            Logger.Log($"[DB] Failed to save seller product '{product.Title}' ({source}): {ex.Message}");
            return null;
        }
    }

    private static string? SellerUrl(string source) => source.ToLowerInvariant() switch
    {
        "ozdilek" => "https://www.ozdilekteyim.com/",
        "migros" => "https://www.migros.com.tr/",
        "macrocenter" => "https://www.macrocenter.com.tr/",
                "ikea" => "https://www.ikea.com.tr/",
_ => null
    };
}
