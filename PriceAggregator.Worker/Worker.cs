using PriceAggregator.Core;

namespace PriceAggregator.Worker;

public class Worker : BackgroundService
{
    private readonly string _connectionString =
        "Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;";

    private readonly TimeSpan _interval = TimeSpan.FromHours(1); // adjust as needed

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

    private async Task RunScrapeAsync()
    {
        var httpClient1 = new HttpClient();
        var migrosScraper = new MigrosScraper(httpClient1);

        var httpClient2 = new HttpClient();
        var macroCenterScraper = new MacroCenterScraper(httpClient2);

        var repo = new ProductRepository(_connectionString);
        var matchingService = new MatchingService(_connectionString);

        string searchItem = "su";
        int savedCount = 0;
        int failedCount = 0;

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

        Logger.Log($"Migros fetched: {migrosProducts.Count}, Macrocenter fetched: {macroProducts.Count}");
        Logger.Log($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");
    }
}