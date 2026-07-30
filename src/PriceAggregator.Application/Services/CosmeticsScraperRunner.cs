using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using PriceAggregator.Infrastructure.Repositories;
using PriceAggregator.Infrastructure.Scrapers.Cosmetics;

namespace PriceAggregator.Application.Services;

/// <summary>
/// Kozmetik scraper orkestratörü.
/// - 3 mağaza (Migros, MacroCenter, Mion) paralel çalışır.
/// - Her mağazada alt kategoriler eş zamanlı taranır (max 3 paralel).
/// - Sayfalama otomatik — her alt kategori için son sayfaya kadar devam eder.
/// </summary>
public class CosmeticsScraperRunner
{
    private readonly IReadOnlyList<ICosmeticsStoreScraper> _scrapers;
    private readonly CategoryRepository _categoryRepo;
    private readonly ProductRepository _productRepo;
    private readonly MatchingService _matchingService;

    private const int SubCategoryParallelism = 3;

    public CosmeticsScraperRunner(
        IReadOnlyList<ICosmeticsStoreScraper> scrapers,
        CategoryRepository categoryRepo,
        ProductRepository productRepo,
        MatchingService matchingService)
    {
        _scrapers        = scrapers;
        _categoryRepo    = categoryRepo;
        _productRepo     = productRepo;
        _matchingService = matchingService;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  Kozmetik Scraper Başlatıldı  (Paralel Mod)");
        Console.WriteLine("═══════════════════════════════════════════════════");

        int categoryId = await _categoryRepo.GetCosmeticsCategoryIdAsync();
        Console.WriteLine($"Kategori ID: {categoryId}\n");

        var scraperTasks = _scrapers.Select(s => RunScraperAsync(s, categoryId));
        var results = await Task.WhenAll(scraperTasks);

        int total = results.Sum();
        Console.WriteLine($"\n═══════════════════════════════════════════════════");
        Console.WriteLine($"  ✅ Tamamlandı — Toplam {total} ürün kaydedildi.");
        Console.WriteLine($"═══════════════════════════════════════════════════");
    }

    private async Task<int> RunScraperAsync(ICosmeticsStoreScraper scraper, int categoryId)
    {
        Console.WriteLine($"▶ [{scraper.Source.ToUpper()}] Alt kategoriler çekiliyor...");

        List<CosmeticSubCategory> subCategories;
        try
        {
            subCategories = await scraper.FetchSubCategoriesAsync();
            Console.WriteLine($"  {subCategories.Count} alt kategori bulundu:");
            foreach (var sc in subCategories)
                Console.WriteLine($"    • {sc.DisplayName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [HATA] Alt kategoriler alınamadı: {ex.Message}");
            return 0;
        }

        var semaphore = new SemaphoreSlim(SubCategoryParallelism);
        int totalProducts = 0;

        var tasks = subCategories.Select(async subCat =>
        {
            await semaphore.WaitAsync();
            try
            {
                Console.Write($"\n  ↳ [{scraper.Source.ToUpper()} / {subCat.DisplayName}] çekiliyor...");

                int subCategoryId = await _categoryRepo.UpsertSubCategoryAsync(
                    categoryId, scraper.Source, subCat);

                var products = await scraper.FetchProductsByCategoryAsync(subCat);

                int saved = 0;
                foreach (var product in products)
                {
                    try
                    {
                        int? sellerProductId = await _productRepo.SaveAsync(scraper.Source, product);
                        if (sellerProductId.HasValue)
                        {
                            await _matchingService.MatchProductAsync(
                                sellerProductId.Value, product.Brand, product.Title, subCat.DisplayName, product.SourceCategory);
                            saved++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n    [UYARI] '{product.Title}': {ex.Message}");
                    }
                }

                Interlocked.Add(ref totalProducts, saved);
                return saved;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        Console.WriteLine($"\n  [{scraper.Source.ToUpper()}] toplam {totalProducts} ürün kaydedildi.");
        return totalProducts;
    }
}
