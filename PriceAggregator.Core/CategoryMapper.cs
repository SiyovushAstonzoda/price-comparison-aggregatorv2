using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class CategoryMapper
{
<<<<<<< Updated upstream
    private readonly string _connectionString;

    public CategoryMapper(string connectionString)
=======
    // Arama kelimesini standart kategori adına eşler.
    private static readonly Dictionary<string, string> KeywordToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["su"] = "Su",
        ["çay"] = "Çay",
        ["kahve"] = "Kahve",
        ["makarna"] = "Makarna",
        ["zeytinyağı"] = "Zeytinyağı",
        ["peynir"] = "Peynir",
        ["yumurta"] = "Yumurta",
        ["süt"] = "Süt",
        ["ekmek"] = "Ekmek",
        ["nutella"] = "Kahvaltılık"
    };

    // Kaynak siteden gelen ham kategori adını standart kategori adına eşler.
    // Yeni kaynak kategorileri görüldükçe bu sözlüğe eklenir.
    private static readonly Dictionary<string, string> RawCategoryToCategory = new(StringComparer.OrdinalIgnoreCase)
>>>>>>> Stashed changes
    {
        _connectionString = connectionString;
    }

<<<<<<< Updated upstream
    public async Task<int?> ResolveAsync(string searchTerm, string? rawSourceCategory)
=======
    // Ham kaynak kategorisini veya arama kelimesini standart kategori adına dönüştürür.
    public static string? Resolve(string searchTerm, string? rawSourceCategory)
>>>>>>> Stashed changes
    {
        using var db = new SqlConnection(_connectionString);

        if (!string.IsNullOrWhiteSpace(rawSourceCategory))
        {
            var byRaw = await db.QuerySingleOrDefaultAsync<int?>(
                "SELECT CategoryId FROM CategoryRawMap WHERE RawCategoryText = @Raw",
                new { Raw = rawSourceCategory.Trim().ToLowerInvariant() });

<<<<<<< Updated upstream
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
=======
            // Kaynak kategori bilgisi var; ancak bu değer henüz sözlükte tanımlı değil.
            // Arama kelimesine doğrudan güvenilmez; aksi hâlde alakasız ürünler
            // yanlış kategoriye düşebilir. Bu durumda ürün sınıflandırılmadan bırakılır.
            return null;
        }

        // Kaynak kategori bilgisi yoksa en iyi tahmin olarak
        // arama kelimesinin eşlendiği kategori kullanılır.
        if (KeywordToCategory.TryGetValue(searchTerm, out var fromKeyword))
        {
            return fromKeyword;
        }

        return null;
    }

    // Kaynak kategorisinin arama kelimesiyle çelişip çelişmediğini kontrol eder.
    // Bu kontrol, yanlış pozitif ürünleri filtrelemek için kullanılır.
    public static bool IsLikelyFalsePositive(string searchTerm, string? rawSourceCategory)
>>>>>>> Stashed changes
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
