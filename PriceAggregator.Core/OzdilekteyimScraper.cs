using System.Text.Json;

namespace PriceAggregator.Core;

public class OzdilekteyimScraper
{
    private const string BaseUrl = "https://api.ozdilekteyim.com/rest/v2/magaza-magaza-store/products/search";
    private const string StorefrontUrl = "https://www.ozdilekteyim.com/";

    private readonly HttpClient _httpClient;

    // Gerekli HTTP başlıklarını ayarlayarak HttpClient nesnesini ilklendirir.
    public OzdilekteyimScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(20);

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }

        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", StorefrontUrl);
    }

    // Belirtilen arama terimine göre tüm sayfalardaki ürünleri çeker ve DTO listesi olarak döner.
    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var results = new List<ProductDto>();

        try
        {
            // İlk sayfayı çekerek toplam sayfa sayısını öğreniyoruz.
            int totalPages = 1;
            int currentPage = 0;

            do
            {
                var url = $"{BaseUrl}?query={Uri.EscapeDataString(searchTerm)}&currentPage={currentPage}&pageSize=20";

                using var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                await using var responseStream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(responseStream);

                // Sayfalama bilgisini ilk sayfada okuyoruz.
                if (currentPage == 0 && json.RootElement.TryGetProperty("pagination", out var pagination))
                {
                    if (pagination.TryGetProperty("totalPages", out var totalPagesEl))
                        totalPages = totalPagesEl.GetInt32();
                }

                if (!json.RootElement.TryGetProperty("products", out var products) ||
                    products.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                foreach (var product in products.EnumerateArray())
                {
                    try
                    {
                        var dto = ParseProduct(product);
                        if (dto != null)
                            results.Add(dto);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[Ozdilek] Skipped one product due to parse error: {ex.Message}");
                    }
                }

                currentPage++;
            }
            while (currentPage < totalPages);
        }
        catch (HttpRequestException ex)
        {
            Logger.Log($"[Ozdilek] Network error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Logger.Log("[Ozdilek] Request timed out.");
        }
        catch (JsonException ex)
        {
            Logger.Log($"[Ozdilek] Failed to parse JSON: {ex.Message}");
        }

        return results;
    }

    // Tek bir ürün JSON elemanını ProductDto'ya dönüştürür.
    private static ProductDto? ParseProduct(JsonElement product)
    {
        // "code" alanı hem sayı hem de string olarak gelebilir.
        if (!TryGetInt64(product, "code", out var externalId))
            return null;

        var productPath = GetString(product, "customUrl");
        var currentPrice = GetPrice(product, "price");
        var regularPrice = GetPrice(product, "listPrice", currentPrice);
        var categoryPath = GetCategoryPath(product);

        return new ProductDto
        {
            ExternalId = externalId,
            Title = GetString(product, "name") ?? string.Empty,
            Brand = GetString(product, "brand") ?? string.Empty,
            ImageUrl = GetBestImageUrl(product),
            Price = currentPrice,
            RegularPrice = regularPrice,
            SourceCategory = categoryPath.LastOrDefault(),
            CategoryPath = categoryPath,
            ProductUrl = ToAbsoluteProductUrl(productPath)
        };
    }

    // Önce "product" formatında PRIMARY resmi, yoksa "thumbnail" PRIMARY, yoksa ilk resmi döner.
    private static string? GetBestImageUrl(JsonElement product)
    {
        if (!product.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            return null;

        string? thumbnailPrimary = null;
        string? firstUrl = null;

        foreach (var image in images.EnumerateArray())
        {
            var format = GetString(image, "format");
            var imageType = GetString(image, "imageType");
            var url = GetString(image, "url");

            if (firstUrl is null)
                firstUrl = url;

            if (imageType == "PRIMARY")
            {
                // "product" formatı (848x848) en yüksek kaliteli resimdir — onu tercih ediyoruz.
                if (format == "product")
                    return url;

                if (thumbnailPrimary is null)
                    thumbnailPrimary = url;
            }
        }

        return thumbnailPrimary ?? firstUrl;
    }

    // Kategorileri JSON'daki kökten yaprağa sıralı haliyle korur.
    private static IReadOnlyList<string> GetCategoryPath(JsonElement product)
    {
        if (!product.TryGetProperty("categories", out var categories) ||
            categories.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return categories.EnumerateArray()
            .Select(category => GetString(category, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToArray();
    }
    // Ürünün belirli bir fiyat alanını (price veya listPrice) decimal olarak okur.
    private static decimal GetPrice(JsonElement product, string propertyName, decimal fallback = 0)
    {
        return product.TryGetProperty(propertyName, out var price) &&
               price.ValueKind == JsonValueKind.Object &&
               price.TryGetProperty("value", out var value) &&
               value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : fallback;
    }

    // "code" gibi alanlar API'de bazen string bazen number olarak gelebilir.
    private static bool TryGetInt64(JsonElement product, string propertyName, out long value)
    {
        value = 0;

        if (!product.TryGetProperty(propertyName, out var property))
            return false;

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(property.GetString(), out value),
            _ => false
        };
    }

    // JSON elementinden string değer okur; alan yoksa veya string değilse null döner.
    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    // customUrl'i tam Ozdilek URL'sine dönüştürür.
    private static string ToAbsoluteProductUrl(string? productPath)
    {
        if (string.IsNullOrWhiteSpace(productPath))
            return StorefrontUrl.TrimEnd('/');

        return productPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? productPath
            : $"{StorefrontUrl}{productPath.TrimStart('/')}";
    }
}
