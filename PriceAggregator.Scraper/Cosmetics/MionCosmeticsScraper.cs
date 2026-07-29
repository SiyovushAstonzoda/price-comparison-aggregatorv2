using System.Text.Json;

namespace PriceAggregator.Scraper.Cosmetics;

/// <summary>
/// Mion'un (Migros online) API'sini kullanarak kozmetik
/// alt kategorilerini ve ürünlerini dinamik olarak çeker.
/// Pagination ile tüm sayfalar taranır.
/// </summary>
public class MionCosmeticsScraper : ICosmeticsStoreScraper
{
    private readonly HttpClient _httpClient;
    private const int PageSize = 48;
    private const int MaxPages = 20;   // 20 × 48 = 960 ürün/alt kategori

    public string Source => "mion";

    public MionCosmeticsScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.mion.com.tr/");
    }

    /// <inheritdoc/>
    public async Task<List<CosmeticSubCategory>> FetchSubCategoriesAsync()
    {
        var results = new List<CosmeticSubCategory>();
        try
        {
            var url = "https://www.migros.com.tr/rest/mion/categories";
            var response = await _httpClient.GetStringAsync(url);
            using var json = JsonDocument.Parse(response);

            var cosmeticsNode = FindCosmeticsNode(json.RootElement);
            if (cosmeticsNode is null) return GetFallbackSubCategories();

            if (cosmeticsNode.Value.TryGetProperty("subCategories", out var subs)
                || cosmeticsNode.Value.TryGetProperty("children", out subs))
            {
                foreach (var child in subs.EnumerateArray())
                {
                    var name = GetString(child, "name", "displayName");
                    var slug = GetString(child, "slug", "prettyName", "url");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    results.Add(new CosmeticSubCategory(
                        Slug: NormalizeSlug(name),
                        DisplayName: name,
                        ExternalSlug: slug ?? NormalizeSlug(name)));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MionCosmeticsScraper] Kategori çekme hatası: {ex.Message}");
            return GetFallbackSubCategories();
        }

        return results.Count > 0 ? results : GetFallbackSubCategories();
    }

    /// <inheritdoc/>
    /// Tüm sayfaları döngüyle çeker — ürün kaçmaz.
    public async Task<List<ProductDto>> FetchProductsByCategoryAsync(CosmeticSubCategory subCategory)
    {
        var allResults = new List<ProductDto>();
        var encoded = Uri.EscapeDataString(subCategory.DisplayName);

        for (int page = 1; page <= MaxPages; page++)
        {
            try
            {
                var url = $"https://www.migros.com.tr/rest/mion/search/screens/products" +
                          $"?q={encoded}" +
                          $"&page={page}" +
                          $"&pageSize={PageSize}";

                var response = await _httpClient.GetStringAsync(url);
                using var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("data", out var data)) break;
                if (!data.TryGetProperty("searchInfo", out var searchInfo)) break;
                if (!searchInfo.TryGetProperty("storeProductInfos", out var products)) break;

                var pageItems = new List<ProductDto>();

                var expectedWords = subCategory.Slug.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Where(w => w != "bakimi" && w != "ve" && w != "urunleri")
                                       .ToList();

                foreach (var p in products.EnumerateArray())
                {
                    try
                    {
                        bool isMatch = expectedWords.Count == 0;

                        if (p.TryGetProperty("categoryAscendants", out var ascendants))
                        {
                            foreach (var asc in ascendants.EnumerateArray())
                            {
                                var ascName = asc.TryGetProperty("name", out var n) ? n.GetString()?.ToLowerInvariant() : "";
                                var ascPretty = asc.TryGetProperty("prettyName", out var pn) ? pn.GetString()?.ToLowerInvariant() : "";
                                
                                if (!isMatch && (ascName != null || ascPretty != null))
                                {
                                    if (expectedWords.Any(w => (ascName != null && ascName.Contains(w)) || (ascPretty != null && ascPretty.Contains(w))))
                                    {
                                        isMatch = true;
                                    }
                                }
                            }
                        }
                        
                        if (p.TryGetProperty("category", out var cat))
                        {
                            var catName = cat.TryGetProperty("name", out var n) ? n.GetString()?.ToLowerInvariant() : "";
                            if (!isMatch && catName != null && expectedWords.Any(w => catName.Contains(w)))
                            {
                                isMatch = true;
                            }
                        }

                        if (!isMatch) continue;

                        string? imageUrl = null;
                        if (p.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                            imageUrl = images[0].GetProperty("urls").GetProperty("PRODUCT_DETAIL").GetString();

                        pageItems.Add(new ProductDto
                        {
                            ExternalId   = p.GetProperty("id").GetInt64(),
                            Title        = p.GetProperty("name").GetString() ?? "",
                            ImageUrl     = imageUrl,
                            Price        = p.GetProperty("shownPrice").GetInt32() / 100m,
                            RegularPrice = p.GetProperty("regularPrice").GetInt32() / 100m,
                            ProductUrl   = $"https://www.mion.com.tr/urun/{p.GetProperty("prettyName").GetString()}"
                        });
                    }
                    catch { }
                }

                allResults.AddRange(pageItems);
                Console.Write($" [sayfa {page}: {pageItems.Count}]");

                // Son sayfadaysak dur
                if (pageItems.Count < PageSize) break;

                // Toplam sayıyı kontrol et (API dönüyorsa)
                if (searchInfo.TryGetProperty("totalItemCount", out var total) ||
                    data.TryGetProperty("totalItemCount", out total))
                {
                    int totalCount = total.GetInt32();
                    if (allResults.Count >= totalCount) break;
                }

                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[MionCosmeticsScraper] Sayfa {page} hatası ({subCategory.DisplayName}): {ex.Message}");
                break;
            }
        }

        return allResults;
    }

    // ── Yardımcı metodlar ──────────────────────────────────────────────────

    private static readonly string[] CosmeticsKeywords =
        { "kozmetik", "kisisel", "bakim", "personal", "beauty" };

    private static JsonElement? FindCosmeticsNode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
            {
                var found = FindCosmeticsNode(item);
                if (found.HasValue) return found;
            }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "name", "displayName", "slug", "prettyName" })
                if (element.TryGetProperty(key, out var val))
                {
                    var s = val.GetString()?.ToLowerInvariant() ?? "";
                    if (CosmeticsKeywords.Any(k => s.Contains(k))) return element;
                }
            foreach (var prop in element.EnumerateObject())
            {
                var found = FindCosmeticsNode(prop.Value);
                if (found.HasValue) return found;
            }
        }
        return null;
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
            if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    private static string NormalizeSlug(string name) =>
        name.ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "-").Replace("/", "-");

    private static List<CosmeticSubCategory> GetFallbackSubCategories() => new()
    {
        new("cilt-bakimi",  "Cilt Bakımı",  "cilt-bakimi"),
        new("sac-bakimi",   "Saç Bakımı",   "sac-bakimi"),
        new("vucut-bakimi", "Vücut Bakımı", "vucut-bakimi"),
        new("parfum",       "Parfüm",       "parfum"),
        new("makyaj",       "Makyaj",       "makyaj"),
    };
}
