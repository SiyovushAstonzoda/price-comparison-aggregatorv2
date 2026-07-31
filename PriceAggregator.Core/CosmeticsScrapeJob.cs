namespace PriceAggregator.Core;

// Sweeps the cosmetics / personal-care sector across the stores that sell it.
//
// Per store: discover that store's own cosmetics root, read its child categories from the
// store's facet, then page through each child. Persistence is deliberately unchanged — every
// product goes through the same ProductRepository.SaveAsync -> MatchingService.MatchProductAsync
// pair the grocery scrape uses, so canonical matching, offers, price history and attributes
// all behave identically. The only new thing is *which* products get fed in.
public class CosmeticsScrapeJob
{
    private readonly ProductRepository _repo;
    private readonly MatchingService _matchingService;

    public CosmeticsScrapeJob(ProductRepository repo, MatchingService matchingService)
    {
        _repo = repo;
        _matchingService = matchingService;
    }

    public record Result(int Fetched, int Saved, int Failed);

    public async Task<Result> RunAsync(CancellationToken ct = default)
    {
        // One HttpClient per store: each browser rewrites shared default headers (Referer in
        // particular) in its constructor, so a single shared client would end up sending
        // MacroCenter's Referer to Migros.
        var browsers = new[]
        {
            MigrosPlatformCategoryBrowser.ForMigros(new HttpClient()),
            MigrosPlatformCategoryBrowser.ForMacroCenter(new HttpClient()),
        };

        int fetched = 0, saved = 0, failed = 0;

        // Stores and categories are swept sequentially on purpose. CategoryResolver's "does a
        // fuzzy-matching sibling already exist?" check and the INSERT that follows it aren't
        // one transaction, so two concurrent sweeps can both miss the sibling and create the
        // very duplicate node that check exists to prevent. This is a background job — a
        // correct tree is worth more here than wall-clock time.
        foreach (var browser in browsers)
        {
            if (ct.IsCancellationRequested) break;

            var categories = await ResolveCategoriesToSweepAsync(browser, ct);
            if (categories.Count == 0)
            {
                Logger.Log($"[Cosmetics] Skipping {browser.Source} — no cosmetics categories found.");
                continue;
            }

            foreach (var category in categories)
            {
                if (ct.IsCancellationRequested) break;

                var products = await browser.FetchCategoryAsync(category.Slug, ct);
                fetched += products.Count;

                foreach (var product in products)
                {
                    var sellerProductId = await _repo.SaveAsync(browser.Source, product);
                    if (sellerProductId is null)
                    {
                        failed++;
                        continue;
                    }

                    var matched = await _matchingService.MatchProductAsync(
                        sellerProductId.Value, product, browser.Source);

                    if (matched) saved++;
                    else failed++;
                }
            }
        }

        Logger.Log($"[Cosmetics] Fetched: {fetched}, saved+matched: {saved}, failed: {failed}");
        return new Result(fetched, saved, failed);
    }

    // Children are swept rather than the root itself, even though the root would return the
    // same products in one pass: a per-category sweep gives per-category logging (so a
    // category that's silently returning nothing is visible), and it keeps each pagination
    // walk short enough that one failure mid-sweep doesn't cost the whole sector. The root is
    // used directly only when it reports no children, i.e. it's a leaf.
    private static async Task<List<CategoryRef>> ResolveCategoriesToSweepAsync(
        MigrosPlatformCategoryBrowser browser, CancellationToken ct)
    {
        foreach (var probe in CosmeticsCatalog.RootProbeKeywords)
        {
            if (ct.IsCancellationRequested) break;

            var root = await browser.DiscoverRootAsync(probe, ct);
            if (root is null) continue;

            // A probe can land outside personal care if a store files that word oddly, and
            // sweeping the wrong root would pour an unrelated section into the cosmetics
            // sector. Cheaper to reject it and try the next probe.
            if (!CosmeticsCatalog.LooksLikeCosmeticsRoot(root.Name))
            {
                Logger.Log($"[{browser.Source}] Probe '{probe}' resolved to '{root.Name}', which doesn't look like a cosmetics root — trying next probe.");
                continue;
            }

            var children = await browser.DiscoverChildCategoriesAsync(root.Slug, ct);
            return children.Count > 0 ? children : [root];
        }

        return [];
    }
}
