using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using PriceAggregator.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// app.js reads PascalCase fields (LowestPrice, OfferCount, ...) — keep the
// serializer from camelCasing them by default.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

var app = builder.Build();

app.UseCors("AllowFrontend");

var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "Frontend");
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(frontendPath)
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(frontendPath)
});

string connectionString = @"Server=.\SQLEXPRESS;Database=Aggregator;Integrated Security=True;TrustServerCertificate=True;";

// Expands a category slug (leaf or root) into the WITH clause needed to match it and
// all its descendants. Empty when no category filter is active.
static string CategoryTreeCte(string? category) => string.IsNullOrWhiteSpace(category) ? "" : @"
    WITH CategoryTree AS (
        SELECT Id FROM Categories WHERE Slug = @CategorySlug
        UNION ALL
        SELECT c.Id FROM Categories c JOIN CategoryTree ct ON c.ParentCategoryId = ct.Id
    )";

// A plain "%term%" substring LIKE matches the term anywhere inside a word, not just as a whole
// word — searching "su" then also matches "SUpreme", "SUn", "SUgar", "SUede" (all common
// product-name loanwords), pulling in things like hair dye or sunscreen. Padding both the
// column and the pattern with a leading/trailing space turns LIKE into whole-word matching:
// "su" then only matches a standalone " su " token (e.g. "Erikli Su 1 L"), not a substring of
// a longer word. @Query is bound to the bare trimmed term (no % wrapping) by every caller.
static string WordBoundaryLike(string column) => $"(' ' + {column} + ' ') LIKE ('% ' + @Query + ' %')";

// A search term can name a whole category (e.g. "mobilya") rather than a product/brand — this
// CTE resolves the term to every category whose name matches it plus all of their descendants,
// so "AND p.CategoryId IN (SELECT Id FROM MatchingCategoriesByName)" pulls in the entire
// subtree instead of only products whose title happens to contain the word. Combined with
// CategoryTreeCte in one WITH clause (T-SQL allows only one WITH per statement) by callers
// that need both a category filter and a search term at the same time.
static string CategoryNameMatchCte(string? q) => string.IsNullOrWhiteSpace(q) ? "" : $@"
    MatchingCategoriesByName AS (
        SELECT Id FROM Categories WHERE {WordBoundaryLike("Name")}
        UNION ALL
        SELECT c.Id FROM Categories c JOIN MatchingCategoriesByName m ON c.ParentCategoryId = m.Id
    )";

// Combines the two CTEs above into a single WITH clause (comma-separated, as T-SQL requires),
// omitting it entirely when neither filter is active.
static string SearchCtes(string? category, string? q)
{
    var parts = new List<string>();
    if (!string.IsNullOrWhiteSpace(category)) parts.Add(CategoryTreeCte(category).Replace("WITH ", ""));
    if (!string.IsNullOrWhiteSpace(q)) parts.Add(CategoryNameMatchCte(q));
    return parts.Count == 0 ? "" : "WITH " + string.Join(",", parts);
}

app.MapGet("/api/brands", async (string? category) =>
{
    var sql = $@"{CategoryTreeCte(category)}
        SELECT DISTINCT b.Name
        FROM Brands b
        JOIN Products p ON p.BrandId = b.Id
        JOIN Offers o ON o.ProductId = p.Id
        WHERE b.Name IS NOT NULL AND b.Name <> ''";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(category))
    {
        sql += " AND p.CategoryId IN (SELECT Id FROM CategoryTree)";
        parameters.Add("CategorySlug", category);
    }
    sql += " ORDER BY b.Name";

    using var db = new SqlConnection(connectionString);
    var brands = await db.QueryAsync<string>(sql, parameters);
    return Results.Ok(brands);
});

