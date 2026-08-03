namespace PriceAggregator.Core.Categorization;

// Title-keyword tagger for the one facet cosmetics/personal-care listings almost always
// filter by that groceries and furniture never need: who the product is for. Same shape as
// ProductPropertyTagger (title-only, exact-token match) but scoped separately since "Cinsiyet"
// only makes sense for this one sector — see MatchingService.UpsertAttributesAsync, which
// only calls this for products SectorClassifier already routed to the Kozmetik sector, so a
// furniture "Çocuk Yatağı" (kids' bed) never gets tagged as if it were a gendered product.
public static class PersonalCareGenderTagger
{
    private static readonly (string Value, string Keyword)[] Rules =
    [
        ("Kadın", "kadin"),
        ("Erkek", "erkek"),
        ("Çocuk", "cocuk"),
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
        if (string.IsNullOrWhiteSpace(title)) return [];

        var normalized = TurkishNormalizer.Normalize(title);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
}
