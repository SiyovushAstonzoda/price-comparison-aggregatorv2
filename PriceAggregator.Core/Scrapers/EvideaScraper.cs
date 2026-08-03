using System.Text;
using System.Text.Json;

namespace PriceAggregator.Core.Scrapers;

public class EvideaScraper : IProductScraper
{
    public string Name => "evidea";
    public ScraperSector Sector => ScraperSector.Mobilya;

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

    private const int PageSize = 24;
    private const int MaxPages = 3;

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var results = new List<ProductDto>();

        for (int pageIndex = 1; pageIndex <= MaxPages; pageIndex++)
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
                paging = new { index = pageIndex, size = PageSize.ToString() },
                price = new { min = (decimal?)null, max = (decimal?)null },
                status = "ACTIVE"
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(responseText);

            // results.products.data
            if (!json.RootElement.TryGetProperty("results", out var resultsEl)) break;
            if (!resultsEl.TryGetProperty("products", out var productsEl)) break;
            if (!productsEl.TryGetProperty("data", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array)
                break;

            foreach (var p in dataArray.EnumerateArray())
            {
                string code = p.GetProperty("code").GetString() ?? "";
                string title = p.GetProperty("name").GetString() ?? "";
                string productUrl = p.GetProperty("url").GetString() ?? "";

                string brand = p.TryGetProperty("brand", out var brandEl) && brandEl.ValueKind == JsonValueKind.Object
                    ? brandEl.GetProperty("name").GetString() ?? ""
                    : "";

                decimal price = p.GetProperty("price").GetDecimal();
                decimal regularPrice = p.TryGetProperty("oldPrice", out var oldPriceProp) && oldPriceProp.ValueKind != JsonValueKind.Null
                    ? oldPriceProp.GetDecimal()
                    : price;

                string? imageUrl = null;
                if (p.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array && images.GetArrayLength() > 0)
                {
                    imageUrl = images[0].GetString();
                }

                // Sayisal urun ID'si "attributes" dizisi icinde name == "pk" olan elemanin value'sunda.
                // Same array also carries "product_type" — a ">"-separated breadcrumb string
                // (e.g. "Tamamlayıcı Mobilya > Çalışma Odası > Çalışma Sandalyesi") that's the
                // real category path for this product, so both are read in one pass.
                long externalId = 0;
                string? productType = null;
                string? color = null;
                string? material = null;
                if (p.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var attr in attributes.EnumerateArray())
                    {
                        if (!attr.TryGetProperty("name", out var attrName)) continue;
                        var an = attrName.GetString();

                        if (an == "pk" && attr.TryGetProperty("value", out var pkValue) &&
                            long.TryParse(pkValue.GetString(), out var parsedPk))
                        {
                            externalId = parsedPk;
                        }
                        else if (an == "product_type" && attr.TryGetProperty("value", out var ptValue))
                        {
                            productType = ptValue.GetString();
                        }
                        else if (an == "color" && attr.TryGetProperty("value", out var colorValue))
                        {
                            color = colorValue.GetString();
                        }
                        // "material" is short/clean for furniture ("Kumaş / Sünger / Plastik")
                        // but a long fabric-composition sentence for some textiles — either
                        // way MatchingService.SplitCompoundValue breaks it into real filter
                        // values, same handling as the color combos.
                        else if (an == "material" && attr.TryGetProperty("value", out var materialValue))
                        {
                            material = materialValue.GetString();
                        }
                    }
                }

                Dictionary<string, string>? productAttributes = null;
                if (!string.IsNullOrWhiteSpace(color) || !string.IsNullOrWhiteSpace(material))
                {
                    productAttributes = new Dictionary<string, string>();
                    if (!string.IsNullOrWhiteSpace(color)) productAttributes["Renk"] = color;
                    if (!string.IsNullOrWhiteSpace(material)) productAttributes["Malzeme"] = material;
                }

                string? categoryName = null, parentCategoryName = null, rootCategoryName = null;
                if (!string.IsNullOrWhiteSpace(productType))
                {
                    var parts = productType.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        rootCategoryName = parts[0];
                        parentCategoryName = parts[1];
                        categoryName = parts[^1];
                    }
                    else if (parts.Length == 2)
                    {
                        parentCategoryName = parts[0];
                        categoryName = parts[1];
                    }
                    else if (parts.Length == 1)
                    {
                        categoryName = parts[0];
                    }
                }
                if (categoryName is null && p.TryGetProperty("categories", out var cats) &&
                    cats.ValueKind == JsonValueKind.Array && cats.GetArrayLength() > 0)
                {
                    rootCategoryName = cats[0].TryGetProperty("name", out var catName) ? catName.GetString() : null;
                }

                // pk bulunamazsa yedek olarak code'un hash'i yerine 0 birakiyoruz - istersen code'u ayri bir kolonda da tutabilirsin
                results.Add(new ProductDto
                {
                    ExternalId = externalId,
                    Title = title,
                    ImageUrl = imageUrl,
                    Price = price,
                    RegularPrice = regularPrice,
                    ProductUrl = productUrl,
                    Brand = brand,
                    CategoryName = categoryName,
                    ParentCategoryName = parentCategoryName,
                    RootCategoryName = rootCategoryName,
                    Attributes = productAttributes
                });
            }

            var totalPages = productsEl.TryGetProperty("totalPages", out var totalPagesEl) ? totalPagesEl.GetInt32() : 1;
            if (dataArray.GetArrayLength() == 0 || pageIndex >= totalPages) break;
        }

        return results;
    }
}