// Which (AttributeName, AttributeValue) pairs actually exist for products in this category
// subtree, with counts — drives the frontend's dynamic filter checkboxes so e.g. "Renk" only
// shows up under Mobilya and "Özellik" only under Market, without hardcoding either.
app.MapGet("/api/attributes", async (string? category) =>
{
    var sql = $@"{CategoryTreeCte(category)}
        SELECT pa.AttributeName, pa.AttributeValue, COUNT(DISTINCT pa.ProductId) AS ProductCount
        FROM ProductAttributes pa
        JOIN Products p ON p.Id = pa.ProductId
        WHERE pa.AttributeName <> 'Adet'";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(category))
    {
        sql += " AND p.CategoryId IN (SELECT Id FROM CategoryTree)";
        parameters.Add("CategorySlug", category);
    }
    sql += " GROUP BY pa.AttributeName, pa.AttributeValue";

    using var db = new SqlConnection(connectionString);
    var rows = await db.QueryAsync<(string AttributeName, string AttributeValue, int ProductCount)>(sql, parameters);

    // Hacim/Ağırlık are size ranges, not arbitrary text — sorted small-to-large by
    // SizeFormatter's own bucket order instead of alphabetically (which would put
    // "500 Ml ve altı" after "5 Litre ve üzeri", since "500" > "5" as a string).
    string[] volumeOrder = ["500 Ml ve altı", "500 Ml - 1 Litre", "1 - 2 Litre", "2 - 5 Litre", "5 Litre ve üzeri"];
    string[] weightOrder = ["250 G ve altı", "250 - 500 G", "500 G - 1 Kg", "1 - 2 Kg", "2 Kg ve üzeri"];

    int RankOf(string attributeName, string value)
    {
        var order = attributeName switch { "Hacim" => volumeOrder, "Ağırlık" => weightOrder, _ => null };
        if (order is null) return 0;
        var i = Array.IndexOf(order, value);
        return i >= 0 ? i : int.MaxValue;
    }

    var grouped = rows
        .GroupBy(r => r.AttributeName)
        .Select(g => new
        {
            Name = g.Key,
            Values = g
                .OrderBy(r => RankOf(g.Key, r.AttributeValue))
                .ThenBy(r => r.AttributeValue)
                .Select(r => new { r.AttributeValue, r.ProductCount })
                .ToList()
        })
        .OrderBy(g => g.Name);

    return Results.Ok(grouped);
});

