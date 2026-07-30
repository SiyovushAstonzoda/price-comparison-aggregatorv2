using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
namespace PriceAggregator.Infrastructure.Scrapers.Cosmetics;

/// <summary>
/// Bir mağazanın kozmetik kategorisi için uygulaması gereken arayüz.
/// Her mağaza scraper'ı hem dinamik alt kategorileri hem de o kategorinin ürünlerini çeker.
/// </summary>
public interface ICosmeticsStoreScraper
{
    /// <summary>Mağazanın tanımlayıcı adı (örn: "migros", "macrocenter")</summary>
    string Source { get; }

    /// <summary>
    /// Mağazanın kategori ağacını tarayarak kozmetik/kişisel bakım
    /// dalını bulur ve alt kategori listesini döndürür.
    /// </summary>
    Task<List<CosmeticSubCategory>> FetchSubCategoriesAsync();

    /// <summary>
    /// Verilen alt kategoriye ait ürünleri çeker.
    /// Scraper'lar, DisplayName'i arama terimi olarak kullanır
    /// (örn: "Cilt Bakımı", "Saç Bakımı").
    /// </summary>
    Task<List<ProductDto>> FetchProductsByCategoryAsync(CosmeticSubCategory subCategory);
}

/// <summary>Bir mağazadan dinamik olarak çekilen kozmetik alt kategorisi.</summary>
public record CosmeticSubCategory(
    string Slug,         // normalize edilmiş (örn: "cilt-bakimi")
    string DisplayName,  // kullanıcıya gösterilen ad (örn: "Cilt Bakımı")
    string ExternalSlug  // mağazanın kendi kategori yolu/ID'si
);
