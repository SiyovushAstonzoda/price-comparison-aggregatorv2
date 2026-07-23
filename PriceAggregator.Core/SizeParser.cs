namespace PriceAggregator.Core;

using System.Text.RegularExpressions;

public static class SizeParser
{
    public static (decimal? value, string? unit) ExtractSize(string title)
    {
        // Matches things like "5 L", "500 Ml", "1.5 L", "750 g"
        var match = Regex.Match(title, @"(\d+[.,]?\d*)\s*(ml|l|g|kg)\b", RegexOptions.IgnoreCase);
        if (!match.Success) return (null, null);

        var value = decimal.Parse(match.Groups[1].Value.Replace(",", "."),
            System.Globalization.CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value.ToUpper();
        return (value, unit);
    }
}