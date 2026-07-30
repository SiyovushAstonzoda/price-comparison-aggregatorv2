using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using System.Text.RegularExpressions;

using Dapper;

using Microsoft.Data.SqlClient;


namespace PriceAggregator.Application.Services;

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

    private string ApplyShapeAliases(string text)
    {
        foreach (var (alias, canonical) in ShapeAliases)
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(alias)}\b", canonical, RegexOptions.IgnoreCase);
        }
        return text;
    }

    private HashSet<string> Tokenize(string text, string? brand)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>();

        text = ApplyShapeAliases(text);
        var normalized = TurkishNormalizer.Normalize(text);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        var brandTokens = string.IsNullOrWhiteSpace(brand)
            ? new HashSet<string>()
            : TurkishNormalizer.Normalize(brand).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Where(w => !brandTokens.Contains(w))
            .Where(w => !UnitWords.Contains(w))
            .Where(w => !DescriptorFillerWords.Contains(w))
            .Where(w => !w.All(char.IsDigit))
            .ToHashSet();
    }

    // Two tokens are "the same word" if identical, or if one is the other's root
    // plus a short Turkish suffix (handles çay/çayı, tiryaki/tiryakiler, etc.)
    private static bool TokensMatch(string a, string b)
    {
        if (a == b) return true;

        // Turkish-suffix case: one word is a prefix of the other (çay / çayı)
        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;
        if (longer.StartsWith(shorter, StringComparison.Ordinal) && (longer.Length - shorter.Length) <= 3)
            return true;

        // Spelling-variant case: small edit distance relative to word length
        // (handles transliteration differences like "Spaghetti" vs "Spagetti")
        if (shorter.Length >= 5)
        {
            int maxAllowedDistance = shorter.Length <= 7 ? 1 : 2;
            if (LevenshteinDistance(a, b) <= maxAllowedDistance)
                return true;
        }

        return false;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }

    private double CalculateSimilarity(string title1, string title2, string brand)
    {
        var tokens1 = Tokenize(title1, brand);
        var tokens2 = Tokenize(title2, brand);

        if (tokens1.Count == 0 && tokens2.Count == 0) return 1.0;
        if (tokens1.Count == 0 || tokens2.Count == 0) return 1.0; // your existing empty-set rule (Erikli/pet-şişe case)

        var remaining = new HashSet<string>(tokens2);
        int matches = 0;

        foreach (var t1 in tokens1)
        {
            var match = remaining.FirstOrDefault(t2 => TokensMatch(t1, t2));
            if (match != null)
            {
                matches++;
                remaining.Remove(match);
            }
        }

        int union = tokens1.Count + tokens2.Count - matches;
        return (double)matches / union;
    }

    /*private double CalculateSimilarity(string title1, string title2, string brand)
    {
        var tokens1 = Tokenize(title1, brand);
        var tokens2 = Tokenize(title2, brand);

        if (tokens1.Count == 0 && tokens2.Count == 0) return 1.0;
        if (tokens1.Count == 0 || tokens2.Count == 0) return 0.0;

        // Full containment: every token of the smaller set has a fuzzy match in the larger set,
        // with nothing left conflicting. Handles "official name" vs "shorter alias" cases
        // (e.g. "Penne Rigate (Kalem)" vs "Kalem") without being fooled by variant codes
        // (e.g. "6-7" vs "4-0"), since containment requires ALL smaller-set tokens to match.
        bool oneWayContainment(HashSet<string> smaller, HashSet<string> larger) =>
            smaller.All(s => larger.Any(l => TokensMatch(s, l)));

        if (tokens1.Count <= tokens2.Count && oneWayContainment(tokens1, tokens2))
            return 1.0;
        if (tokens2.Count < tokens1.Count && oneWayContainment(tokens2, tokens1))
            return 1.0;

        var remaining = new HashSet<string>(tokens2);
        int matches = 0;

        foreach (var t1 in tokens1)
        {
            var match = remaining.FirstOrDefault(t2 => TokensMatch(t1, t2));
            if (match != null)
            {
                matches++;
                remaining.Remove(match);
            }
        }

        int union = tokens1.Count + tokens2.Count - matches;
        return (double)matches / union;
    }*/
}
