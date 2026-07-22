using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Scraper;

public class MatchingService
{
    private readonly string _connectionString;

    public MatchingService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task MatchProductAsync(int productId, string? brand, string title)
    {
        // Fallback: if brand is missing, use "Unknown" instead of null
        var safeBrand = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand;
        /*string safeBrand;
        if (string.IsNullOrWhiteSpace(brand))
            safeBrand = "Unknown";
        else
            safeBrand = brand;*/

        var (size, unit) = SizeParser.ExtractSize(title);

        using var db = new SqlConnection(_connectionString);

        var existingMasterId = await db.QuerySingleOrDefaultAsync<int?>(@"
            SELECT TOP 1 Id FROM MasterProducts
            WHERE Brand = @Brand AND SizeValue = @Size AND SizeUnit = @Unit",
            new { Brand = safeBrand, Size = size, Unit = unit });

        int masterId;
        if (existingMasterId.HasValue)
        {
            masterId = existingMasterId.Value;
        }
        else
        {
            masterId = await db.QuerySingleAsync<int>(@"
                INSERT INTO MasterProducts (CanonicalTitle, Brand, SizeValue, SizeUnit)
                OUTPUT INSERTED.Id
                VALUES (@Title, @Brand, @Size, @Unit)",
                new { Title = title, Brand = safeBrand, Size = size, Unit = unit });
        }

        await db.ExecuteAsync(
            "UPDATE Products SET MasterProductId = @MasterId WHERE Id = @ProductId",
            new { MasterId = masterId, ProductId = productId });
    }
}