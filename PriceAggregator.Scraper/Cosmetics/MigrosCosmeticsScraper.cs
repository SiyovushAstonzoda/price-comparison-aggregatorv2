using System.Text.Json;

namespace PriceAggregator.Scraper.Cosmetics;

/// <summary>
/// Migros kozmetik scraper'ı.
/// Önce gerçek kategori endpoint'ini dener (ekranAdi), başarısız olursa
/// keyword aramasına düşer.
/// Pagination ile tüm sayfalar taranır.
/// </summary>
public class MigrosCosmeticsScraper : ICosmeticsStoreScraper
{
    private readonly HttpClient _httpClient;
    private const int PageSize = 48;
    private const int MaxPages = 30;

    public string Source => "migros";

    public MigrosCosmeticsScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        SetHeaders("https://www.migros.com.tr/");
    }

    private void SetHeaders(string referer)
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", referer);
    }

    public async Task<List<CosmeticSubCategory>> FetchSubCategoriesAsync()
    {
        // Sabit ve güvenilir fallback — kategori API'si çalışmıyor
        return GetFallbackSubCategories();
    }

    public async Task<List<ProductDto>> FetchProductsByCategoryAsync(CosmeticSubCategory subCategory)
    {
        Console.Write($"\n     [ekranAdi] deneniyor...");
        var byCategory = await FetchByEkranAdiAsync(subCategory.ExternalSlug, subCat: subCategory);

        if (byCategory.Count > 0)
        {
            Console.Write($" ✓ kategori modu ({byCategory.Count} ürün)");
            return byCategory;
        }

        // Fallback: keyword araması (kategori filtresi ile)
        Console.Write($" ✗ → arama moduna geçildi (akıllı filtre devrede)");
        return await FetchByQueryAsync(subCategory);
    }

    // ── Gerçek kategori endpoint'i ────────────────────────────────────────
    private async Task<List<ProductDto>> FetchByEkranAdiAsync(string ekranAdi, CosmeticSubCategory subCat)
    {
        var allResults = new List<ProductDto>();

        for (int page = 1; page <= MaxPages; page++)
        {
            try
            {
                var url = "https://www.migros.com.tr/rest/products/search" +
                          $"?ekranAdi={Uri.EscapeDataString(ekranAdi)}" +
                          $"&sayfa={page}" +
                          $"&urunSayisi={PageSize}" +
                          $"&sirala=akilli-siralama";

                var response = await _httpClient.GetStringAsync(url);
                using var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("data", out var data)) break;
                if (!data.TryGetProperty("storeProductInfos", out var products)) break;

                var pageItems = ParseProducts(products, "https://www.migros.com.tr/", subCat);
                allResults.AddRange(pageItems);
                Console.Write($" [s{page}:{pageItems.Count}]");

                if (pageItems.Count < PageSize) break;
                if (CheckTotalReached(data, allResults.Count)) break;
                await Task.Delay(150);
            }
            catch { break; }
        }
        return allResults;
    }

    // ── Arama bazlı fallback ──────────────────────────────────────────────
    private async Task<List<ProductDto>> FetchByQueryAsync(CosmeticSubCategory subCat)
    {
        var allResults = new List<ProductDto>();
        var searchTerm = Uri.EscapeDataString(subCat.DisplayName);

        for (int page = 1; page <= MaxPages; page++)
        {
            try
            {
                var url = "https://www.migros.com.tr/rest/products/search" +
                          $"?query={searchTerm}" +
                          $"&sayfa={page}" +
                          $"&urunSayisi={PageSize}" +
                          $"&sirala=akilli-siralama";

                var response = await _httpClient.GetStringAsync(url);
                using var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("data", out var data)) break;
                if (!data.TryGetProperty("storeProductInfos", out var products)) break;

                var pageItems = ParseProducts(products, "https://www.migros.com.tr/", subCat);
                allResults.AddRange(pageItems);
                Console.Write($" [s{page}:{pageItems.Count}]");

                if (pageItems.Count < PageSize) break;
                if (CheckTotalReached(data, allResults.Count)) break;
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  [Migros] Hata: {ex.Message}");
                break;
            }
        }
        return allResults;
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────

    private static List<ProductDto> ParseProducts(JsonElement products, string baseUrl, CosmeticSubCategory subCat)
    {
        var list = new List<ProductDto>();
        
        // Hedef kategoriden kilit kelimeleri çıkar (örn: "el-ayak-bakimi" -> ["el", "ayak"])
        var expectedWords = subCat.Slug.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Where(w => w != "bakimi" && w != "ve" && w != "urunleri")
                                       .ToList();

        foreach (var p in products.EnumerateArray())
        {
            try
            {
                // AKILLI FİLTRE: Ürünün Migros'taki üst kategorisi hedef kelimeleri içeriyor mu?
                bool isCosmetic = false;
                bool isMatch = expectedWords.Count == 0; // Eğer kelime kalmadıysa mecbur eşleşti sayarız

                if (p.TryGetProperty("categoryAscendants", out var ascendants))
                {
                    foreach (var asc in ascendants.EnumerateArray())
                    {
                        var ascName = asc.TryGetProperty("name", out var n) ? n.GetString()?.ToLowerInvariant() : "";
                        var ascPretty = asc.TryGetProperty("prettyName", out var pn) ? pn.GetString()?.ToLowerInvariant() : "";
                        
                        if ((ascName != null && (ascName.Contains("kozmetik") || ascName.Contains("kisisel") || ascName.Contains("kişisel"))) ||
                            (ascPretty != null && (ascPretty.Contains("kozmetik") || ascPretty.Contains("kisisel"))))
                        {
                            isCosmetic = true;
                        }

                        // Alt kategori kelimelerinden HERHANGİ BİRİ ürünün ağacında var mı?
                        if (!isMatch && (ascName != null || ascPretty != null))
                        {
                            if (expectedWords.Any(w => (ascName != null && ascName.Contains(w)) || (ascPretty != null && ascPretty.Contains(w))))
                            {
                                isMatch = true;
                            }
                        }
                    }
                }
                
                // Fallback olarak ana 'category' objesine de bak
                if (p.TryGetProperty("category", out var cat))
                {
                    var catName = cat.TryGetProperty("name", out var n) ? n.GetString()?.ToLowerInvariant() : "";
                    if (catName != null && (catName.Contains("kozmetik") || catName.Contains("kisisel") || catName.Contains("kişisel")))
                    {
                        isCosmetic = true;
                    }
                    if (!isMatch && catName != null && expectedWords.Any(w => catName.Contains(w)))
                    {
                        isMatch = true;
                    }
                }

                // Eğer kozmetik değilse VEYA ürünün orijinal kategorisinde hedef kelime (el/ayak vs) YOKSA atla!
                if (!isCosmetic || !isMatch) continue;


                string? imageUrl = null;
                if (p.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
                    imageUrl = imgs[0].GetProperty("urls").GetProperty("PRODUCT_DETAIL").GetString();

                string? brand = null;
                if (p.TryGetProperty("brand", out var b))
                    brand = b.TryGetProperty("name", out var bn) ? bn.GetString() : null;

                list.Add(new ProductDto
                {
                    ExternalId   = p.GetProperty("id").GetInt64(),
                    Title        = p.GetProperty("name").GetString() ?? "",
                    Brand        = brand ?? "",
                    ImageUrl     = imageUrl,
                    Price        = p.GetProperty("shownPrice").GetInt32() / 100m,
                    RegularPrice = p.GetProperty("regularPrice").GetInt32() / 100m,
                    ProductUrl   = baseUrl + p.GetProperty("prettyName").GetString()
                });
            }
            catch { /* malformed product, skip */ }
        }
        return list;
    }

    private static bool CheckTotalReached(JsonElement data, int fetched)
    {
        if (data.TryGetProperty("pagination", out var pag) &&
            pag.TryGetProperty("totalItemCount", out var t))
            return fetched >= t.GetInt32();
        return false;
    }

    private static List<CosmeticSubCategory> GetFallbackSubCategories() => new()
    {
        new("cilt-bakimi",     "Cilt Bakımı",      "kisisel-bakim/cilt-bakimi"),
        new("sac-bakimi",      "Saç Bakımı",       "kisisel-bakim/sac-bakimi"),
        new("vucut-bakimi",    "Vücut Bakımı",     "kisisel-bakim/vucut-bakimi"),
        new("parfum",          "Parfüm",           "kisisel-bakim/parfum"),
        new("agiz-bakimi",     "Ağız Bakımı",      "kisisel-bakim/agiz-bakimi"),
        new("makyaj",          "Makyaj",           "kisisel-bakim/makyaj"),
        new("el-ayak-bakimi",  "El & Ayak Bakımı", "kisisel-bakim/el-ayak-bakimi"),
    };
}
