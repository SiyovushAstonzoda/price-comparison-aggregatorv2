namespace PriceAggregator.Core.Categorization;

// Title-keyword tagger for grocery dietary/lifestyle properties (e.g. "Organik",
// "Laktozsuz"). Migros's search API exposes these as a real "Yaşam & Beslenme Tarzı"
// facet — confirming shoppers actually filter by them — but only at the search-aggregation
// level, not attached to individual product objects, so per-product tagging has to fall
// back to the title itself (same reasoning as CategoryClassifier's keyword rules). Title-only
// means this runs identically for every Market source (Migros, MacroCenter, MarketFiyati,
// Hakmar, Cagri), not just the one the vocabulary was sourced from.
//
// Vocabulary confirmed by sampling that real facet across many search terms (süt, yoğurt,
// çikolata, cips, makarna, kahve, çay, ...) rather than guessed.
public static class ProductPropertyTagger
{
    private static readonly (string Value, string Keyword)[] Rules =
    [
        ("Organik", "organik"),
        ("Laktozsuz", "laktozsuz"),
        ("Glutensiz", "glutensiz"),
        ("Şekersiz", "sekersiz"),
        ("Vegan", "vegan"),
        ("Vejetaryen", "vejetaryen"),
        ("Probiyotik", "probiyotik"),
        ("Superfood", "superfood"),
        ("Kafeinsiz", "kafeinsiz"),
        ("Kolajen İçeren", "kolajen"),
    ];

    public static List<string> Tag(string title)
    {
        var tokens = Tokenize(title);
        return Rules
            .Where(rule => tokens.Contains(rule.Keyword))
            .Select(rule => rule.Value)
            .ToList();
    }

    private static HashSet<string> Tokenize(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return new HashSet<string>();

        var normalized = TurkishNormalizer.Normalize(title);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
}
