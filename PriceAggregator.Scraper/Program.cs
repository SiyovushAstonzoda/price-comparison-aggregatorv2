using PriceAggregator.Scraper;

var httpClient = new HttpClient();
var scraper = new MigrosScraper(httpClient);
var repo = new ProductRepository("Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;");

var products = await scraper.FetchProductsAsync("su");

foreach (var product in products)
{
    await repo.SaveAsync("migros", product);
}

Console.WriteLine($"Saved {products.Count} products from Migros.");