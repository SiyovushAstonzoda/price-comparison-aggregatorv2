using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
namespace PriceAggregator.Core.Utils;

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
