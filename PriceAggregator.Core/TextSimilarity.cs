namespace PriceAggregator.Core;

public static class TextSimilarity
{
    public static HashSet<string> Tokenize(string text, IEnumerable<string>? excludeWords = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>();

        var normalized = TurkishNormalizer.Normalize(text);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        var exclude = excludeWords?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .Where(w => !exclude.Contains(w))
            .Where(w => !w.All(char.IsDigit))
            .ToHashSet();
    }

    // Two tokens are "the same word" if identical, if one is the other's root plus a
    // short Turkish suffix (çay/çayı), or if they're a small spelling variant of each
    // other (Spaghetti/Spagetti).
    public static bool TokensMatch(string a, string b)
    {
        if (a == b) return true;

        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;
        if (longer.StartsWith(shorter, StringComparison.Ordinal) && (longer.Length - shorter.Length) <= 3)
            return true;

        if (shorter.Length >= 5)
        {
            int maxAllowedDistance = shorter.Length <= 7 ? 1 : 2;
            if (LevenshteinDistance(a, b) <= maxAllowedDistance)
                return true;
        }

        return false;
    }

    public static double Score(HashSet<string> tokens1, HashSet<string> tokens2, bool emptySetMeansCompatible)
    {
        if (tokens1.Count == 0 && tokens2.Count == 0) return 1.0;
        if (tokens1.Count == 0 || tokens2.Count == 0) return emptySetMeansCompatible ? 1.0 : 0.0;

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
}