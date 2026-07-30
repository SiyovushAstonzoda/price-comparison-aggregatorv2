using Microsoft.Extensions.Configuration;
using PriceAggregator.Core;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Veritabanı bağlantı bilgilerini appsettings.json dosyasından değiştirin
string connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

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

    var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
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

    var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
    if (matched) savedCount++;
    else failedCount++;
}

Logger.Log($"Done. Migros fetched: {migrosProducts.Count}, Macrocenter fetched: {macroProducts.Count}");
Logger.Log($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");