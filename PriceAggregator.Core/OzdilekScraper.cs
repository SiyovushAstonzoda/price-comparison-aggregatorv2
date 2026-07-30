using System.Text.Json;

namespace PriceAggregator.Core;

public class OzdilekScraper
{
    private static readonly string[] MaterialKeywords =
    {
        "plastik", "ahşap", "ağaç", "metal", "cam", "kumaş", "deri", "bambu",
        "seramik", "paslanmaz", "çelik", "mdf", "sunta", "mermer", "teak", "rattan", "pamuk", "polyester"
    };

    private readonly HttpClient _httpClient;

    public OzdilekScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0.0.0 Safari/537.36"
            );
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "Accept",
                "application/json, text/plain, */*"
            );
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("Accept-Language"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "Accept-Language",
                "tr-TR,tr;q=0.9,en;q=0.8"
            );
        }

        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add(
            "Referer",
            "https://www.ozdilekteyim.com/"
        );
    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm, int maxPages = 5)
    {
        var byId = new Dictionary<long, ProductDto>();

        for (var page = 1; page <= maxPages; page++)
        {
            var batch = await FetchPageAsync(searchTerm, page);
            if (batch.Count == 0)
                break;

            foreach (var product in batch)
                byId[product.ExternalId] = product;

            if (batch.Count < 24)
                break;
        }

        return byId.Values.ToList();
    }

    private async Task<List<ProductDto>> FetchPageAsync(string searchTerm, int page)
    {
        var results = new List<ProductDto>();

        try
        {
            var url = $"https://api.ozdilekteyim.com/rest/v2/magaza-magaza-store/products/search?query={Uri.EscapeDataString(searchTerm)}&currentPage={page - 1}&pageSize=24";

            var response = await _httpClient.GetStringAsync(url);
            using var json = JsonDocument.Parse(response);

            if (!json.RootElement.TryGetProperty("products", out var products) ||
                products.ValueKind != JsonValueKind.Array)
            {
                if (page == 1)
                    Logger.Log("[Ozdilek] Unexpected response shape — no products found.");
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
                    Logger.Log($"[Ozdilek] Skipped one product due to parse error: {ex.Message}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Log($"[Ozdilek] Network error (page {page}): {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Logger.Log($"[Ozdilek] Request timed out (page {page}).");
        }
        catch (JsonException ex)
        {
            Logger.Log($"[Ozdilek] Failed to parse JSON (page {page}): {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[Ozdilek] Error fetching products (page {page}): {ex.Message}");
        }

        return results;
    }

    private ProductDto ParseProduct(JsonElement p)
    {
        string? imageUrl = null;
        if (p.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in images.EnumerateArray())
            {
                if (img.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    var url = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        imageUrl = url;
                        // Prefer PRIMARY product image if available
                        if (img.TryGetProperty("imageType", out var typeProp) && typeProp.GetString() == "PRIMARY" &&
                            img.TryGetProperty("format", out var formatProp) && formatProp.GetString() == "product")
                        {
                            break;
                        }
                    }
                }
            }
        }

        var productName = p.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";

        var customUrl = p.TryGetProperty("customUrl", out var uProp) ? uProp.GetString() ?? "" : "";
        var productUrl = string.IsNullOrWhiteSpace(customUrl)
            ? "https://www.ozdilekteyim.com"
            : (customUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? customUrl
                : $"https://www.ozdilekteyim.com/{customUrl.TrimStart('/')}");

        long id = 0;
        if (p.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
        {
            var codeStr = codeProp.GetString();
            if (!string.IsNullOrWhiteSpace(codeStr))
            {
                // Generate consistent numeric id from code string
                id = Math.Abs((long)codeStr.GetHashCode());
            }
        }

        decimal price = 0;
        if (p.TryGetProperty("price", out var priceObj) && priceObj.ValueKind == JsonValueKind.Object)
        {
            if (priceObj.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.Number)
            {
                price = valProp.GetDecimal();
            }
        }

        decimal regularPrice = price;
        if (p.TryGetProperty("listPrice", out var listPriceObj) && listPriceObj.ValueKind == JsonValueKind.Object)
        {
            if (listPriceObj.TryGetProperty("value", out var regValProp) && regValProp.ValueKind == JsonValueKind.Number)
            {
                regularPrice = regValProp.GetDecimal();
            }
        }

        string brand = "Özdilek";
        if (p.TryGetProperty("brand", out var brandProp) && brandProp.ValueKind == JsonValueKind.String)
        {
            var brandStr = brandProp.GetString();
            if (!string.IsNullOrWhiteSpace(brandStr)) brand = brandStr;
        }

        string? category = null;
        string? midCategory = null;
        string? subCategory = null;

        if (p.TryGetProperty("categories", out var catsObj) && catsObj.ValueKind == JsonValueKind.Array)
        {
            var catList = new List<string>();
            foreach (var catItem in catsObj.EnumerateArray())
            {
                if (catItem.TryGetProperty("name", out var catNameProp) && catNameProp.ValueKind == JsonValueKind.String)
                {
                    var catName = catNameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(catName)) catList.Add(catName);
                }
            }
            if (catList.Count > 0) category = catList[0];
            if (catList.Count > 1) midCategory = catList[1];
            if (catList.Count > 2) subCategory = catList[^1];
        }

        string? color = null;
        string? dimensions = null;

        if (p.TryGetProperty("properties", out var propsObj) && propsObj.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsObj.EnumerateArray())
            {
                var name = prop.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                var val = prop.TryGetProperty("value", out var vProp) ? vProp.GetString() : null;

                if (string.Equals(name, "color", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "renk", StringComparison.OrdinalIgnoreCase))
                {
                    color = val;
                }
                else if (string.Equals(name, "ebat", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "boyut", StringComparison.OrdinalIgnoreCase))
                {
                    dimensions = val;
                }
            }
        }

        var description = p.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
        var material = ExtractMaterial(productName, description);

        return new ProductDto
        {
            ExternalId = id,
            Title = productName,
            Brand = brand,
            ImageUrl = imageUrl,
            Price = price,
            RegularPrice = regularPrice,
            ProductUrl = productUrl,
            Category = category,
            MidCategory = midCategory,
            SubCategory = subCategory,
            Color = color,
            Dimensions = dimensions,
            ProductType = subCategory ?? category,
            Material = material
        };
    }

    private static string? ExtractMaterial(string? title, string? description)
    {
        var text = $"{title} {description}".Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (var keyword in MaterialKeywords)
        {
            if (!text.Contains(keyword, StringComparison.Ordinal))
                continue;

            return char.ToUpper(keyword[0]) + keyword[1..];
        }

        return null;
    }
}
