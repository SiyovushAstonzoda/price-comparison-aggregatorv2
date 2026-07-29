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
        var safeBrand = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand;
        var (size, unit) = SizeParser.ExtractSize(title);

        using var db = new SqlConnection(_connectionString);

        // Adayları getir (Aynı marka)
        var candidates = await db.QueryAsync<MasterProductCandidate>(@"
            SELECT Id, CanonicalTitle, SizeValue, SizeUnit 
            FROM MasterProducts 
            WHERE Brand = @Brand", new { Brand = safeBrand });

        int? bestMatchId = null;
        double bestScore = 0;

        var normalizedTitle = NormalizeTitle(title);
        var titleWords = new HashSet<string>(normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var c in candidates)
        {
            // Sadece hacmi de uyanları karşılaştır (eğer ikisi de null değilse)
            if (c.SizeValue != size || c.SizeUnit != unit) continue;

            var cNormalized = NormalizeTitle(c.CanonicalTitle);
            var cWords = new HashSet<string>(cNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            double score = CalculateJaccard(titleWords, cWords);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatchId = c.Id;
            }
        }

        int masterId;
        if (bestMatchId.HasValue && bestScore > 0.80) // %80 benzerlik barajı (çok daha katı, yanlış gruplamaları önler)
        {
            masterId = bestMatchId.Value;
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

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        
        var lower = title.ToLowerInvariant();
        // Hacim birimlerini metinden çıkar
        lower = System.Text.RegularExpressions.Regex.Replace(lower, @"\d+[.,]?\d*\s*(ml|l|g|kg)\b", "");
        // Noktalama işaretlerini boşlukla değiştir
        lower = System.Text.RegularExpressions.Regex.Replace(lower, @"[^\w\s]", " ");
        return lower;
    }

    private static double CalculateJaccard(HashSet<string> set1, HashSet<string> set2)
    {
        if (set1.Count == 0 && set2.Count == 0) return 1.0;
        if (set1.Count == 0 || set2.Count == 0) return 0.0;

        int intersection = 0;
        foreach (var w in set1)
        {
            if (set2.Contains(w)) intersection++;
        }

        int union = set1.Count + set2.Count - intersection;
        return (double)intersection / union;
    }

    // Dapper için yardımcı model
    private class MasterProductCandidate
    {
        public int Id { get; set; }
        public string CanonicalTitle { get; set; } = "";
        public decimal? SizeValue { get; set; }
        public string? SizeUnit { get; set; }
    }
}