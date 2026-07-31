using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class ProductRepository
{
    private readonly string _connectionString;

    public ProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int?> SaveAsync(string source, ProductDto product)
    {
        try
        {
            using var db = new SqlConnection(_connectionString);

            var sellerId = await FindOrCreateSellerAsync(db, source);

            var id = await db.QuerySingleAsync<int>(@"
        MERGE SellerProducts AS target
        USING (SELECT @SellerId AS SellerId, @SellerProductCode AS SellerProductCode) AS src
        ON target.SellerId = src.SellerId AND target.SellerProductCode = src.SellerProductCode
        WHEN MATCHED THEN
            UPDATE SET ExternalTitle=@ExternalTitle, ExternalUrl=@ExternalUrl, ExternalImageUrl=@ExternalImageUrl,
                       CurrentPrice=@CurrentPrice, RegularPrice=@RegularPrice, UnitType=@UnitType, UnitAmount=@UnitAmount,
                       SourceUnitPrice=@SourceUnitPrice, LastScrapedAt=GETDATE()
        WHEN NOT MATCHED THEN
            INSERT (SellerId, SellerProductCode, ExternalTitle, ExternalUrl, ExternalImageUrl,
                    CurrentPrice, RegularPrice, UnitType, UnitAmount, SourceUnitPrice, LastScrapedAt)
            VALUES (@SellerId, @SellerProductCode, @ExternalTitle, @ExternalUrl, @ExternalImageUrl,
                    @CurrentPrice, @RegularPrice, @UnitType, @UnitAmount, @SourceUnitPrice, GETDATE())
        OUTPUT INSERTED.Id;",
                new
                {
                    SellerId = sellerId,
                    SellerProductCode = product.ExternalId.ToString(),
                    ExternalTitle = product.Title,
                    ExternalUrl = product.ProductUrl,
                    ExternalImageUrl = product.ImageUrl,
                    CurrentPrice = product.Price,
                    RegularPrice = product.RegularPrice > product.Price ? product.RegularPrice : (decimal?)null,
                    UnitType = product.SourceUnitType,
                    UnitAmount = product.SourceUnitAmount,
                    SourceUnitPrice = product.SourceUnitPrice
                });

            return id;
        }
        catch (SqlException ex)
        {
            Logger.Log($"[DB] Failed to save product '{product.Title}' ({source}): {ex.Message}");
            return null;
        }
    }

    private static async Task<int> FindOrCreateSellerAsync(SqlConnection db, string name)
    {
        var existing = await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Sellers WHERE Name = @Name", new { Name = name });
        if (existing.HasValue) return existing.Value;

        try
        {
            return await db.QuerySingleAsync<int>(
                "INSERT INTO Sellers (Name) OUTPUT INSERTED.Id VALUES (@Name)", new { Name = name });
        }
        catch (SqlException)
        {
            // Unique constraint hit — a concurrent scrape created this seller first.
            return await db.QuerySingleAsync<int>(
                "SELECT Id FROM Sellers WHERE Name = @Name", new { Name = name });
        }
    }
}
