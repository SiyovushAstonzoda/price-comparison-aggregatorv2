namespace PriceAggregator.Core.Matching;

// Shared fuzzy word/phrase comparison. Originally lived only inside MatchingService for
// reconciling product titles across stores; CategoryResolver/CategoryMerger reuse the same
// rules so that near-duplicate category names (plural vs singular, minor spelling variants,
// short Turkish suffixes) are recognized as the same category instead of creating duplicates.
public static class FuzzyMatcher
{
    // Two tokens are "the same word" if identical, or if one is the other's root
    // plus a short Turkish suffix (handles çay/çayı, içecek/içecekler, etc.)
    public static bool TokensMatch(string a, string b)
    {
        if (a == b) return true;

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

    public static int LevenshteinDistance(string a, string b)
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

    // Category names carry parent/child (genus/species) relationships that product titles
    // don't — "Peynir" (Cheese) must NOT match "Beyaz Peynir" (White Cheese), since the
    // extra word is a real distinguishing type, not noise. So unlike TokensMatch's use in
    // product-title matching, category names require a full bijection (same count of real
    // words, each fuzzy-paired) after stripping empty connector words — "ve"/"&" and generic
    // "ürün(ü/leri)" collective-noun suffixes are the only words treated as droppable, since
    // they add no distinguishing meaning ("Süt Ürünleri" ≈ "Süt", "X ve Y" ≈ "X" ∪ "Y" name-wise).
    private static readonly HashSet<string> FillerWords = new() { "ve", "ile", "urun", "urunu", "urunleri" };

    public static bool NamesMatch(string nameA, string nameB)
    {
        var tokensA = Tokenize(nameA);
        var tokensB = Tokenize(nameB);
        if (tokensA.Count == 0 || tokensB.Count == 0 || tokensA.Count != tokensB.Count) return false;

        var remaining = new List<string>(tokensB);
        foreach (var a in tokensA)
        {
            var match = remaining.FirstOrDefault(b => TokensMatch(a, b));
            if (match is null) return false;
            remaining.Remove(match);
        }

        return true;
    }

    private static List<string> Tokenize(string text)
    {
        var normalized = TurkishNormalizer.Normalize(text);
        var cleaned = new string(normalized.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !FillerWords.Contains(t))
            .ToList();
    }
}
