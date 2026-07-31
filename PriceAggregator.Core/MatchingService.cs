using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class MatchingService
{
    private readonly string _connectionString;
    private readonly CategoryMapper _categoryMapper;

    public MatchingService(string connectionString)
    {
        _connectionString = connectionString;
        _categoryMapper = new CategoryMapper(connectionString);
    }

    public async Task<bool> MatchProductAsync(int productId, string? brand, string title, string searchTerm, string? sourceCategory)
    {
        try
        {
            if (await _categoryMapper.IsLikelyFalsePositiveAsync(searchTerm, sourceCategory))
            {
                Logger.Log($"[Matching] Skipping likely false positive: '{title}' (category: {sourceCategory}) for search '{searchTerm}'");
                return false;
            }

            var safeBrand = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand;
            var (size, unit, packQty) = SizeParser.ExtractSize(title);
            var canonicalCategoryId = await _categoryMapper.ResolveAsync(searchTerm, sourceCategory);

            using var db = new SqlConnection(_connectionString);

            var candidates = await db.QueryAsync<(int Id, string CanonicalTitle)>(@"
                SELECT Id, CanonicalTitle FROM MasterProducts
                WHERE Brand = @Brand
                  AND (SizeValue = @Size OR (SizeValue IS NULL AND @Size IS NULL))
                  AND (SizeUnit = @Unit OR (SizeUnit IS NULL AND @Unit IS NULL))
                  AND PackQuantity = @PackQty",
                new { Brand = safeBrand, Size = size, Unit = unit, PackQty = packQty });

            int? bestMatchId = null;
            double bestScore = 0.0;
            double threshold = 0.70;

            foreach (var candidate in candidates)
            {
                double score = CalculateSimilarity(candidate.CanonicalTitle, title, safeBrand);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatchId = candidate.Id;
                }
            }

            int masterId;
            if (bestMatchId.HasValue && bestScore >= threshold)
            {
                masterId = bestMatchId.Value;
                if (canonicalCategoryId.HasValue)
                {
                    await db.ExecuteAsync(
                        "UPDATE MasterProducts SET CanonicalCategoryId = ISNULL(CanonicalCategoryId, @CatId) WHERE Id = @Id",
                        new { CatId = canonicalCategoryId, Id = masterId });
                }
            }
            else
            {
                masterId = await db.QuerySingleAsync<int>(@"
                    INSERT INTO MasterProducts (CanonicalTitle, Brand, SizeValue, SizeUnit, PackQuantity, CanonicalCategoryId)
                    OUTPUT INSERTED.Id
                    VALUES (@Title, @Brand, @Size, @Unit, @PackQty, @CategoryId)",
                    new { Title = title, Brand = safeBrand, Size = size, Unit = unit, PackQty = packQty, CategoryId = canonicalCategoryId });
            }

            await db.ExecuteAsync(
                "UPDATE Products SET MasterProductId = @MasterId WHERE Id = @ProductId",
                new { MasterId = masterId, ProductId = productId });

            return true;
        }
        catch (SqlException ex)
        {
            Logger.Log($"[Matching] DB error matching product {productId} ('{title}'): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Matching] Unexpected error matching product {productId} ('{title}'): {ex.Message}");
            return false;
        }
    }

    private static readonly HashSet<string> UnitWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ml", "lt", "l", "g", "gr", "kg", "adet", "cc", "pet"
    };

    private static readonly HashSet<string> DescriptorFillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "pet", "sise", "su", "dogal", "kaynak", "sade"
    };

    private static readonly Dictionary<string, string> ShapeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kalem"] = "penne rigate",
        // ["fiyonk"] = "farfalle",
        // ["burgu"] = "fusilli",
    };

    private static string ApplyShapeAliases(string text)
    {
        foreach (var (alias, canonical) in ShapeAliases)
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(alias)}\b", canonical, RegexOptions.IgnoreCase);
        }
        return text;
    }

    // Product-title-specific tokenization: strips brand name, units, and packaging
    // filler words, on top of TextSimilarity's generic normalization/splitting.
    private static HashSet<string> TokenizeProductTitle(string text, string? brand)
    {
        text = ApplyShapeAliases(text);

        var brandTokens = string.IsNullOrWhiteSpace(brand)
            ? Enumerable.Empty<string>()
            : TurkishNormalizer.Normalize(brand).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var exclude = UnitWords.Union(DescriptorFillerWords).Union(brandTokens);

        // length > 2 here (vs TextSimilarity's generic length > 1) — drops short
        // noise fragments that are common in product titles specifically.
        return TextSimilarity.Tokenize(text, exclude)
            .Where(w => w.Length > 2)
            .ToHashSet();
    }

    private static double CalculateSimilarity(string title1, string title2, string brand)
    {
        var tokens1 = TokenizeProductTitle(title1, brand);
        var tokens2 = TokenizeProductTitle(title2, brand);

        // emptySetMeansCompatible: true — handles cases like Hakmar's generic
        // "Erikli Su 5 Lt" (no distinguishing words left) matching Migros's more
        // descriptive "Erikli Su Pet Şişe 5 L".
        return TextSimilarity.Score(tokens1, tokens2, emptySetMeansCompatible: true);
    }
}