namespace PriceAggregator.Core.Categorization;

// Splits scraped products into the top-level sectors the app shows ("Market", "Mobilya",
// "Kişisel Bakım, Kozmetik, Sağlık"). These sector names become the true root Categories — every
// other category nests underneath one of them. Furniture/home sources never provide real
// category data, so their products land directly on the sector root instead of being run
// through CategoryClassifier's grocery keyword rules (which produced false positives like
// "Kahve Sehpası" matching the "kahve" keyword).
public static class SectorClassifier
{
    private static readonly HashSet<string> FurnitureSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "evidea", "mion", "ikea"
    };

    public const string MarketSectorName = "Market";
    public const string MobilyaSectorName = "Mobilya";

    // Must stay textually identical to what CosmeticsCategorySynonyms canonicalizes "Kozmetik"
    // to (Migros's own real root breadcrumb spelling) — CategoryResolver.ResolveBreadcrumbOnlyAsync
    // feeds that canonicalized name back into FindOrCreateAsync scoped to this same sector root,
    // and a mismatch here used to make it match on Slug and fail, creating a redundant second
    // "cosmetics root" node one level below this one on every scrape run instead of collapsing
    // into it.
    public const string KozmetikSectorName = "Kişisel Bakım, Kozmetik, Sağlık";

    // Sector used to be a pure function of the source, which worked while every source sold
    // exactly one kind of thing. Migros and MacroCenter sell both groceries AND cosmetics
    // through one API, so a shampoo scraped from Migros would otherwise be filed under
    // "Market" next to the cheese — and would share Market's slug-uniqueness scope in
    // CategoryResolver, where a cosmetics "Sabun" and a cleaning-aisle "Sabun" then collapse
    // into one node. The product's own root breadcrumb is what actually distinguishes the
    // two, so it's consulted when present.
    //
    // `product` is optional so existing single-sector callers keep compiling; passing it is
    // what enables the cosmetics split.
    public static string GetSectorName(string source, ProductDto? product = null)
    {
        if (FurnitureSources.Contains(source)) return MobilyaSectorName;
        if (product is not null && IsPersonalCare(product)) return KozmetikSectorName;
        return MarketSectorName;
    }

    // Matched against the ancestors rather than the leaf or the title: the leaf is often a
    // generic word shared across sectors ("Sabun" sits under both the cleaning aisle and
    // personal care), while the root/parent breadcrumb is where these stores actually name
    // their "Kozmetik ve Kişisel Bakım" branch. Titles are deliberately never consulted —
    // telling "bebek bezi" from "bebek maması" by keyword would reinvent exactly the mess
    // CategoryClassifier's exclude-lists exist to contain.
    private static readonly string[] PersonalCareMarkers =
    [
        "kozmetik",
        "kisisel bakim",
        "kisisel-bakim",
        "makyaj",
        "parfum",
        "bebek bakim",
        "bebek-bakim",
    ];

    private static bool IsPersonalCare(ProductDto product) =>
        Matches(product.RootCategoryName) || Matches(product.ParentCategoryName);

    private static bool Matches(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return false;

        // Normalize first so "Kişisel Bakım" and "KISISEL BAKIM" both reduce to the same
        // ASCII form the markers are written in (see TurkishNormalizer's dotted-İ note).
        var normalized = TurkishNormalizer.Normalize(categoryName);
        return PersonalCareMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }
}
