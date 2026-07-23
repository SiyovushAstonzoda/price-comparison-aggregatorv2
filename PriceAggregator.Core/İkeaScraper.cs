using System.Text.Json;

namespace PriceAggregator.Core;

public class IkeaScraper
{
    private readonly HttpClient _httpClient;

    public IkeaScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;

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
                "tr-TR,tr;q=0.9"
            );
        }

        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add(
            "Referer",
            "https://www.ikea.com.tr/"
        );

    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var url =
            "https://frontendapi.ikea.com.tr/api/search/products" +
            $"?Keyword={Uri.EscapeDataString(searchTerm)}" +
            "&language=tr" +
            "&IncludeFilters=false" +
            "&StoreCode=331" +
            "&sortby=None" +
            "&page=1" +
            "&size=24" +
            "&SearchIn=product" +
            "&IncludeColorVariants=true";

        var response = await _httpClient.GetStringAsync(url);

        using var json = JsonDocument.Parse(response);

        var results = new List<ProductDto>();

        if (!json.RootElement.TryGetProperty("products", out var products) ||
            products.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var p in products.EnumerateArray())
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

            results.Add(new ProductDto
            {
                ExternalId = id,
                Title = productName,
                ImageUrl = imageUrl,
                Price = price,
                RegularPrice = regularPrice,
                ProductUrl = productUrl
            });
        }

        return results;
    }
}
