using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using System.Text;


using System.Text.Json;



namespace PriceAggregator.Infrastructure.Scrapers.Market;

public class MarketFiyatiScraper
{
    private readonly HttpClient _httpClient;

            public string GenerateSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            // 1. Convert to lowercase invariant to handle culture-specific casing safely
            title = title.ToLowerInvariant();

            // 2. Replace common Turkish/special characters
            title = title.Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c");

            // 3. Remove all characters that are not lowercase alphanumeric, spaces, or hyphens
            title = System.Text.RegularExpressions.Regex.Replace(title, @"[^a-z0-9\s-]", "");

            // 4. Clean up multiple spaces and hyphens into a single space
            title = System.Text.RegularExpressions.Regex.Replace(title, @"[\s-]+", " ").Trim();

            // 5. Replace the remaining single spaces with hyphens
            title = title.Replace(" ", "-");

            return title;
        }

    public MarketFiyatiScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Remove("Origin");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://marketfiyati.org.tr");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://marketfiyati.org.tr/");
    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var payload = new
        {
            keywords = searchTerm,
            pages = 0,
            size = 24,
            latitude = 41.08037598,
            longitude = 28.79330796,
            distance = 1,
            depots = new[]
            {
                "bim-U153",
                "a101-D021",
                "a101-I398",
                "bim-U160",
                "a101-F979"
            }
        };

        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(
            "https://api.marketfiyati.org.tr/api/v2/search",
            content);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var results = new List<ProductDto>();

        var products = document.RootElement.GetProperty("content");

        foreach (var p in products.EnumerateArray())
        {
            decimal price = 0;

            if (p.TryGetProperty("productDepotInfoList", out var depots) &&
                depots.GetArrayLength() > 0)
            {
                price = depots[0]
                    .GetProperty("price")
                    .GetDecimal();
            }

            results.Add(new ProductDto
            {
                ExternalId = (p.GetProperty("id").GetString()?.GetHashCode() ?? 0).ToString(),

                Title = p.GetProperty("title").GetString() ?? "",

                ImageUrl = p.TryGetProperty("imageUrl", out var image)
                    ? image.GetString()
                    : null,

                Price = price,

                RegularPrice = price,

                ProductUrl = $"https://marketfiyati.org.tr/detay/{p.GetProperty("id").GetString()}/{GenerateSlug(p.GetProperty("title").GetString() ?? "")}"
            });
        }

        return results;
    }
}
