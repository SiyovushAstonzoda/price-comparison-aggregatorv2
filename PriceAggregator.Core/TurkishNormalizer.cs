namespace PriceAggregator.Core;

public static class TurkishNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        text = text.ToLowerInvariant();
        text = text.Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
                    .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c");
        return text;
    }
}