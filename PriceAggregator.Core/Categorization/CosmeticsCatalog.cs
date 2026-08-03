namespace PriceAggregator.Core.Categorization;

// What "the cosmetics sector" means to the scrape, kept deliberately tiny.
//
// An earlier draft of this file was a hardcoded list of ~26 category slugs. That was wrong
// twice over: the slugs the stores actually use carry a numeric id suffix
// ("sac-bakim-c-8f", not "kozmetik/sac-bakimi"), and the same suffix means different
// categories on Migros vs MacroCenter — so any hardcoded list is both unguessable and
// non-portable, and goes stale whenever a store renumbers.
//
// So nothing about the tree is hardcoded. The sector root is discovered per store from a
// probe keyword (MigrosPlatformCategoryBrowser.DiscoverRootAsync) and its children are read
// from the store's own CATEGORY facet. All that's left to declare is the probe.
public static class CosmeticsCatalog
{
    // Probes are tried in order until one resolves to a root category. Each has to be a word
    // that can *only* be filed under personal care on both stores — "sabun" would be a bad
    // probe because dish soap lives in the cleaning aisle, and "krem" because of krem
    // çikolata. Several are listed so a temporary out-of-stock on one doesn't fail the sweep.
    public static readonly string[] RootProbeKeywords =
    [
        "şampuan",
        "diş macunu",
        "deodorant",
    ];

    // Guards against the probe landing somewhere unintended (a store reshuffling its tree,
    // or a probe word turning up in a promo category): the discovered root's name has to
    // still look like personal care before a full sweep is launched from it. Compared after
    // TurkishNormalizer, so these are written in its ASCII output form.
    private static readonly string[] ExpectedRootMarkers =
    [
        "kozmetik",
        "kisisel bakim",
        "saglik",
    ];

    public static bool LooksLikeCosmeticsRoot(string rootName)
    {
        if (string.IsNullOrWhiteSpace(rootName)) return false;

        var normalized = TurkishNormalizer.Normalize(rootName);
        return ExpectedRootMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }
}
