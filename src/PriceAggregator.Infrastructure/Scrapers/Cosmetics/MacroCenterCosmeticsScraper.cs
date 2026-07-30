using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using System.Text.Json;



namespace PriceAggregator.Infrastructure.Scrapers.Cosmetics;

/// <summary>
/// MacroCenter kozmetik scraper'ı.
/// Önce gerçek kategori endpoint'ini dener (ekranAdi), başarısız olursa
/// keyword aramasına düşer.
/// Pagination ile tüm sayfalar taranır.
/// </summary>
public class MacroCenterCosmeticsScraper : ICosmeticsStoreScraper
{
    private readonly HttpClient _httpClient;
    private const int PageSize = 48;
    private const int MaxPages = 40;

    public string Source => "macrocenter";

    public MacroCenterCosmeticsScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.macrocenter.com.tr/");
    }

    public async Task<List<CosmeticSubCategory>> FetchSubCategoriesAsync()
    {
        return GetFallbackSubCategories();
    }

    /// <inheritdoc/>
    public async Task<List<ProductDto>> FetchProductsByCategoryAsync(CosmeticSubCategory subCategory)
    {
        Console.Write($"\n     [ekranAdi] deneniyor...");
        var byCategory = await FetchByEkranAdiAsync(subCategory.ExternalSlug, subCategory);

        if (byCategory.Count > 0)
        {
            Console.Write($" ✓ kategori modu ({byCategory.Count} ürün)");
            return byCategory;
        }

        Console.Write($" ✗ → arama moduna geçildi (akıllı filtre devrede)");
        return await FetchByQueryAsync(subCategory);
    }

    // ── Gerçek kategori endpoint'i ────────────────────────────────────────
    private async Task<List<ProductDto>> FetchByEkranAdiAsync(string ekranAdi, CosmeticSubCategory subCat)
    {
        var allResults = new List<ProductDto>();

        for (int page = 0; page < MaxPages; page++)
        {
            try
            {
                var url = "https://www.macrocenter.com.tr/rest/products/search" +
                          $"?ekranAdi={Uri.EscapeDataString(ekranAdi)}" +
                          $"&page-size={PageSize}" +
                          $"&page={page}";

                var response = await _httpClient.GetStringAsync(url);
                using var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("data", out var data)) break;
                if (!data.TryGetProperty("storeProductInfos", out var products)) break;

                var pageItems = ParseProducts(products, subCat);
                allResults.AddRange(pageItems);
                Console.Write($" [s{page + 1}:{pageItems.Count}]");

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

        for (int page = 0; page < MaxPages; page++)
        {
            try
            {
                var url = "https://www.macrocenter.com.tr/rest/products/search" +
                          $"?q={searchTerm}" +
                          $"&page-size={PageSize}" +
                          $"&page={page}";

                var response = await _httpClient.GetStringAsync(url);
                using var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("data", out var data)) break;
                if (!data.TryGetProperty("storeProductInfos", out var products)) break;

                var pageItems = ParseProducts(products, subCat);
                allResults.AddRange(pageItems);
                Console.Write($" [s{page + 1}:{pageItems.Count}]");

                if (pageItems.Count < PageSize) break;
                if (CheckTotalReached(data, allResults.Count)) break;
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  [MacroCenter] Hata: {ex.Message}");
                break;
            }
        }
        return allResults;
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────

    private static List<ProductDto> ParseProducts(JsonElement products, CosmeticSubCategory subCat)
    {
        var list = new List<ProductDto>();

        // Hedef kategoriden kilit kelimeleri çıkar
        var expectedWords = subCat.Slug.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Where(w => w != "bakimi" && w != "ve" && w != "urunleri")
                                       .ToList();

        foreach (var p in products.EnumerateArray())
        {
            try
            {
                // AKILLI FİLTRE: Ürünün üst kategorisi hedef kelimeleri içeriyor mu?
                bool isCosmetic = false;
                bool isMatch = expectedWords.Count == 0;

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

                if (!isCosmetic || !isMatch) continue;


                string? imageUrl = null;
                if (p.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
                    imageUrl = imgs[0].GetProperty("urls").GetProperty("PRODUCT_DETAIL").GetString();

                string? brand = null;
                if (p.TryGetProperty("brand", out var b))
                    brand = b.TryGetProperty("name", out var bn) ? bn.GetString() : null;

                list.Add(new ProductDto
                {
                    ExternalId = p.GetProperty("id").GetInt64().ToString(),
                    Title        = p.GetProperty("name").GetString() ?? "",
                    Brand        = brand ?? "",
                    ImageUrl     = imageUrl,
                    Price        = p.GetProperty("shownPrice").GetInt32() / 100m,
                    RegularPrice = p.GetProperty("regularPrice").GetInt32() / 100m,
                    ProductUrl   = "https://www.macrocenter.com.tr/" + p.GetProperty("prettyName").GetString()
                });
            }
            catch { }
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
    };
}
