using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using System.Text.Json;



namespace PriceAggregator.Infrastructure.Scrapers.Market;

public class MacroCenterScraper
{
    private readonly HttpClient _httpClient;

    public MacroCenterScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.macrocenter.com.tr/");
    }

    public async Task<List<ProductDto>> FetchProductsAsync(string searchTerm)
    {
        var url = $"https://www.macrocenter.com.tr/rest/products/search?q={searchTerm}&page-size=30";
        var response = await _httpClient.GetStringAsync(url);
        var json = JsonDocument.Parse(response);

        var results = new List<ProductDto>();
        var products = json.RootElement.GetProperty("data").GetProperty("storeProductInfos");

        foreach (var p in products.EnumerateArray())
        {
            string? imageUrl = null;
            if (p.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                imageUrl = images[0].GetProperty("urls").GetProperty("PRODUCT_DETAIL").GetString();

            results.Add(new ProductDto
            {
                ExternalId = p.GetProperty("id").GetInt64().ToString(),
                Title = p.GetProperty("name").GetString() ?? "",
                Brand = p.TryGetProperty("brand", out var brand) ? brand.GetProperty("name").GetString() : null,
                ImageUrl = imageUrl,
                Price = p.GetProperty("shownPrice").GetInt32() / 100m,
                RegularPrice = p.GetProperty("regularPrice").GetInt32() / 100m,
                ProductUrl = $"https://www.macrocenter.com.tr/{p.GetProperty("prettyName").GetString()}"
            });
        }

        return results;
    }
}
