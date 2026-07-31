using PriceAggregator.Core;

var connectionString = "Server=.\\SQLEXPRESS;Database=Aggregator;Trusted_Connection=True;TrustServerCertificate=True;";

var migrosScraper = new MigrosScraper(new HttpClient());
var macroCenterScraper = new MacroCenterScraper(new HttpClient());
var marketFiyatiScraper = new MarketFiyatiScraper(new HttpClient());
var hakmarExpressScraper = new HakmarExpressScraper(new HttpClient());
var cagriMarketScraper = new CagriMarketScraper(new HttpClient());
var evideaScraper = new EvideaScraper(new HttpClient());
var mionScraper = new MionScraper(new HttpClient());
var ikeaScraper = new IkeaScraper(new HttpClient());

var repo = new ProductRepository(connectionString);
var matchingService = new MatchingService(connectionString);

// The cosmetics sector is browsed by category rather than searched by keyword, so it's a
// separate pass rather than another entry in the search-term loop below. Gated behind an
// argument (`dotnet run -- kozmetik`) because a full sweep is ~26 category paths x 2 stores
// and takes far longer than one grocery search.
if (args.Contains("kozmetik", StringComparer.OrdinalIgnoreCase))
{
    var cosmeticsJob = new CosmeticsScrapeJob(repo, matchingService);
    await cosmeticsJob.RunAsync();
    return;
}

// Grocery search terms return almost nothing on the furniture/home stores
// (evidea, mion, ikea) — these terms match their actual catalog instead.
var marketSearchTerms = new[] { "su", "nutella", "çay", "kahve", "makarna", "zeytinyağı", "peynir", "yumurta" };
var mobilyaSearchTerms = new[] { "sandalye", "masa", "koltuk", "yatak", "halı", "lamba", "dolap", "yastık" };

int savedCount = 0;
int failedCount = 0;

foreach (var searchItem in marketSearchTerms)
{
    Logger.Log($"--- Searching '{searchItem}' (market) ---");

    var migrosRes = await RunScraperSafe("migros", () => migrosScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += migrosRes.saved; failedCount += migrosRes.failed;

    var macroRes = await RunScraperSafe("macrocenter", () => macroCenterScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += macroRes.saved; failedCount += macroRes.failed;

    var marketFiyatiRes = await RunScraperSafe("marketfiyati", () => marketFiyatiScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += marketFiyatiRes.saved; failedCount += marketFiyatiRes.failed;

    var hakmarRes = await RunScraperSafe("hakmarexpress", () => hakmarExpressScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += hakmarRes.saved; failedCount += hakmarRes.failed;

    var cagriRes = await RunScraperSafe("cagrimarket", () => cagriMarketScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += cagriRes.saved; failedCount += cagriRes.failed;
}

foreach (var searchItem in mobilyaSearchTerms)
{
    Logger.Log($"--- Searching '{searchItem}' (mobilya) ---");

    var evideaRes = await RunScraperSafe("evidea", () => evideaScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += evideaRes.saved; failedCount += evideaRes.failed;

    var mionRes = await RunScraperSafe("mion", () => mionScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += mionRes.saved; failedCount += mionRes.failed;

    var ikeaRes = await RunScraperSafe("ikea", () => ikeaScraper.FetchProductsAsync(searchItem), searchItem);
    savedCount += ikeaRes.saved; failedCount += ikeaRes.failed;
}

Logger.Log($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");

async Task<(int saved, int failed)> RunScraperSafe(string scraperName, Func<Task<List<ProductDto>>> fetchFunc, string searchItem)
{
    int saved = 0;
    int failed = 0;
    try
    {
        var products = await fetchFunc();
        foreach (var product in products)
        {
            var savedId = await repo.SaveAsync(scraperName, product);
            if (savedId is null) { failed++; continue; }

            var matched = await matchingService.MatchProductAsync(savedId.Value, product, scraperName);
            if (matched) saved++; else failed++;

            await Task.Delay(30);
        }
    }
    catch (Exception ex)
    {
        Logger.Log($"[{scraperName}] Error searching '{searchItem}': {ex.Message}");
    }
    return (saved, failed);
}