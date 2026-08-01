namespace PriceAggregator.Core;

public static class TurkishNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // Invariant-culture ToLower doesn't map capital dotted İ (U+0130) to "i" — it's
        // left untouched, so it must be replaced before lowercasing or it gets silently
        // dropped downstream by callers that strip non a-z characters (e.g. slug generation).
        text = text.Replace("İ", "i");
        text = text.ToLowerInvariant();
        text = text.Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
                    .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c");
        return text;
    }
}