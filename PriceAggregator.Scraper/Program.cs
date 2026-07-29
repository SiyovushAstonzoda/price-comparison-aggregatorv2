using PriceAggregator.Core;

var connectionString = "Server=localhost,1433;Database=Tutumlu;User Id=sa;Password=SariaAdmin123!;TrustServerCertificate=True;";

var httpClient1 = new HttpClient();
var migrosScraper = new MigrosScraper(httpClient1);

var httpClient2 = new HttpClient();
var macroCenterScraper = new MacroCenterScraper(httpClient2);

var httpClient3 = new HttpClient();
var ikeaScraper = new IkeaScraper(httpClient3);

var repo = new ProductRepository(connectionString);
var matchingService = new MatchingService(connectionString);

var marketSearchTerms = new[] { "su", "nutella", "çay", "kahve", "makarna" };
var ikeaSearchTerms = new[] { "sandalye", "masa", "koltuk", "dolap", "lamba", "yatak", "sehpa", "mutfak" };

int savedCount = 0;
int failedCount = 0;

foreach (var searchItem in marketSearchTerms)
{
    var migrosProducts = await migrosScraper.FetchProductsAsync(searchItem);
    foreach (var product in migrosProducts)
    {
        var savedId = await repo.SaveAsync("migros", product);
        if (savedId is null) { failedCount++; continue; }
        var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title);
        if (matched) savedCount++; else failedCount++;
    }

    var macroProducts = await macroCenterScraper.FetchProductsAsync(searchItem);
    foreach (var product in macroProducts)
    {
        var savedId = await repo.SaveAsync("macrocenter", product);
        if (savedId is null) { failedCount++; continue; }
        var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title);
        if (matched) savedCount++; else failedCount++;
    }
}

foreach (var searchItem in ikeaSearchTerms)
{
    var ikeaProducts = await ikeaScraper.FetchProductsAsync(searchItem);
    foreach (var product in ikeaProducts)
    {
        var savedId = await repo.SaveAsync("ikea", product);
        if (savedId is null) { failedCount++; continue; }
        var matched = await matchingService.MatchProductAsync(savedId.Value, product.Brand, product.Title);
        if (matched) savedCount++; else failedCount++;
    }
}

Logger.Log($"Done scraping. Saved+matched successfully: {savedCount}, Failed: {failedCount}");