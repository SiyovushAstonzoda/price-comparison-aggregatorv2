using PriceAggregator.Core;

var connectionString = "Server=localhost\\SQLEXPRESS;Database=Aggregator;User Id=sa;Password=123456;TrustServerCertificate=True;";

// var httpClient1 = new HttpClient();
// var migrosScraper = new MigrosScraper(httpClient1);

// var httpClient2 = new HttpClient();
// var macroCenterScraper = new MacroCenterScraper(httpClient2);

var httpClient3 = new HttpClient();
var ozdilekScraper = new OzdilekteyimScraper(httpClient3);

var ikeaHttpClient = new HttpClient();
var ikeaScraper = new IkeaScraper(ikeaHttpClient);

var repo = new ProductRepository(connectionString);
var matchingService = new MatchingService(connectionString);
var categoryRepository = new CategoryRepository(connectionString);

string searchItem = "sandalye";
int savedCount = 0;
int failedCount = 0;

// var migrosProducts = await migrosScraper.FetchProductsAsync(searchItem);
// foreach (var product in migrosProducts)
// {
//     var savedId = await repo.SaveAsync("migros", product);
//     if (savedId is null)
//     {
//         failedCount++;
//         continue;
//     }

//     var matched = await matchingService.MatchProductAsync(
//         savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
//     if (matched) savedCount++;
//     else failedCount++;
// }

// var macroProducts = await macroCenterScraper.FetchProductsAsync(searchItem);
// foreach (var product in macroProducts)
// {
//     var savedId = await repo.SaveAsync("macrocenter", product);
//     if (savedId is null)
//     {
//         failedCount++;
//         continue;
//     }

//     var matched = await matchingService.MatchProductAsync(
//         savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
//     if (matched) savedCount++;
//     else failedCount++;
// }

var ozdilekProducts = await ozdilekScraper.FetchProductsAsync(searchItem);
var addedCategoryCount_Ozdilek = await categoryRepository.SavePathsAsync(
    ozdilekProducts.Select(product => product.CategoryPath));
foreach (var product in ozdilekProducts)
{
    var savedId = await repo.SaveAsync("ozdilek", product);
    if (savedId is null)
    {
        failedCount++;
        continue;
    }

    var matched = await matchingService.MatchProductAsync(
        savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
    if (matched) savedCount++;
    else failedCount++;
}

var ikeaProducts = await ikeaScraper.FetchProductsAsync(searchItem);
var addedCategoryCount_Ikea = await categoryRepository.SavePathsAsync(
    ikeaProducts.Select(product => product.CategoryPath));
foreach (var product in ikeaProducts)
{
    var savedId = await repo.SaveAsync("ikea", product);
    if (savedId is null)
    {
        failedCount++;
        continue;
    }

    var matched = await matchingService.MatchProductAsync(
        savedId.Value, product.Brand, product.Title, searchItem, product.SourceCategory);
    if (matched) savedCount++;
    else failedCount++;
}


//Logger.Log($"Done. Migros fetched: {migrosProducts.Count}, Macrocenter fetched: {macroProducts.Count}, Ozdilek fetched: {ozdilekProducts.Count}");
Logger.Log($"Done. Ozdilek fetched: {ozdilekProducts.Count}, Ikea fetched: {ikeaProducts.Count}");
Logger.Log($"Ozdilek categories added: {addedCategoryCount_Ozdilek}, Ikea categories added: {addedCategoryCount_Ikea}");
Logger.Log($"Saved+matched successfully: {savedCount}, Failed: {failedCount}");
