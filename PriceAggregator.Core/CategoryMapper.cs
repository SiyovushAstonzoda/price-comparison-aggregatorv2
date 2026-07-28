
public static class CategoryMapper
{
    // Maps YOUR search keyword -> canonical category name
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

    // Maps a RAW source category string -> canonical category name
    // (grows as you see more raw category strings from different sources)
    private static readonly Dictionary<string, string> RawCategoryToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["su"] = "Su",
        ["sular"] = "Su",
        ["maden suyu"] = "Su",
        ["sade sular"] = "Su",
        ["su ve maden suyu"] = "Su",
        ["su & maden suyu"] = "Su",
        ["çamaşır suyu"] = "Temizlik",
        ["çay"] = "Çay",
        ["kahve"] = "Kahve",
        ["makarna"] = "Makarna",
    };

    public static string? Resolve(string searchTerm, string? rawSourceCategory)
    {
        if (!string.IsNullOrWhiteSpace(rawSourceCategory))
        {
            if (RawCategoryToCategory.TryGetValue(rawSourceCategory.Trim(), out var mapped))
            {
                return mapped;
            }

            // We DO have real category info, we just don't recognize this specific string yet.
            // Don't blindly trust the search keyword here — that's exactly how mouthwash,
            // sunscreen, and sucuk leaked into "Su" results. Leave it unclassified instead.
            return null;
        }

        // No category info available at all from this source — fall back to the
        // search keyword as our best available guess.
        if (KeywordToCategory.TryGetValue(searchTerm, out var fromKeyword))
        {
            return fromKeyword;
        }

        return null;
    }

    // Returns true if the source's own category actively CONTRADICTS the expected
    // keyword-derived category — useful for filtering out false-positive search hits
    // (e.g. "Domestos Çamaşır Suyu" matching a "su" search).
    public static bool IsLikelyFalsePositive(string searchTerm, string? rawSourceCategory)
    {
        if (string.IsNullOrWhiteSpace(rawSourceCategory)) return false;
        if (!KeywordToCategory.TryGetValue(searchTerm, out var expectedCategory)) return false;

        var resolved = Resolve(searchTerm, rawSourceCategory);
        return !string.Equals(resolved, expectedCategory, StringComparison.OrdinalIgnoreCase);
    }
}