using System.Text.Json;

namespace PriceAggregator.Core;

public class IkeaScraper
{
    private static readonly string[] MaterialKeywords =
    {
        "plastik", "ahşap", "ağaç", "metal", "cam", "kumaş", "deri", "bambu",
        "seramik", "paslanmaz", "çelik", "mdf", "sunta", "mermer", "teak", "rattan"
    };

    private readonly HttpClient _httpClient;

    public IkeaScraper(HttpClient httpClient)
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
            "https://www.ikea.com.tr/"
        );
    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm, int maxPages = 8)
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
            var url =
                "https://frontendapi.ikea.com.tr/api/search/products" +
                $"?Keyword={Uri.EscapeDataString(searchTerm)}" +
                "&language=tr" +
                "&IncludeFilters=true" +
                "&StoreCode=331" +
                "&sortby=None" +
                $"&page={page}" +
                "&size=24" +
                "&SearchIn=product" +
                "&IncludeColorVariants=true";

            var response = await _httpClient.GetStringAsync(url);
            using var json = JsonDocument.Parse(response);

            if (!json.RootElement.TryGetProperty("products", out var products) ||
                products.ValueKind != JsonValueKind.Array)
            {
                if (page == 1)
                    Logger.Log("[Ikea] Unexpected response shape — no products found.");
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
                    Logger.Log($"[Ikea] Skipped one product due to parse error: {ex.Message}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Log($"[Ikea] Network error (page {page}): {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Logger.Log($"[Ikea] Request timed out (page {page}).");
        }
        catch (JsonException ex)
        {
            Logger.Log($"[Ikea] Failed to parse JSON (page {page}): {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[Ikea] Error fetching products (page {page}): {ex.Message}");
        }

        return results;
    }

    private ProductDto ParseProduct(JsonElement p)
    {
        string? imageUrl = null;
        if (
            p.TryGetProperty("images", out var images) &&
            images.ValueKind == JsonValueKind.Array &&
            images.GetArrayLength() > 0 &&
            images[0].TryGetProperty("image", out var imgProp)
        )
        {
            imageUrl = imgProp.GetString();
        }

        var title = p.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
        var subTitle = p.TryGetProperty("subTitle", out var stProp) ? stProp.GetString() ?? "" : "";
        var productName = $"{title} {subTitle}".Trim();

        var productPath = p.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : "";
        var productUrl = string.IsNullOrWhiteSpace(productPath)
            ? "https://www.ikea.com.tr"
            : (productPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? productPath
                : $"https://www.ikea.com.tr{productPath}");

        long id = 0;
        if (p.TryGetProperty("id", out var idProp))
        {
            if (idProp.ValueKind == JsonValueKind.Number)
                id = idProp.GetInt64();
            else if (idProp.ValueKind == JsonValueKind.String && long.TryParse(idProp.GetString(), out var parsedId))
                id = parsedId;
        }

        decimal price = 0;
        if (p.TryGetProperty("price", out var priceProp) && priceProp.ValueKind == JsonValueKind.Number)
        {
            price = priceProp.GetDecimal();
        }

        decimal regularPrice = price;
        if (p.TryGetProperty("regularPrice", out var regPriceProp) && regPriceProp.ValueKind == JsonValueKind.Number)
        {
            regularPrice = regPriceProp.GetDecimal();
        }

        string brand = "IKEA";
        if (p.TryGetProperty("brand", out var brandProp) && brandProp.ValueKind == JsonValueKind.String)
        {
            var brandStr = brandProp.GetString();
            if (!string.IsNullOrWhiteSpace(brandStr)) brand = brandStr;
        }

        var (category, midCategory, subCategory) = ParseCategories(p);

        string? color = null;
        if (p.TryGetProperty("unitColorName", out var colorProp) && colorProp.ValueKind == JsonValueKind.String)
        {
            color = colorProp.GetString();
        }

        var dimensions = ParseDimensions(p);

        var functionName = p.TryGetProperty("functionName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String
            ? fnProp.GetString()
            : null;
        var productType = !string.IsNullOrWhiteSpace(functionName) ? functionName : subTitle;
        if (string.IsNullOrWhiteSpace(productType))
            productType = subTitle;

        var material = ExtractMaterial(subTitle, functionName);

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
            ProductType = productType,
            Material = material
        };
    }

    private static (string? category, string? midCategory, string? subCategory) ParseCategories(JsonElement p)
    {
        if (!p.TryGetProperty("navigations", out var navs) ||
            navs.ValueKind != JsonValueKind.Array ||
            navs.GetArrayLength() == 0)
        {
            return (null, null, null);
        }

        JsonElement? bestBreadcrumb = null;
        var bestDepth = -1;

        foreach (var nav in navs.EnumerateArray())
        {
            if (!nav.TryGetProperty("breadcrumb", out var bc) ||
                bc.ValueKind != JsonValueKind.Array ||
                bc.GetArrayLength() == 0)
            {
                continue;
            }

            if (bc.GetArrayLength() > bestDepth)
            {
                bestDepth = bc.GetArrayLength();
                bestBreadcrumb = bc;
            }
        }

        if (bestBreadcrumb is null)
        {
            var firstNav = navs[0];
            var navOnlyName = firstNav.TryGetProperty("name", out var navName) ? navName.GetString() : null;
            return (navOnlyName, null, navOnlyName);
        }

        var breadcrumb = bestBreadcrumb.Value;
        var len = breadcrumb.GetArrayLength();

        string? NameAt(int index)
        {
            if (index < 0 || index >= len) return null;
            return breadcrumb[index].TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        }

        var top = NameAt(0);
        var leaf = NameAt(len - 1);
        string? mid = null;
        if (len >= 3)
            mid = NameAt(1);
        else if (len == 2)
            mid = null;

        if (string.IsNullOrWhiteSpace(leaf) &&
            navs[0].TryGetProperty("name", out var fallbackName))
        {
            leaf = fallbackName.GetString();
        }

        if (string.IsNullOrWhiteSpace(top))
            top = leaf;

        return (top, mid, leaf);
    }

    private static string? ParseDimensions(JsonElement p)
    {
        var parts = new List<string>();
        foreach (var key in new[] { "variant1", "variant2", "variant3" })
        {
            if (p.TryGetProperty(key, out var variant) &&
                variant.ValueKind == JsonValueKind.String)
            {
                var value = variant.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add(value.Trim());
            }
        }

        if (parts.Count == 0)
            return null;

        return string.Join(" · ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? ExtractMaterial(string? subTitle, string? functionName)
    {
        var text = $"{subTitle} {functionName}".Trim().ToLowerInvariant();
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
