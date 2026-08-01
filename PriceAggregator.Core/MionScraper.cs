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

    private const int MaxPages = 3;

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var encodedSearch = Uri.EscapeDataString(searchTerm);
        var results = new List<ProductDto>();

        for (int page = 1; page <= MaxPages; page++)
        {
            var url =
                $"https://www.migros.com.tr/rest/mion/search/screens/products?q={encodedSearch}&page={page}";

            var response = await _httpClient.GetStringAsync(url);

            using var json = JsonDocument.Parse(response);

            var searchInfo = json.RootElement
                .GetProperty("data")
                .GetProperty("searchInfo");
            var products = searchInfo.GetProperty("storeProductInfos");

            if (products.GetArrayLength() == 0) break;

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

                string brand = p.TryGetProperty("brand", out var brandEl) && brandEl.ValueKind == JsonValueKind.Object
                    ? brandEl.GetProperty("name").GetString() ?? ""
                    : "";

                // Mion runs on the same Migros commerce platform, so its product JSON has the
                // identical category/categoryAscendants shape (see MacroCenterScraper, which
                // reuses this same parser for the same reason).
                var (categoryName, parentName, rootName) = MigrosScraper.ParseCategory(p);
                var (unitType, unitAmount, sourceUnitPrice) = MigrosScraper.ParseUnitInfo(p);

                results.Add(new ProductDto
                {
                    ExternalId = p.GetProperty("id").GetInt64(),

                    Title = p.GetProperty("name").GetString() ?? "",

                    ImageUrl = imageUrl,

                    Price = p.GetProperty("shownPrice").GetInt32() / 100m,

                    RegularPrice = p.GetProperty("regularPrice").GetInt32() / 100m,

                    ProductUrl =
                        $"https://www.mion.com.tr/urun/{p.GetProperty("prettyName").GetString()}",

                    Brand = brand,
                    CategoryName = categoryName,
                    ParentCategoryName = parentName,
                    RootCategoryName = rootName,
                    SourceUnitType = unitType,
                    SourceUnitAmount = unitAmount,
                    SourceUnitPrice = sourceUnitPrice
                });
            }

            var pageCount = searchInfo.TryGetProperty("pageCount", out var pageCountEl) ? pageCountEl.GetInt32() : 1;
            if (page >= pageCount) break;
        }

        return results;
    }
}