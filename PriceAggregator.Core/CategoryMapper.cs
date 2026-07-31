using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class CategoryMapper
{
    private readonly string _connectionString;
    private const double AutoApproveThreshold = 0.85;
    private const double ReviewQueueThreshold = 0.40;

    public CategoryMapper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int?> ResolveAsync(string searchTerm, string? rawSourceCategory)
    {
        using var db = new SqlConnection(_connectionString);

        if (string.IsNullOrWhiteSpace(rawSourceCategory))
        {
            return await db.QuerySingleOrDefaultAsync<int?>(
                "SELECT CategoryId FROM CategoryKeywordMap WHERE Keyword = @Keyword",
                new { Keyword = searchTerm.ToLowerInvariant() });
        }

        var normalized = rawSourceCategory.Trim().ToLowerInvariant();

        var cached = await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT CategoryId FROM CategoryRawMap WHERE RawCategoryText = @Raw",
            new { Raw = normalized });
        if (cached.HasValue) return cached;

        // Already reviewed and rejected — stop asking, stay uncategorized permanently
        var alreadyRejected = await db.QuerySingleOrDefaultAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM CategoryReviewQueue WHERE RawCategoryText = @Raw AND Status = 'Rejected') THEN 1 ELSE 0 END AS BIT)",
            new { Raw = normalized });
        if (alreadyRejected) return null;

        var categories = await db.QueryAsync<(int Id, string Name)>("SELECT Id, Name FROM Categories");

        int? bestId = null;
        double bestScore = 0.0;
        var rawTokens = TextSimilarity.Tokenize(normalized);

        foreach (var cat in categories)
        {
            var catTokens = TextSimilarity.Tokenize(cat.Name);
            var score = TextSimilarity.Score(rawTokens, catTokens, emptySetMeansCompatible: false);
            if (score > bestScore)
            {
                bestScore = score;
                bestId = cat.Id;
            }
        }

        if (bestId.HasValue && bestScore >= AutoApproveThreshold)
        {
            await db.ExecuteAsync(
                "INSERT INTO CategoryRawMap (RawCategoryText, CategoryId) VALUES (@Raw, @CatId)",
                new { Raw = normalized, CatId = bestId });
            return bestId;
        }

        if (bestId.HasValue && bestScore >= ReviewQueueThreshold)
        {
            await db.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM CategoryReviewQueue WHERE RawCategoryText = @Raw)
            INSERT INTO CategoryReviewQueue (RawCategoryText, SuggestedCategoryId, Score)
            VALUES (@Raw, @CatId, @Score)",
                new { Raw = normalized, CatId = bestId, Score = bestScore });
        }

        return null;
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