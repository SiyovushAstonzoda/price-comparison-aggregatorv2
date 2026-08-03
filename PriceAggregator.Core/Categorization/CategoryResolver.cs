using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core.Categorization;

// Builds the Categories tree on the fly from whatever category data the source store
// provided (see ProductDto.CategoryName/Parent/Root). Falls back to CategoryClassifier's
// keyword guess when the source gave no category at all.
public static class CategoryResolver
{
    public static async Task<int?> ResolveAsync(SqlConnection db, ProductDto product, string source)
    {
        var sectorName = SectorClassifier.GetSectorName(source, product);
        var sectorId = await FindOrCreateAsync(db, sectorName, null, sectorId: null);

        return sectorName switch
        {
            SectorClassifier.MarketSectorName => await ResolveMarketAsync(db, product, sectorId),
            SectorClassifier.MobilyaSectorName => await ResolveMobilyaAsync(db, product, sectorId),
            SectorClassifier.KozmetikSectorName => await ResolveBreadcrumbOnlyAsync(db, product, sectorId),
            _ => sectorId
        };
    }

    // Cosmetics reaches us only via MigrosPlatformCategoryBrowser, which browses real
    // category pages — so a real breadcrumb is always present and the tree is built from it
    // exactly as ResolveMarketAsync does. What's deliberately NOT shared is that method's
    // fallback: CategoryClassifier's rules are grocery vocabulary, and letting them near
    // personal care is how "ağız bakım suyu" ends up filed under "Su > İçecek" (the rule
    // list already carries a special case fighting precisely that). A cosmetics product
    // with no breadcrumb sits on the sector root instead of being guessed at.
    private static async Task<int?> ResolveBreadcrumbOnlyAsync(SqlConnection db, ProductDto product, int sectorId)
    {
        if (string.IsNullOrWhiteSpace(product.CategoryName)) return sectorId;

        int? parentId = sectorId;

        if (!string.IsNullOrWhiteSpace(product.RootCategoryName))
            parentId = await FindOrCreateAsync(db, CosmeticsCategorySynonyms.Canonicalize(product.RootCategoryName), sectorId, sectorId);

        if (!string.IsNullOrWhiteSpace(product.ParentCategoryName))
            parentId = await FindOrCreateAsync(db, CosmeticsCategorySynonyms.Canonicalize(product.ParentCategoryName), parentId, sectorId);

        return await FindOrCreateAsync(db, CosmeticsCategorySynonyms.Canonicalize(product.CategoryName), parentId, sectorId);
    }

    // Migros/MacroCenter's real category data already reflects one coherent, non-overlapping
    // taxonomy (they're literally the same platform), so their Root/Parent/Leaf breadcrumb is
    // trusted and built literally. When a source gives none, CategoryClassifier's grocery
    // keyword guess is used instead (see its own docs for why that's Market-only vocabulary).
    private static async Task<int?> ResolveMarketAsync(SqlConnection db, ProductDto product, int sectorId)
    {
        if (!string.IsNullOrWhiteSpace(product.CategoryName))
        {
            int? parentId = sectorId;

            if (!string.IsNullOrWhiteSpace(product.RootCategoryName))
                parentId = await FindOrCreateAsync(db, product.RootCategoryName, sectorId, sectorId);

            if (!string.IsNullOrWhiteSpace(product.ParentCategoryName))
                parentId = await FindOrCreateAsync(db, product.ParentCategoryName, parentId, sectorId);

            return await FindOrCreateAsync(db, product.CategoryName, parentId, sectorId);
        }

        var classified = CategoryClassifier.Classify(product.Title);
        if (classified is null) return sectorId;

        var (name, classifierParentName) = classified.Value;
        var classifierParentId = classifierParentName is null
            ? sectorId
            : await FindOrCreateAsync(db, classifierParentName, sectorId, sectorId);

        return await FindOrCreateAsync(db, name, classifierParentId, sectorId);
    }

