using System.Text;
using System.Text.Json;

namespace PriceAggregator.Core;

public class EvideaScraper
{
    private readonly HttpClient _httpClient;

    public EvideaScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.evidea.com/");
        _httpClient.DefaultRequestHeaders.Remove("Origin");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://www.evidea.com");
    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var url = "https://api.tr.primewidgets.com/products/searchv5/evidea?=";

        var requestBody = new
        {
            search = searchTerm,
            categories = Array.Empty<string>(),
            brands = Array.Empty<string>(),
            attributes = new[]
            {
                new { name = "color", values = Array.Empty<string>() },
                new { name = "gender", values = Array.Empty<string>() }
            },
            filter = new { orderBy = "DESC", sortBy = "sales.monthly" },
            paging = new { index = 1, size = "24" },
            price = new { min = (decimal?)null, max = (decimal?)null },
            status = "ACTIVE"
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseText);

        var results = new List<ProductDto>();

        // results.products.data
        if (!json.RootElement.TryGetProperty("results", out var resultsEl)) return results;
        if (!resultsEl.TryGetProperty("products", out var productsEl)) return results;
        if (!productsEl.TryGetProperty("data", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var p in dataArray.EnumerateArray())
        {
            string code = p.GetProperty("code").GetString() ?? "";
            string title = p.GetProperty("name").GetString() ?? "";
            string productUrl = p.GetProperty("url").GetString() ?? "";

            decimal price = p.GetProperty("price").GetDecimal();
            decimal regularPrice = p.TryGetProperty("oldPrice", out var oldPriceProp) && oldPriceProp.ValueKind != JsonValueKind.Null
                ? oldPriceProp.GetDecimal()
                : price;

            string? imageUrl = null;
            if (p.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array && images.GetArrayLength() > 0)
            {
                imageUrl = images[0].GetString();
            }

            // Sayisal urun ID'si "attributes" dizisi icinde name == "pk" olan elemanin value'sunda
            long externalId = 0;
            if (p.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Array)
            {
                foreach (var attr in attributes.EnumerateArray())
                {
                    if (attr.TryGetProperty("name", out var attrName) && attrName.GetString() == "pk")
                    {
                        if (attr.TryGetProperty("value", out var pkValue) &&
                            long.TryParse(pkValue.GetString(), out var parsedPk))
                        {
                            externalId = parsedPk;
                        }
                        break;
                    }
                }
            }

            // pk bulunamazsa yedek olarak code'un hash'i yerine 0 birakiyoruz - istersen code'u ayri bir kolonda da tutabilirsin
            results.Add(new ProductDto
            {
                ExternalId = externalId,
                Title = title,
                ImageUrl = imageUrl,
                Price = price,
                RegularPrice = regularPrice,
                ProductUrl = productUrl
            });
        }

        return results;
    }
}