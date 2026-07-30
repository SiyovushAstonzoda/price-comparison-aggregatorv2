using PriceAggregator.Core;

namespace PriceAggregator.Worker;

public class Worker : BackgroundService
{
    private readonly string _connectionString =
        "Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;";

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
                // Düzenli kapatma sırasında beklenen durumdur, hata değildir.
                break;
            }
        }
    }

    private readonly string[] _searchTerms = new[]
    {
        "su", "nutella", "çay", "kahve", "makarna", "zeytinyağı", "peynir", "yumurta"
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

        var httpClient9 = new HttpClient();
        var ozdilekScraper = new OzdilekteyimScraper(httpClient9);

        // var httpClient8 = new HttpClient();
        // var cagriMarketScraper = new CagriMarketScraper(httpClient8);

        // var httpClient4 = new HttpClient();
        // var evideaScraper = new EvideaScraper(httpClient4);

        // var httpClient5 = new HttpClient();
        // var mionScraper = new MionScraper(httpClient5);

        // var httpClient7 = new HttpClient();
        // var ikeaScraper = new IkeaScraper(httpClient7);


        var repo = new ProductRepository(_connectionString);
        var matchingService = new MatchingService(_connectionString);

        int savedCount = 0;
        int failedCount = 0;

        foreach (var searchItem in _searchTerms)
        {
            Logger.Log($"--- Searching '{searchItem}' ---");

            var migrosRes = await RunScraperSafe("migros", () => migrosScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += migrosRes.saved; failedCount += migrosRes.failed;

            var macroRes = await RunScraperSafe("macrocenter", () => macroCenterScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += macroRes.saved; failedCount += macroRes.failed;

            var marketFiyatiRes = await RunScraperSafe("marketfiyati", () => marketFiyatiScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += marketFiyatiRes.saved; failedCount += marketFiyatiRes.failed;

            // var evideaRes = await RunScraperSafe("evidea", () => evideaScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            // savedCount += evideaRes.saved; failedCount += evideaRes.failed;

            // var mionRes = await RunScraperSafe("mion", () => mionScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            // savedCount += mionRes.saved; failedCount += mionRes.failed;

            var hakmarRes = await RunScraperSafe("hakmarexpress", () => hakmarExpressScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += hakmarRes.saved; failedCount += hakmarRes.failed;

            var ozdilekRes = await RunScraperSafe("ozdilek", () => ozdilekScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            savedCount += ozdilekRes.saved; failedCount += ozdilekRes.failed;

            // var ikeaRes = await RunScraperSafe("ikea", () => ikeaScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            // savedCount += ikeaRes.saved; failedCount += ikeaRes.failed;

            // var cagriRes = await RunScraperSafe("cagrimarket", () => cagriMarketScraper.FetchProductsAsync(searchItem), repo, matchingService, searchItem);
            // savedCount += cagriRes.saved; failedCount += cagriRes.failed;

            // Siteleri art arda yoğun istekle yormamak için arama kelimeleri arasında bekleme yapılabilir.
            //await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Logger.Log($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");
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

                var matched = await matchingService.MatchProductAsync(
                    savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
                if (matched) saved++; else failed++;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[{scraperName}] Error searching '{searchItem}': {ex.Message}");
        }
        return (saved, failed);
    }
}
