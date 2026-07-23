using PriceAggregator.Scraper;

var connectionString = "Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;";

var httpClient1 = new HttpClient();
var migrosScraper = new MigrosScraper(httpClient1);

var httpClient2 = new HttpClient();
var macroCenterScraper = new MacroCenterScraper(httpClient2);

var repo = new ProductRepository(connectionString);
var matchingService = new MatchingService(connectionString);

string searchItem = "su";
int savedCount = 0;
int failedCount = 0;

var migrosProducts = await migrosScraper.FetchProductsAsync(searchItem);
foreach (var product in migrosProducts)
{
    var savedId = await repo.SaveAsync("migros", product);
    if (savedId is null)
    {
        failedCount++;
        continue;
    }

    var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title);
    if (matched) savedCount++;
    else failedCount++;
}

var macroProducts = await macroCenterScraper.FetchProductsAsync(searchItem);
foreach (var product in macroProducts)
{
    var savedId = await repo.SaveAsync("macrocenter", product);
    if (savedId is null)
    {
        failedCount++;
        continue;
    }

    var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title);
    if (matched) savedCount++;
    else failedCount++;
}

Console.WriteLine($"Done. Migros fetched: {migrosProducts.Count}, Macrocenter fetched: {macroProducts.Count}");
Console.WriteLine($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");