using System.Text.Json;

namespace PriceAggregator.Core;

public class MionScraper
{
    private readonly HttpClient _httpClient;

    public MionScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.mion.com.tr/");
    }

    // Veriyi getirir.
    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var encodedSearch = Uri.EscapeDataString(searchTerm);

        var url =
            $"https://www.migros.com.tr/rest/mion/search/screens/products?q={encodedSearch}";

        var response = await _httpClient.GetStringAsync(url);

        using var json = JsonDocument.Parse(response);

        var results = new List<ProductDto>();

        var products = json.RootElement
            .GetProperty("data")
            .GetProperty("searchInfo")
            .GetProperty("storeProductInfos");

        foreach (var p in products.EnumerateArray())
        {
            string? imageUrl = null;

            if (p.TryGetProperty("images", out var images) &&
                images.GetArrayLength() > 0)
            {
                imageUrl = images[0]
                    .GetProperty("urls")
                    .GetProperty("PRODUCT_DETAIL")
                    .GetString();
            }

            results.Add(new ProductDto
            {
                ExternalId = p.GetProperty("id").GetInt64(),

                Title = p.GetProperty("name").GetString() ?? "",

                ImageUrl = imageUrl,

                Price = p.GetProperty("shownPrice").GetInt32() / 100m,

                RegularPrice = p.GetProperty("regularPrice").GetInt32() / 100m,

                ProductUrl =
                    $"https://www.mion.com.tr/urun/{p.GetProperty("prettyName").GetString()}"
            });
        }

        return results;
    }
}
