using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class CategoryMapper
{
    private readonly string _connectionString;

    public CategoryMapper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int?> ResolveAsync(string searchTerm, string? rawSourceCategory)
    {
        using var db = new SqlConnection(_connectionString);

        if (!string.IsNullOrWhiteSpace(rawSourceCategory))
        {
            var byRaw = await db.QuerySingleOrDefaultAsync<int?>(
                "SELECT CategoryId FROM CategoryRawMap WHERE RawCategoryText = @Raw",
                new { Raw = rawSourceCategory.Trim().ToLowerInvariant() });

            if (byRaw.HasValue) return byRaw;

            // Real category info exists, we just don't recognize this specific string yet —
            // don't blindly fall back to the keyword here (that's how bleach/mouthwash
            // leaked into "su" results before).
            return null;
        }

        return await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT CategoryId FROM CategoryKeywordMap WHERE Keyword = @Keyword",
            new { Keyword = searchTerm.ToLowerInvariant() });
    }

    public async Task<bool> IsLikelyFalsePositiveAsync(string searchTerm, string? rawSourceCategory)
    {
        if (string.IsNullOrWhiteSpace(rawSourceCategory)) return false;

        using var db = new SqlConnection(_connectionString);

        var expectedCategoryId = await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT CategoryId FROM CategoryKeywordMap WHERE Keyword = @Keyword",
            new { Keyword = searchTerm.ToLowerInvariant() });

        if (!expectedCategoryId.HasValue) return false;

        var resolvedId = await ResolveAsync(searchTerm, rawSourceCategory);
        return resolvedId.HasValue && resolvedId != expectedCategoryId;
    }
}