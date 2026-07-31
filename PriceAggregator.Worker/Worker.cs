using PriceAggregator.Core;

namespace PriceAggregator.Worker;

public class Worker : BackgroundService
{
    // Must match the Api project's connection string — both read/write the same
    // local Aggregator database (the Docker mssql container is a separate,
    // disconnected database and is not what the running app reads from).
    private readonly string _connectionString =
        @"Server=.\SQLEXPRESS;Database=Aggregator;Integrated Security=True;TrustServerCertificate=True;";

    private readonly TimeSpan _interval = TimeSpan.FromHours(6); // adjust as needed

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Logger.Log("=== Starting scheduled scrape ===");
            await RunScrapeAsync();
            Logger.Log($"=== Scrape finished. Waiting {_interval.TotalHours}h until next run. ===");

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Happens on graceful shutdown — expected, not an error
                break;
            }
        }
    }

    private readonly string[] _marketSearchTerms = new[]
    {
        "su", "nutella", "çay", "kahve", "makarna", "zeytinyağı", "peynir", "yumurta"
    };

    // Grocery search terms return almost nothing on the furniture/home stores
    // (evidea, mion, ikea) — these terms match their actual catalog instead.
    private readonly string[] _mobilyaSearchTerms = new[]
    {
        "sandalye", "masa", "koltuk", "yatak", "halı", "lamba", "dolap", "yastık"
    };

    private async Task RunScrapeAsync()
    {
        var httpClient1 = new HttpClient();
        var migrosScraper = new MigrosScraper(httpClient1);

        var httpClient2 = new HttpClient();
        var macroCenterScraper = new MacroCenterScraper(httpClient2);

        var httpClient3 = new HttpClient();
        var marketFiyatiScraper = new MarketFiyatiScraper(httpClient3);

        var httpClient6 = new HttpClient();
        var hakmarExpressScraper = new HakmarExpressScraper(httpClient6);

        var httpClient8 = new HttpClient();
        var cagriMarketScraper = new CagriMarketScraper(httpClient8);

        var httpClient4 = new HttpClient();
        var evideaScraper = new EvideaScraper(httpClient4);

        var httpClient5 = new HttpClient();
        var mionScraper = new MionScraper(httpClient5);

        var httpClient7 = new HttpClient();
        var ikeaScraper = new IkeaScraper(httpClient7);


        var repo = new ProductRepository(_connectionString);
        var matchingService = new MatchingService(_connectionString);

        int savedCount = 0;
        int failedCount = 0;

        foreach (var searchItem in _marketSearchTerms)
        {
            Logger.Log($"--- Searching '{searchItem}' (market) ---");

            var migrosRes = await RunScraperSafe("migros", () => migrosScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += migrosRes.saved; failedCount += migrosRes.failed;

            var macroRes = await RunScraperSafe("macrocenter", () => macroCenterScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += macroRes.saved; failedCount += macroRes.failed;

            var marketFiyatiRes = await RunScraperSafe("marketfiyati", () => marketFiyatiScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += marketFiyatiRes.saved; failedCount += marketFiyatiRes.failed;

            var hakmarRes = await RunScraperSafe("hakmarexpress", () => hakmarExpressScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += hakmarRes.saved; failedCount += hakmarRes.failed;

            var cagriRes = await RunScraperSafe("cagrimarket", () => cagriMarketScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += cagriRes.saved; failedCount += cagriRes.failed;

            // Small delay between search terms to avoid hammering the sites back-to-back
            //await Task.Delay(TimeSpan.FromSeconds(3));
        }

        foreach (var searchItem in _mobilyaSearchTerms)
        {
            Logger.Log($"--- Searching '{searchItem}' (mobilya) ---");

            var evideaRes = await RunScraperSafe("evidea", () => evideaScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += evideaRes.saved; failedCount += evideaRes.failed;

            var mionRes = await RunScraperSafe("mion", () => mionScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += mionRes.saved; failedCount += mionRes.failed;

            var ikeaRes = await RunScraperSafe("ikea", () => ikeaScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += ikeaRes.saved; failedCount += ikeaRes.failed;
        }

        Logger.Log($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");

        // Unlike Market/Mobilya above, Kozmetik is browsed by category rather than searched
        // by keyword (see CosmeticsScrapeJob), so it doesn't fit the searchItem loops — it's
        // its own sweep, but still part of the same scheduled 6h cycle as everything else.
        Logger.Log("--- Sweeping Kozmetik (category browse) ---");
        var cosmeticsJob = new CosmeticsScrapeJob(repo, matchingService);
        await cosmeticsJob.RunAsync();
    }

    private async Task<(int saved, int failed)> RunScraperSafe(string scraperName, Func<Task<List<ProductDto>>> fetchFunc, ProductRepository repo, MatchingService matchingService, string searchItem)
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

                // Each product costs MatchingService/ProductRepository ~15-20 individual DB
                // round-trips (seller/brand lookups, candidate matching, offer + attribute
                // upserts). Firing those back-to-back for thousands of products with no
                // pacing at all overwhelms SQL Server Express's limited scheduler — new
                // connections start queuing and time out at login (see the Api hangs this
                // caused). A small pause between products costs a few seconds overall but
                // keeps the connection rate something Express can actually keep up with.
                await Task.Delay(30);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[{scraperName}] Error searching '{searchItem}': {ex.Message}");
        }
        return (saved, failed);
    }
}