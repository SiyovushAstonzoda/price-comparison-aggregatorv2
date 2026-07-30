using System.Text.Json;

namespace PriceAggregator.Core;

// IKEA arama API yanitini ortak ProductDto modeline donusturur.
public class IkeaScraper
{
    private const string StorefrontUrl = "https://www.ikea.com.tr";
    private readonly HttpClient _httpClient;

    // IKEA API istekleri icin gerekli HTTP basliklarini ayarlar.
    public IkeaScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Remove("Accept");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Remove("Accept-Language");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9");
        _httpClient.DefaultRequestHeaders.Remove("Referer");
        _httpClient.DefaultRequestHeaders.Add("Referer", StorefrontUrl + "/");
    }

    // Arama kelimesine gore IKEA urunlerini ceker ve satilabilir urunleri DTO listesine donusturur.
    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var url = $"https://frontendapi.ikea.com.tr/api/search/products?Keyword={Uri.EscapeDataString(searchTerm)}&language=tr&IncludeFilters=false&StoreCode=331&sortby=None&page=1&size=24&SearchIn=product&IncludeColorVariants=true";
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);

        if (!json.RootElement.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
            return new List<ProductDto>();

        return products.EnumerateArray()
            .Where(product => !product.TryGetProperty("isSellable", out var sellable) || sellable.ValueKind != JsonValueKind.False)
            .Select(ParseProduct)
            .Where(product => product is not null)
            .Cast<ProductDto>()
            .ToList();
    }

    // IKEA JSON urununu SellerProducts ve Categories akisi icin gerekli alanlarla doldurur.
    private static ProductDto? ParseProduct(JsonElement product)
    {
        if (!TryGetInt64(product, "id", out var externalId)) return null;

        var title = GetString(product, "title");
        var subTitle = GetString(product, "subTitle");
        var color = GetString(product, "unitColorName");
        var productName = string.Join(" ", new[] { title, subTitle, color }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(productName)) return null;

        var categoryPath = GetCategoryPath(product);
        var price = GetDecimal(product, "price");

        return new ProductDto
        {
            ExternalId = externalId,
            Title = productName,
            Brand = "IKEA",
            ImageUrl = GetPrimaryImageUrl(product),
            Price = price,
            RegularPrice = GetDecimal(product, "regularPrice", price),
            ProductUrl = ToAbsoluteUrl(GetString(product, "url")),
            SourceCategory = categoryPath.LastOrDefault(),
            CategoryPath = categoryPath
        };
    }

    // Navigations icindeki en uzun breadcrumb yolunu kokten yapraga kategori agaci olarak kullanir.
    private static IReadOnlyList<string> GetCategoryPath(JsonElement product)
    {
        if (!product.TryGetProperty("navigations", out var navigations) || navigations.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var paths = new List<List<string>>();
        foreach (var navigation in navigations.EnumerateArray())
        {
            if (!navigation.TryGetProperty("breadcrumb", out var breadcrumb) || breadcrumb.ValueKind != JsonValueKind.Array)
                continue;

            var path = breadcrumb.EnumerateArray()
                .OrderBy(item => GetDecimal(item, "rank"))
                .Select(item => GetString(item, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .ToList();
            if (path.Count > 0) paths.Add(path);
        }

        return paths.OrderByDescending(path => path.Count).FirstOrDefault() ?? new List<string>();
    }

    // Ilk siradaki IKEA urun gorselini alir.
    private static string? GetPrimaryImageUrl(JsonElement product)
    {
        if (!product.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            return null;

        return images.EnumerateArray()
            .OrderBy(image => GetDecimal(image, "rank"))
            .Select(image => GetString(image, "image"))
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
    }

    // JSON sayi veya metin alanini uzun tamsayi olarak okur.
    private static bool TryGetInt64(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property)) return false;
        return property.ValueKind == JsonValueKind.Number ? property.TryGetInt64(out value)
            : property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out value);
    }

    // JSON fiyat alanini ondalik sayi olarak okur; alan yoksa varsayilan degeri doner.
    private static decimal GetDecimal(JsonElement element, string propertyName, decimal fallback = 0)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDecimal() : fallback;
    }

    // JSON metin alanini guvenli bicimde okur.
    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;
    }

    // Goreli IKEA urun yolunu tam magazaya ait URL haline getirir.
    private static string ToAbsoluteUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return StorefrontUrl;
        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : $"{StorefrontUrl}/{path.TrimStart('/')}";
    }
}