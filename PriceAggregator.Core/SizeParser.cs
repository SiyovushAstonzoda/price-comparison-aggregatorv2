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

    public static (decimal? value, string? unit, int packQuantity) ExtractSize(string title)
    {
        // Pattern A: "N x SIZE UNIT" — multiplier BEFORE size (e.g. "12 x 500 Ml")
        var packBefore = Regex.Match(title, $@"(\d+)\s*x\s*(\d+[.,]?\d*)\s*({UnitPattern})\b", RegexOptions.IgnoreCase);
        if (packBefore.Success)
        {
            var packQty = int.Parse(packBefore.Groups[1].Value);
            var (value, unit) = Normalize(packBefore.Groups[2].Value, packBefore.Groups[3].Value);
            return (value, unit, packQty);
        }

        var sizeMatch = Regex.Match(title, $@"(\d+[.,]?\d*)\s*({UnitPattern})\b", RegexOptions.IgnoreCase);
        var countSuffix = Regex.Match(title, @"(\d+)\s*'?\s*(?:li|lı|lu|lü|adet)\b", RegexOptions.IgnoreCase);

        // Pattern B: SIZE UNIT ... N'li — multiplier AFTER size (e.g. "2 Gr 25'li")
        if (sizeMatch.Success && countSuffix.Success)
        {
            var (value, unit) = Normalize(sizeMatch.Groups[1].Value, sizeMatch.Groups[2].Value);
            var packQty = int.Parse(countSuffix.Groups[1].Value);
            return (value, unit, packQty);
        }

        // Pattern C: just a weight/volume, no separate pack count
        if (sizeMatch.Success)
        {
            var (value, unit) = Normalize(sizeMatch.Groups[1].Value, sizeMatch.Groups[2].Value);
            return (value, unit, 1);
        }

        // Pattern D: pure count, no weight at all (e.g. "48'li" tea bags with no per-bag gram listed)
        if (countSuffix.Success)
        {
            var count = decimal.Parse(countSuffix.Groups[1].Value);
            return (count, "ADET", 1);
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