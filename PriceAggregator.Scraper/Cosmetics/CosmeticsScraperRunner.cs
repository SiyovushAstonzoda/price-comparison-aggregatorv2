namespace PriceAggregator.Scraper.Cosmetics;

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
    private readonly MatchingService _matchingService;

    private const int SubCategoryParallelism = 3;

    public CosmeticsScraperRunner(
        IReadOnlyList<ICosmeticsStoreScraper> scrapers,
        CategoryRepository categoryRepo,
        MatchingService matchingService)
    {
        _scrapers        = scrapers;
        _categoryRepo    = categoryRepo;
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
                        int productId = await _categoryRepo.SaveProductAsync(
                            scraper.Source, subCategoryId, product);
                        await _matchingService.MatchProductAsync(
                            productId, product.Brand, product.Title);
                        saved++;
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
