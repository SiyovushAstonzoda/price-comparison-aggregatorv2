using PriceAggregator.Scraper;

var httpClient1 = new HttpClient();
var migrosScraper = new MigrosScraper(httpClient1);

var httpClient2 = new HttpClient();
var macroCenterScraper = new MacroCenterScraper(httpClient2);

var repo = new ProductRepository("Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;");

var migrosProducts = await migrosScraper.FetchProductsAsync("su");
foreach (var product in migrosProducts)
    await repo.SaveAsync("migros", product);

var macroProducts = await macroCenterScraper.FetchProductsAsync("muz");
foreach (var product in macroProducts)
    await repo.SaveAsync("macrocenter", product);

Console.WriteLine($"Saved {migrosProducts.Count} Migros + {macroProducts.Count} Macrocenter products.");