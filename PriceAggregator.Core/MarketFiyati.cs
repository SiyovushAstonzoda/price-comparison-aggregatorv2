using System.Text;
using System.Text.Json;

namespace PriceAggregator.Core;

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

    // Veriyi getirir.
    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var results = new List<ProductDto>();

        var payload = new
        {
            keywords = searchTerm,
            pages = 0,
            size = 24,
            latitude = 41.08037598,
            longitude = 28.79330796,
            distance = 1,
            depots = new[] { "bim-U153", "a101-D021", "a101-I398", "bim-U160", "a101-F979" }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("https://api.marketfiyati.org.tr/api/v2/search", content);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var products = document.RootElement.GetProperty("content");

        foreach (var p in products.EnumerateArray())
        {
            decimal price = 0;
            if (p.TryGetProperty("productDepotInfoList", out var depots) && depots.GetArrayLength() > 0)
            {
                price = depots[0].GetProperty("price").GetDecimal();
            }

            var idString = p.GetProperty("id").GetString() ?? "";

            results.Add(new ProductDto
            {
                ExternalId = StableHash(idString),
                Title = p.GetProperty("title").GetString() ?? "",
                Brand = p.TryGetProperty("brand", out var brandEl) ? brandEl.GetString() : null,
                ImageUrl = p.TryGetProperty("imageUrl", out var image) ? image.GetString() : null,
                Price = price,
                RegularPrice = price,
                SourceCategory = p.TryGetProperty("main_category", out var mainCat) ? mainCat.GetString() : null,
                ProductUrl = $"https://marketfiyati.org.tr/detay/{idString}/{GenerateSlug(p.GetProperty("title").GetString() ?? "")}"
            });
        }

        return results;
    }

    private static long StableHash(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToInt64(bytes, 0);
    }
}