// Powers the search bar's type-ahead dropdown: as the shopper types, two small grouped
// lists (brand/product name matches) instead of one big product list. Each list is capped
// at 5 and scoped to things that actually have live offers/products (a brand with zero
// current listings isn't a useful shortcut).
app.MapGet("/api/search-suggestions", async (string? q) =>
{
    var term = q?.Trim() ?? "";
    if (term.Length < 2)
        return Results.Ok(new { Brands = Array.Empty<object>(), Products = Array.Empty<object>() });

    var likeTerm = $"%{term}%";
    var parameters = new { Term = likeTerm };

    using var db = new SqlConnection(connectionString);

    // Shorter names first: typing "su" should surface "Su" before "Su Ürünleri Sepeti" —
    // a cheap relevance proxy without needing full-text search.
    var brands = await db.QueryAsync<string>(@"
        SELECT TOP 5 b.Name
        FROM Brands b
        WHERE b.Name LIKE @Term AND EXISTS (SELECT 1 FROM Products p WHERE p.BrandId = b.Id)
        ORDER BY LEN(b.Name), b.Name", parameters);

    var products = await db.QueryAsync(@"
        SELECT TOP 5 p.Id, p.Name AS CanonicalTitle, p.ImageUrl
        FROM Products p
        WHERE p.Name LIKE @Term AND EXISTS (SELECT 1 FROM Offers o WHERE o.ProductId = p.Id)
        ORDER BY LEN(p.Name), p.Name", parameters);

    return Results.Ok(new { Brands = brands, Products = products });
});

app.MapGet("/api/categories", async () =>
{
    using var db = new SqlConnection(connectionString);
    var categories = await db.QueryAsync(@"
        SELECT Id, Name, Slug, ParentCategoryId
        FROM Categories
        ORDER BY ParentCategoryId ASC, Name ASC");
    return Results.Ok(categories);
});

// Distinct categories (with counts) that products matching a search term actually sit in —
// drives the filter panel's category facet so it only offers categories the search word
// actually returns results for, instead of the full category tree. The frontend rolls each
// CategoryId up to its Level 1 (root) ancestor using the category tree it already holds.
app.MapGet("/api/search-categories", async (string? q) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(Array.Empty<object>());

    using var db = new SqlConnection(connectionString);
    var rows = await db.QueryAsync<(int CategoryId, int ProductCount)>($@"{SearchCtes(null, q)}
        SELECT p.CategoryId, COUNT(DISTINCT p.Id) AS ProductCount
        FROM Products p
        JOIN Brands b ON b.Id = p.BrandId
        WHERE p.CategoryId IS NOT NULL
            AND ({WordBoundaryLike("p.Name")} OR {WordBoundaryLike("b.Name")} OR p.CategoryId IN (SELECT Id FROM MatchingCategoriesByName))
        GROUP BY p.CategoryId",
        new { Query = q.Trim() });

    // Project into an anonymous type before serializing — System.Text.Json only serializes
    // public properties, and ValueTuple exposes its data as public fields (Item1, Item2),
    // so returning `rows` directly here would serialize as empty `{}` objects.
    return Results.Ok(rows.Select(r => new { r.CategoryId, r.ProductCount }));
});

app.MapPost("/api/admin/merge-duplicate-categories", async () =>
{
    using var db = new SqlConnection(connectionString);
    var merged = await CategoryMerger.MergeDuplicatesAsync(db);
    return Results.Ok(new { merged });
});

app.MapPost("/api/admin/reclassify-categories", async () =>
{
    using var db = new SqlConnection(connectionString);

    var uncategorized = await db.QueryAsync<(int Id, string CanonicalTitle, string? Source)>(@"
        SELECT p.Id, p.Name AS CanonicalTitle,
               (SELECT TOP 1 s.Name FROM Offers o JOIN Sellers s ON s.Id = o.SellerId WHERE o.ProductId = p.Id) AS Source
        FROM Products p WHERE p.CategoryId IS NULL");

    int updated = 0;
    foreach (var product in uncategorized)
    {
        if (product.Source is null) continue;

        // Only the title survives on Products — real per-store category data (if any)
        // was only available on the original ProductDto at scrape time.
        var categoryId = await CategoryResolver.ResolveAsync(db, new ProductDto { Title = product.CanonicalTitle }, product.Source);
        if (categoryId is null) continue;

        await db.ExecuteAsync(
            "UPDATE Products SET CategoryId = @CategoryId WHERE Id = @Id",
            new { CategoryId = categoryId, product.Id });
        updated++;
    }

    return Results.Ok(new { updated });
});

// Re-checks every product currently sitting in one category against today's
// CategoryClassifier rules and moves the ones that no longer belong (e.g. after a rule fix
// like "çamaşır suyu" no longer falling into "Su"). Safe to run repeatedly: a product whose
// title still resolves to the same category is left untouched.
app.MapPost("/api/admin/reclassify-category/{categoryId}", async (int categoryId) =>
{
    using var db = new SqlConnection(connectionString);

    var products = await db.QueryAsync<(int Id, string Name)>(
        "SELECT Id, Name FROM Products WHERE CategoryId = @CategoryId",
        new { CategoryId = categoryId });

    int moved = 0;
    foreach (var product in products)
    {
        // Only the title survives on Products — real per-store category data (if any) was
        // only available on the original ProductDto at scrape time, same limitation as
        // /api/admin/reclassify-categories above. "migros" is just a representative
        // Market-sector source so CategoryResolver resolves the right sector.
        var newCategoryId = await CategoryResolver.ResolveAsync(db, new ProductDto { Title = product.Name }, "migros");
        if (newCategoryId is not null && newCategoryId != categoryId)
        {
            await db.ExecuteAsync(
                "UPDATE Products SET CategoryId = @CategoryId WHERE Id = @Id",
                new { CategoryId = newCategoryId, Id = product.Id });
            moved++;
        }
    }

    return Results.Ok(new { moved, total = products.Count() });
});

app.MapPost("/api/admin/reparse-sizes", async () =>
{
    using var db = new SqlConnection(connectionString);
    var products = await db.QueryAsync<(int Id, string CanonicalTitle)>(
        "SELECT Id, Name AS CanonicalTitle FROM Products");

    int updated = 0;
    foreach (var product in products)
    {
        var (size, unit, packQty) = SizeParser.ExtractSize(product.CanonicalTitle);
        var rows = await db.ExecuteAsync(@"
            UPDATE Products SET SizeValue = @Size, SizeUnit = @Unit, PackQuantity = @PackQty
            WHERE Id = @Id
            AND (ISNULL(SizeValue, -1) <> ISNULL(@Size, -1)
                 OR ISNULL(SizeUnit, '') <> ISNULL(@Unit, '')
                 OR PackQuantity <> @PackQty)",
            new { Id = product.Id, Size = size, Unit = unit, PackQty = packQty });
        updated += rows;
    }

    return Results.Ok(new { updated, total = products.Count() });
});

app.MapGet("/api/products/{productId}", async (int productId) =>
{
    using var db = new SqlConnection(connectionString);
    var offers = await db.QueryAsync<OfferRow>(@"
        SELECT p.Name AS CanonicalTitle, s.Name AS Source, o.Price,
               sp.ExternalImageUrl AS ImageUrl, sp.ExternalUrl AS ProductUrl,
               p.SizeValue, p.SizeUnit, p.PackQuantity
        FROM Offers o
        JOIN Products p ON p.Id = o.ProductId
        JOIN Sellers s ON s.Id = o.SellerId
        JOIN SellerProducts sp ON sp.Id = o.SellerProductId
        WHERE o.ProductId = @ProductId
        ORDER BY o.Price ASC",
        new { ProductId = productId });

    var result = offers.Select(o =>
    {
        var unitPrice = UnitPriceCalculator.Calculate(o.Price, o.SizeValue, o.SizeUnit, o.PackQuantity);
        return new
        {
            o.CanonicalTitle,
            o.Source,
            o.Price,
            o.ImageUrl,
            o.ProductUrl,
            UnitPrice = unitPrice?.Amount,
            UnitLabel = unitPrice?.Label
        };
    });

    return Results.Ok(result);
});

// "Benzer ürünler": for Mobilya, other products in the exact same leaf category whose
// title clears a (looser) similarity bar. For Market, the pool is widened to the whole
// subtree under the nearest top-level bucket (e.g. "Süt, Kahvaltılık") — its category tree
// runs 2-4 levels deep and many leaves hold a single product (e.g. "Eski Kaşar Peyniri"),
// so leaf-only matching often finds nothing even though a sibling cheese type — or a
// different-but-related breakfast item like jam — clearly exists. Same-leaf candidates are
// always included (same specific grocery type, regardless of title wording); the rest of
// the widened bucket is included too, ranked below same-leaf/text-similar items, so a cheese
// product surfaces both other cheeses AND other "kahvaltılık" staples.
app.MapGet("/api/products/{productId}/similar", async (int productId) =>
{
    using var db = new SqlConnection(connectionString);

    var target = await db.QuerySingleOrDefaultAsync<TargetProductRow>(@"
        SELECT p.Name, p.CategoryId, root.Name AS SectorName
        FROM Products p
        LEFT JOIN Categories c ON c.Id = p.CategoryId
        LEFT JOIN Categories root ON root.Id = c.SectorId
        WHERE p.Id = @Id",
        new { Id = productId });

    if (target is null) return Results.NotFound();

    // No reliable category means no reliable "nitelik" (attribute) signal to compare on —
    // showing text-only matches across unrelated categories is exactly what produced false
    // positives like a bleach product matching bottled water. Safer to show nothing.
    if (target.CategoryId is null) return Results.Ok(Array.Empty<object>());

    var isMobilya = target.SectorName == SectorClassifier.MobilyaSectorName;

    var scopeCategoryId = target.CategoryId.Value;
    if (!isMobilya)
    {
        scopeCategoryId = await db.QuerySingleOrDefaultAsync<int?>(@"
            ;WITH Ancestors AS (
                SELECT Id, ParentCategoryId, SectorId, 0 AS Depth FROM Categories WHERE Id = @CategoryId
                UNION ALL
                SELECT c.Id, c.ParentCategoryId, c.SectorId, a.Depth + 1
                FROM Categories c JOIN Ancestors a ON c.Id = a.ParentCategoryId
            )
            SELECT TOP 1 Id FROM Ancestors WHERE ParentCategoryId = SectorId ORDER BY Depth ASC",
            new { target.CategoryId }) ?? target.CategoryId.Value;
    }

    var candidates = await db.QueryAsync<ProductListRow>(@"
        WITH CategoryTree AS (
            SELECT Id FROM Categories WHERE Id = @ScopeCategoryId
            UNION ALL
            SELECT c.Id FROM Categories c JOIN CategoryTree ct ON c.ParentCategoryId = ct.Id
        )
        SELECT p.Id, p.Name AS CanonicalTitle, b.Name AS Brand,
               MIN(o.Price) AS LowestPrice,
               COUNT(o.Id) AS OfferCount,
               p.ImageUrl,
               p.SizeValue, p.SizeUnit, p.PackQuantity, p.CategoryId,
               (SELECT TOP 1 o2.RegularPrice FROM Offers o2 WHERE o2.ProductId = p.Id ORDER BY o2.Price ASC) AS OldPrice
        FROM Products p
        JOIN Brands b ON b.Id = p.BrandId
        JOIN Offers o ON o.ProductId = p.Id
        WHERE p.Id <> @ProductId
        AND p.CategoryId IN (SELECT Id FROM CategoryTree)
        GROUP BY p.Id, p.Name, b.Name, p.ImageUrl, p.SizeValue, p.SizeUnit, p.PackQuantity, p.CategoryId",
        new { ProductId = productId, ScopeCategoryId = scopeCategoryId });

    var threshold = isMobilya ? 0.40 : 0.60;

    var scored = candidates.Select(c => new
    {
        Product = c,
        Score = TitleSimilarity.Calculate(c.CanonicalTitle, target.Name, c.Brand),
        SameCategory = c.CategoryId == target.CategoryId
    });

    // Mobilya's candidates are already scoped to the exact leaf, so the text-similarity bar
    // still applies. Market's candidates are the widened top-level bucket — everything in it
    // is eligible; the ordering below is what keeps same-leaf/text-similar items on top and
    // pushes purely bucket-related items ("diğer kahvaltılık ürünleri") to the back.
    var eligible = isMobilya ? scored.Where(x => x.Score >= threshold) : scored;

    var similar = eligible
        .OrderByDescending(x => x.SameCategory)
        .ThenByDescending(x => x.Score)
        .Take(10)
        .Select(x =>
        {
            var unitPrice = UnitPriceCalculator.Calculate(x.Product.LowestPrice, x.Product.SizeValue, x.Product.SizeUnit, x.Product.PackQuantity);
            return new
            {
                x.Product.Id,
                x.Product.CanonicalTitle,
                x.Product.Brand,
                x.Product.LowestPrice,
                x.Product.OfferCount,
                x.Product.ImageUrl,
                x.SameCategory,
                UnitPrice = unitPrice?.Amount,
                UnitLabel = unitPrice?.Label
            };
        });

    return Results.Ok(similar);
});

app.MapGet("/api/products", async (
    string? q,
    string? brand,
    string? category,
    string? sort,
    decimal? minPrice,
    decimal? maxPrice,
    string[]? attr,
    bool? onSale) =>
{
    var orderBy = sort switch
    {
        "price_desc" => "LowestPrice DESC",
        "price_asc" => "LowestPrice ASC",
        "name_desc" => "CanonicalTitle DESC",
        "newest" => "p.CreatedAt DESC",
        _ => "CanonicalTitle ASC"
    };

    var sql = $@"{SearchCtes(category, q)}
        SELECT p.Id, p.Name AS CanonicalTitle, b.Name AS Brand,
               MIN(o.Price) AS LowestPrice,
               COUNT(o.Id) AS OfferCount,
               p.ImageUrl,
               p.SizeValue, p.SizeUnit, p.PackQuantity, p.CategoryId,
               (SELECT TOP 1 o2.RegularPrice FROM Offers o2 WHERE o2.ProductId = p.Id ORDER BY o2.Price ASC) AS OldPrice
        FROM Products p
        JOIN Brands b ON b.Id = p.BrandId
        JOIN Offers o ON o.ProductId = p.Id
        WHERE 1=1";

    var parameters = new DynamicParameters();

    if (!string.IsNullOrWhiteSpace(q))
    {
        // Whole-word match (see WordBoundaryLike) — a plain substring LIKE would match "su"
        // inside "Supreme"/"Sun"/"Sugar" and pull in hair dye/sunscreen for a water search.
        // A search word can also name a category (e.g. "mobilya") — matching it against
        // Categories.Name too (via MatchingCategoriesByName) surfaces that category's whole
        // subtree, not just products whose title happens to contain the word.
        sql += $" AND ({WordBoundaryLike("p.Name")} OR {WordBoundaryLike("b.Name")} OR p.CategoryId IN (SELECT Id FROM MatchingCategoriesByName))";
        parameters.Add("Query", q.Trim());
    }

    if (!string.IsNullOrWhiteSpace(brand))
    {
        sql += " AND b.Name = @Brand";
        parameters.Add("Brand", brand);
    }

    if (!string.IsNullOrWhiteSpace(category))
    {
        sql += " AND p.CategoryId IN (SELECT Id FROM CategoryTree)";
        parameters.Add("CategorySlug", category);
    }

    if (onSale == true)
    {
        sql += " AND EXISTS (SELECT 1 FROM Offers o3 WHERE o3.ProductId = p.Id AND o3.RegularPrice IS NOT NULL AND o3.RegularPrice > o3.Price)";
    }

    // Each "Name:Value" pair (e.g. "Renk:Bej") groups by AttributeName: multiple values for
    // the SAME name are OR'd (picking "Bej" and "Siyah" shows either), different names are
    // AND'd (Renk:Bej + Özellik:Organik requires both) — standard faceted-filter semantics.
    if (attr is not null && attr.Length > 0)
    {
        var byName = attr
            .Select(a => a.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], parts => parts[1])
            .ToList();

        for (int i = 0; i < byName.Count; i++)
        {
            sql += $@" AND EXISTS (
                SELECT 1 FROM ProductAttributes pa
                WHERE pa.ProductId = p.Id AND pa.AttributeName = @AttrName{i} AND pa.AttributeValue IN @AttrValues{i})";
            parameters.Add($"AttrName{i}", byName[i].Key);
            parameters.Add($"AttrValues{i}", byName[i].ToList());
        }
    }

    sql += " GROUP BY p.Id, p.Name, b.Name, p.ImageUrl, p.SizeValue, p.SizeUnit, p.PackQuantity, p.CategoryId, p.CreatedAt";

    if (minPrice.HasValue)
    {
        sql += " HAVING MIN(o.Price) >= @MinPrice";
        parameters.Add("MinPrice", minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        sql += minPrice.HasValue ? " AND MIN(o.Price) <= @MaxPrice" : " HAVING MIN(o.Price) <= @MaxPrice";
        parameters.Add("MaxPrice", maxPrice.Value);
    }

    sql += $" ORDER BY {orderBy}";

    using var db = new SqlConnection(connectionString);
    var masterProducts = await db.QueryAsync<ProductListRow>(sql, parameters);

    var result = masterProducts.Select(mp =>
    {
        var unitPrice = UnitPriceCalculator.Calculate(mp.LowestPrice, mp.SizeValue, mp.SizeUnit, mp.PackQuantity);
        var discountPercent = mp.OldPrice.HasValue && mp.OldPrice > mp.LowestPrice
            ? Math.Round((mp.OldPrice.Value - mp.LowestPrice) / mp.OldPrice.Value * 100, 0)
            : (decimal?)null;
        return new
        {
            mp.Id,
            mp.CanonicalTitle,
            mp.Brand,
            mp.LowestPrice,
            mp.OfferCount,
            mp.ImageUrl,
            UnitPrice = unitPrice?.Amount,
            UnitLabel = unitPrice?.Label,
            OldPrice = discountPercent.HasValue ? mp.OldPrice : null,
            DiscountPercent = discountPercent
        };
    });

    return Results.Ok(result);
});

app.Run();

// Plain POCOs instead of ValueTuples for these Dapper query results — Dapper maps tuples
// past 7 elements onto the nested "Rest" field ValueTuple<> generates, which it doesn't
// bind reliably, silently leaving later columns (price, offer count...) unset.
record OfferRow(string CanonicalTitle, string Source, decimal Price, string? ImageUrl,
    string ProductUrl, decimal? SizeValue, string? SizeUnit, int PackQuantity);

record ProductListRow(int Id, string CanonicalTitle, string? Brand, decimal LowestPrice,
    int OfferCount, string? ImageUrl, decimal? SizeValue, string? SizeUnit, int PackQuantity, int? CategoryId,
    decimal? OldPrice);

record TargetProductRow(string Name, int? CategoryId, string? SectorName);