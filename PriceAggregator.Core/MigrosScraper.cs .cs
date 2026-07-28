using System.Text.Json;

namespace PriceAggregator.Core;

public class MigrosScraper
{
    private readonly HttpClient _httpClient;

    public MigrosScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.migros.com.tr/");
    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var results = new List<ProductDto>();

        try
        {
            var url = $"https://www.migros.com.tr/rest/products/search?query={searchTerm}&sirala=akilli-siralama";
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            if (!json.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("storeProductInfos", out var products))
            {
                Logger.Log("[Migros] Unexpected response shape — no products found.");
                return results;
            }

            foreach (var p in products.EnumerateArray())
            {
                try
                {
                    results.Add(ParseProduct(p));
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Migros] Skipped one product due to parse error: {ex.Message}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Log($"[Migros] Network error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Logger.Log("[Migros] Request timed out.");
        }
        catch (JsonException ex)
        {
            Logger.Log($"[Migros] Failed to parse JSON: {ex.Message}");
        }

        return results;
    }

    private ProductDto ParseProduct(JsonElement p)
    {
        string? imageUrl = null;
        if (p.TryGetProperty("images", out var images) && images.GetArrayLength() > 0 &&
            images[0].TryGetProperty("urls", out var urls) &&
            urls.TryGetProperty("PRODUCT_DETAIL", out var detailUrl))
        {
            imageUrl = detailUrl.GetString();
        }

        string? brand = null;
        if (p.TryGetProperty("brand", out var brandEl) && brandEl.TryGetProperty("name", out var brandName))
            brand = brandName.GetString();


        string? sourceCategory = null;
        if (p.TryGetProperty("category", out var catEl) && catEl.TryGetProperty("name", out var catName))
            sourceCategory = catName.GetString();

        return new ProductDto
        {
            ExternalId = p.GetProperty("id").GetInt64(),
            Title = p.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Brand = brand,
            ImageUrl = imageUrl,
            Price = p.TryGetProperty("shownPrice", out var price) ? price.GetInt32() / 100m : 0,
            RegularPrice = p.TryGetProperty("regularPrice", out var regPrice) ? regPrice.GetInt32() / 100m : 0,
            SourceCategory = sourceCategory,
            ProductUrl = p.TryGetProperty("prettyName", out var pretty)
                ? $"https://www.migros.com.tr/{pretty.GetString()}"
                : ""
        };
    }
}