using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PriceAggregator.Core.Scrapers;

public class MigrosScraper : IProductScraper
{
    private readonly HttpClient _httpClient;

    public string Name => "migros";
    public ScraperSector Sector => ScraperSector.Market;

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

        var (categoryName, parentName, rootName) = ParseCategory(p);
        var (unitType, unitAmount, sourceUnitPrice) = ParseUnitInfo(p);

        return new ProductDto
        {
            ExternalId = p.GetProperty("id").GetInt64(),
            Title = p.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Brand = brand,
            ImageUrl = imageUrl,
            Price = p.TryGetProperty("shownPrice", out var price) ? price.GetInt32() / 100m : 0,
            RegularPrice = p.TryGetProperty("regularPrice", out var regPrice) ? regPrice.GetInt32() / 100m : 0,
            ProductUrl = p.TryGetProperty("prettyName", out var pretty)
                ? $"https://www.migros.com.tr/{pretty.GetString()}"
                : "",
            CategoryName = categoryName,
            ParentCategoryName = parentName,
            RootCategoryName = rootName,
            SourceUnitType = unitType,
            SourceUnitAmount = unitAmount,
            SourceUnitPrice = sourceUnitPrice
        };
    }

    // Migros/MacroCenter share this JSON shape: "category" is the leaf category,
    // "categoryAscendants" lists ancestors nearest-first (last element is the root).
    internal static (string? Name, string? ParentName, string? RootName) ParseCategory(JsonElement p)
    {
        string? categoryName = p.TryGetProperty("category", out var categoryEl) && categoryEl.TryGetProperty("name", out var cn)
            ? cn.GetString()
            : null;

        string? parentName = null, rootName = null;
        if (p.TryGetProperty("categoryAscendants", out var ascendants) && ascendants.ValueKind == JsonValueKind.Array)
        {
            var items = ascendants.EnumerateArray().ToList();
            if (items.Count == 1)
            {
                rootName = items[0].TryGetProperty("name", out var rn) ? rn.GetString() : null;
            }
            else if (items.Count > 1)
            {
                parentName = items[0].TryGetProperty("name", out var pn) ? pn.GetString() : null;
                rootName = items[^1].TryGetProperty("name", out var rn2) ? rn2.GetString() : null;
            }
        }

        return (categoryName, parentName, rootName);
    }

    // Migros/MacroCenter/Mion run on the same commerce platform, so their product JSON
    // carries the same "unit"/"unitAmount" fields (e.g. GRAM/1000) and, for many items, a
    // ready-made legally-required "(2499,50 TL/Kg)" unitPrice label — structured size data
    // that's more reliable than guessing from the free-text title (see SizeParser).
    internal static (string? UnitType, decimal? UnitAmount, decimal? SourceUnitPrice) ParseUnitInfo(JsonElement p)
    {
        string? unitType = p.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String
            ? u.GetString()
            : null;

        decimal? unitAmount = p.TryGetProperty("unitAmount", out var ua) && ua.TryGetDecimal(out var uav)
            ? uav
            : null;

        decimal? sourceUnitPrice = null;
        if (p.TryGetProperty("unitPrice", out var up) && up.ValueKind == JsonValueKind.String)
        {
            var match = Regex.Match(up.GetString() ?? "", @"[\d.,]+");
            if (match.Success)
            {
                var numeric = match.Value.Replace(".", "").Replace(",", ".");
                if (decimal.TryParse(numeric, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    sourceUnitPrice = value;
            }
        }

        return (unitType, unitAmount, sourceUnitPrice);
    }
}