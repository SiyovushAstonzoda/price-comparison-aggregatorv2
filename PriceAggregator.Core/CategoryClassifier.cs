namespace PriceAggregator.Core;

// Keyword-based product categorization, used only when a scraped product carries no real
// category data at all (see CategoryResolver.ResolveAsync). Name/ParentName are chosen to
// read exactly like the real Category rows Migros/MacroCenter/MarketFiyati already populate
// (e.g. "Çay" under "İçecek") so FindOrCreateAsync's exact-slug lookup reuses those existing
// nodes instead of inventing a separate, misplaced bucket sitting directly under the sector root.
public static class CategoryClassifier
{
    private static readonly (string Name, string? ParentName, string[] Keywords, string[] ExcludeKeywords)[] Rules =
    [
        // These five must stay ahead of the "Su" rule below: "çamaşır suyu" (bleach),
        // "meyve suyu" (fruit juice) and "ağız bakım suyu" (mouthwash) all end in a real
        // inflected form of "su" ("suyu"), but aren't water; "süt" (milk, normalized "sut")
        // is a different word entirely, but "su"'s suffix tolerance (see TokenMatchesKeyword)
        // is generous enough to also accept it. Rules is checked in order and the first match
        // wins, so the more specific compound/word has to be caught first.
        ("Genel Temizlik Ürünleri", "Temizlik Ürünleri", ["camasir"], []),
        ("Meyve Suyu", "İçecek", ["meyve"], []),
        ("Kişisel Bakım", null, ["agiz"], []),
        // "sucuk"/"sumak" already can't reach the "Su" rule below (see TokenMatchesKeyword's
        // length-scaled tolerance), but they had no positive home of their own either, so
        // uncategorized products with these words fell all the way to the sector root.
        ("Et, Tavuk, Balık, Şarküteri", null, ["sucuk", "salam", "sosis", "pastirma", "jambon"], []),
        ("Süt, Kahvaltılık", null, ["peynir", "yumurta", "sut", "yogurt", "tereyagi"], []),
        // "su bardağı" (drinking glass) is kitchenware, not water — it just happens to
        // contain "su" as a real, standalone word rather than a suffixed form of it. Same
        // idea for "çay bardağı" (tea glass) and "kahve fincanı" (coffee cup) below.
        ("Su", "İçecek", ["su"], ["bardak", "bardag"]),
        ("Çay", "İçecek", ["cay"], ["bardak", "bardag", "fincan"]),
        ("Kahve", "İçecek", ["kahve"], ["bardak", "bardag", "fincan"]),
        // Catches the bardak/fincan/tabak items excluded from Su/Çay/Kahve above — real
        // kitchenware, not a beverage.
        ("Sofra Ürünleri", null, ["bardak", "bardag", "fincan", "tabak", "catal", "kasik", "bicak"], []),
        ("Krem Çikolata, Ezmeler", "Süt, Kahvaltılık", ["nutella"], []),
        ("Sürülebilir Ürünler ve Kahvaltılık Soslar", "Süt, Kahvaltılık", ["recel", "bal"], []),
        ("Makarna", "Temel Gıda", ["makarna", "spagetti"], []),
        ("Bakliyat", "Temel Gıda", ["bulgur", "pirinc", "mercimek", "nohut", "fasulye"], []),
        ("Zeytinyağı", "Temel Gıda", ["zeytinyagi"], []),
        ("Sıvı Yağlar", "Temel Gıda", ["aycicekyagi", "yag"], []),
        ("Baharat ve Harçlar", "Temel Gıda", ["sumak", "baharat"], []),
    ];

    public static (string Name, string? ParentName)? Classify(string title)
    {
        var tokens = Tokenize(title);
        if (tokens.Count == 0) return null;

        foreach (var (name, parentName, keywords, excludeKeywords) in Rules)
        {
            if (excludeKeywords.Any(keyword => tokens.Any(token => TokenMatchesKeyword(token, keyword))))
                continue;

            if (keywords.Any(keyword => tokens.Any(token => TokenMatchesKeyword(token, keyword))))
                return (name, parentName);
        }

        return null;
    }

    private static bool TokenMatchesKeyword(string token, string keyword)
    {
        if (token == keyword) return true;

        // Allow short Turkish suffixes (e.g. "cay" matches "cayi", "kahveli"). The tolerance
        // scales with keyword length rather than a flat 3 chars — a flat allowance let short
        // keywords like "su" (2 chars) swallow unrelated words that merely happen to start
        // with the same two letters ("sucuk"/sausage, "sumak"/spice, +3 chars each).
        var tolerance = Math.Min(3, keyword.Length);
        return token.Length > keyword.Length
            && token.StartsWith(keyword, StringComparison.Ordinal)
            && (token.Length - keyword.Length) <= tolerance;
    }

    private static List<string> Tokenize(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return [];

        var normalized = TurkishNormalizer.Normalize(title);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