    // Unlike Market, Evidea/İkea/Mion each organize furniture under their OWN, mutually
    // incompatible hierarchy (İkea's "Mobilyalar > Masalar > Çalışma Masaları" vs Evidea's
    // "Tamamlayıcı Mobilya > Çalışma Odası > Çalışma Sandalyesi" vs Mion's Migros-style
    // categories). Trusting any one of those literally — as this used to — produced a dozen
    // separate top-level "furniture" roots all containing the same real product types, so
    // instead every source's real category name (or, failing that, the title) is first
    // mapped onto ONE shared canonical bucket via FurnitureCategoryClassifier. The store's
    // own specific leaf name is then kept as a child of that bucket for real granularity —
    // e.g. İkea's "Ahşap Çalışma Masaları" ends up under the shared "Masa" node, not under
    // a separate "Mobilyalar" tree.
    private static async Task<int?> ResolveMobilyaAsync(SqlConnection db, ProductDto product, int sectorId)
    {
        string? Classify(string? text) => string.IsNullOrWhiteSpace(text) ? null : FurnitureCategoryClassifier.Classify(text);

        var canonicalName =
            Classify(product.CategoryName) ??
            Classify(product.ParentCategoryName) ??
            Classify(product.RootCategoryName) ??
            Classify(product.Title);

        if (canonicalName is null) return sectorId;

        // Not actually furniture (soap, cosmetics, small appliances) — same real category
        // Market already has, just reached via a furniture-sector source that doesn't give
        // real category breadcrumbs for these. Filing it under a Mobilya-scoped copy of the
        // same name would nest it in the furniture menu next to Koltuk/Yatak/Masa, which is
        // confusing for a product that has nothing to do with furniture.
        if (FurnitureCategoryClassifier.MarketCrossoverBuckets.Contains(canonicalName))
        {
            var marketSectorId = await FindOrCreateAsync(db, SectorClassifier.MarketSectorName, null, sectorId: null);
            return await FindOrCreateAsync(db, canonicalName, marketSectorId, marketSectorId);
        }

        var canonicalId = await FindOrCreateAsync(db, canonicalName, sectorId, sectorId);

        // Only nest the store's real leaf name as a child when it says something more
        // specific than the canonical bucket itself — otherwise "Masa" would get a
        // redundant "Masa" child every time a source's leaf name IS just "Masa"/"Masalar".
        if (string.IsNullOrWhiteSpace(product.CategoryName) ||
            FuzzyMatcher.NamesMatch(product.CategoryName, canonicalName))
        {
            return canonicalId;
        }

        return await FindOrCreateAsync(db, product.CategoryName, canonicalId, sectorId);
    }

    // Identity is derived from the normalized Name, not the source store's own category
    // id/slug — different stores use different ids for the same real category (e.g.
    // Migros and MacroCenter both have an "İçecek" root with different prettyNames), and
    // matching by name is what lets those merge into a single node instead of duplicating.
    //
    // That name match is scoped to `sectorId` — a category name isn't necessarily unique
    // *across* sectors (a furniture store's own taxonomy can happen to reuse a word like
    // "Elektronik" that a grocery store's tree already has), and reusing that unrelated
    // node would silently misfile the product into the wrong sector's tree. `sectorId` is
    // null only when this call is resolving the sector root itself (Market/Mobilya), which
    // has nothing to scope against yet.
    private static async Task<int> FindOrCreateAsync(SqlConnection db, string name, int? parentId, int? sectorId)
    {
        var slug = Slugify(name);

        var existing = await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Categories WHERE Slug = @Slug AND (@SectorId IS NULL OR SectorId = @SectorId)",
            new { Slug = slug, SectorId = sectorId });
        if (existing.HasValue) return existing.Value;

        // Exact slug match failed, but a sibling under the same parent might still be the
        // same real category under a slightly different name (plural vs singular, spelling
        // variant, etc. — e.g. "İçecek" vs "İçecekler"). Reuse it instead of creating a
        // duplicate node next to it.
        var siblingId = await FindFuzzyMatchAsync(db, name, parentId, sectorId);
        if (siblingId.HasValue) return siblingId.Value;

        int newId;
        try
        {
            newId = await db.QuerySingleAsync<int>(@"
                INSERT INTO Categories (Name, Slug, ParentCategoryId, SectorId)
                OUTPUT INSERTED.Id
                VALUES (@Name, @Slug, @ParentId, @SectorId)",
                new { Name = name, Slug = slug, ParentId = parentId, SectorId = sectorId });
        }
        catch (SqlException)
        {
            // Unique constraint hit — another concurrent scrape created this exact
            // (Slug, SectorId) pair first. Must stay sector-scoped here too, or this
            // fallback would return a same-named row from a *different* sector instead.
            newId = await db.QuerySingleAsync<int>(
                "SELECT Id FROM Categories WHERE Slug = @Slug AND (@SectorId IS NULL OR SectorId = @SectorId)",
                new { Slug = slug, SectorId = sectorId });
        }

        // This call is defining a brand-new sector root (parentId/sectorId both null) —
        // a root's own Id is its SectorId, but it isn't known until after the insert.
        if (sectorId is null && parentId is null)
        {
            await db.ExecuteAsync(
                "UPDATE Categories SET SectorId = @Id WHERE Id = @Id",
                new { Id = newId });
        }

        return newId;
    }

    private static async Task<int?> FindFuzzyMatchAsync(SqlConnection db, string name, int? parentId, int? sectorId)
    {
        var siblings = await db.QueryAsync<(int Id, string Name)>(@"
            SELECT Id, Name FROM Categories
            WHERE ((ParentCategoryId = @ParentId) OR (@ParentId IS NULL AND ParentCategoryId IS NULL))
            AND (@SectorId IS NULL OR SectorId = @SectorId)",
            new { ParentId = parentId, SectorId = sectorId });


        foreach (var sibling in siblings)
        {
            if (FuzzyMatcher.NamesMatch(name, sibling.Name)) return sibling.Id;
        }

        return null;
    }

    private static string Slugify(string text)
    {
        var normalized = TurkishNormalizer.Normalize(text);
        var cleaned = Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
        cleaned = Regex.Replace(cleaned, @"[\s-]+", " ").Trim();
        return cleaned.Replace(" ", "-");
    }
}
