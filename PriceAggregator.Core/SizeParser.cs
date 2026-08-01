using System.Text.RegularExpressions;

namespace PriceAggregator.Core;

public static class SizeParser
{
    // Longer/more specific unit strings first isn't strictly required (word boundaries
    // handle ambiguity), but keeping this order for readability.
    private static readonly Dictionary<string, (string CanonicalUnit, decimal Multiplier)> UnitMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gr"] = ("G", 1),
            ["g"] = ("G", 1),
            ["kg"] = ("G", 1000),
            ["ml"] = ("ML", 1),
            ["cc"] = ("ML", 1),
            ["lt"] = ("ML", 1000),
            ["l"] = ("ML", 1000),
        };

    private const string UnitPattern = "gr|g|kg|ml|cc|lt|l";

    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static (decimal? value, string? unit, int packQuantity) ExtractSize(string title)
    {
        // "N x SIZE UNIT" pattern (e.g. "12 x 500 Ml")
        var packMatch = Regex.Match(title, $@"(\d+)\s*x\s*(\d+[.,]?\d*)\s*({UnitPattern})\b", Opts);
        if (packMatch.Success)
        {
            var packQty = int.Parse(packMatch.Groups[1].Value);
            var (value, unit) = Normalize(packMatch.Groups[2].Value, packMatch.Groups[3].Value);
            return (value, unit, packQty);
        }

        // Weight/volume pattern (e.g. "500 Ml", "1 Kg")
        var singleMatch = Regex.Match(title, $@"(\d+[.,]?\d*)\s*({UnitPattern})\b", Opts);
        if (singleMatch.Success)
        {
            var (value, unit) = Normalize(singleMatch.Groups[1].Value, singleMatch.Groups[2].Value);
            return (value, unit, 1);
        }

        // Count pattern: "12 Adet", "12'li", "12 li", "15Li" — for items sold by piece, not
        // weight. The apostrophe/space between the number and the Turkish "lı/li/lu/lü"
        // suffix is inconsistent across store feeds, and RegexOptions.IgnoreCase alone won't
        // fold the dotless "ı"/"İ" pair (the "Turkish I problem"), so both must be spelled
        // out explicitly rather than relying on case-insensitivity.
        var countMatch = Regex.Match(title, @"(\d+)\s*(?:adet\b|['’]?\s?l[iIıİuüÜ]\b)", Opts);
        if (countMatch.Success)
        {
            var count = decimal.Parse(countMatch.Groups[1].Value);
            return (count, "ADET", 1);
        }

        // Bulk items with no explicit number ("Muz Yerli Kg", "Ünal Taze Lor Peyniri Kg") are
        // priced per kilogram/litre directly — the listed price already IS the unit price.
        var bareUnitMatch = Regex.Match(title, @"\b(kg|kilogram|lt|litre)\b", Opts);
        if (bareUnitMatch.Success)
        {
            var isWeight = bareUnitMatch.Groups[1].Value.ToLowerInvariant() is "kg" or "kilogram";
            return (1000m, isWeight ? "G" : "ML", 1);
        }

        return (null, null, 1);
    }

    private static (decimal value, string unit) Normalize(string rawValue, string rawUnit)
    {
        var value = decimal.Parse(rawValue.Replace(",", "."),
            System.Globalization.CultureInfo.InvariantCulture);

        var (canonicalUnit, multiplier) = UnitMap[rawUnit.ToLowerInvariant()];

        return (value * multiplier, canonicalUnit);
    }
}