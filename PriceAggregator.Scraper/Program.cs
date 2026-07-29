using PriceAggregator.Scraper;
using PriceAggregator.Scraper.Cosmetics;

var connectionString = "Server=localhost,1433;Database=Tutumlu;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;";

// ── Kategori argümanını oku ──────────────────────────────────────────────────
// Kullanım:
//   dotnet run                            → varsayılan genel scrape (su araması)
//   dotnet run -- --category kozmetik     → kozmetik kategorisi scrape'i
// ─────────────────────────────────────────────────────────────────────────────
var category = args
    .SkipWhile(a => a != "--category")
    .Skip(1)
    .FirstOrDefault();

if (category?.ToLowerInvariant() == "kozmetik")
{
    // ── Kozmetik modu ────────────────────────────────────────────────────────
    Console.WriteLine("Mod: Kozmetik Kategorisi");

    var categoryRepo     = new CategoryRepository(connectionString);
    var matchingService  = new MatchingService(connectionString);

    var scrapers = new List<ICosmeticsStoreScraper>
    {
        new MigrosCosmeticsScraper(new HttpClient()),
        new MacroCenterCosmeticsScraper(new HttpClient()),
        new MionCosmeticsScraper(new HttpClient()),
    };

    var runner = new CosmeticsScraperRunner(scrapers, categoryRepo, matchingService);
    await runner.RunAsync();
}
else
{
    // ── Varsayılan genel arama modu (mevcut davranış korunuyor) ──────────────
    Console.WriteLine("Mod: Genel Arama");

    var httpClient1 = new HttpClient();
    var migrosScraper = new MigrosScraper(httpClient1);

    var httpClient2 = new HttpClient();
    var macroCenterScraper = new MacroCenterScraper(httpClient2);

    var httpClient3 = new HttpClient();
    var marketFiyatiScraper = new MarketFiyatiScraper(httpClient3);

    var repo = new ProductRepository(connectionString);
    var matchingService = new MatchingService(connectionString);

    string searchItem = "su";

    var migrosProducts = await migrosScraper.FetchProductsAsync(searchItem);
    foreach (var product in migrosProducts)
    {
        var savedId = await repo.SaveAsync("migros", product);
        await matchingService.MatchProductAsync(savedId, product.Brand, product.Title);
    }

    var macroProducts = await macroCenterScraper.FetchProductsAsync(searchItem);
    foreach (var product in macroProducts)
    {
        var savedId = await repo.SaveAsync("macrocenter", product);
        await matchingService.MatchProductAsync(savedId, product.Brand, product.Title);
    }

    var marketProducts = await marketFiyatiScraper.FetchProductsAsync(searchItem);
    foreach (var product in marketProducts)
    {
        var savedId = await repo.SaveAsync("marketfiyati", product);
        await matchingService.MatchProductAsync(savedId, product.Brand, product.Title);
    }

    Console.WriteLine(
        $"Saved {migrosProducts.Count} Migros + " +
        $"{macroProducts.Count} Macrocenter + " +
        $"{marketProducts.Count} MarketFiyati products.");
}