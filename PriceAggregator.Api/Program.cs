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

var app = builder.Build();

app.UseCors("AllowFrontend");

var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "Frontend");
var distPath = Path.Combine(frontendPath, "dist");
if (Directory.Exists(distPath))
{
    frontendPath = distPath;
}

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(frontendPath)
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(frontendPath)
});

// Veritabanı bağlantı bilgilerini appsettings.json dosyasından değiştirin
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

app.MapGet("/api/brands", async () =>
{
    using var db = new SqlConnection(connectionString);
    var brands = await db.QueryAsync<string>(@"
        SELECT DISTINCT mp.Brand
        FROM MasterProducts mp
        JOIN Products p ON p.MasterProductId = mp.Id
        WHERE mp.Brand IS NOT NULL AND mp.Brand <> ''
        ORDER BY mp.Brand");
    return Results.Ok(brands);
});

app.MapGet("/api/products/{masterId}", async (int masterId) =>
{
    using var db = new SqlConnection(connectionString);
    var offers = await db.QueryAsync(@"
        SELECT mp.CanonicalTitle, p.Source, p.Price, p.ImageUrl, p.ProductUrl
        FROM MasterProducts mp
        JOIN Products p ON p.MasterProductId = mp.Id
        WHERE mp.Id = @MasterId
        ORDER BY p.Price ASC",
        new { MasterId = masterId });

    return Results.Ok(offers);
});

app.MapGet("/api/products", async (
    string? q,
    string? brand,
    string? sort,
    decimal? minPrice,
    decimal? maxPrice) =>
{
    var orderBy = sort switch
    {
        "price_desc" => "LowestPrice DESC",
        "price_asc" => "LowestPrice ASC",
        "name_desc" => "CanonicalTitle DESC",
        _ => "CanonicalTitle ASC"
    };

    var sql = $@"
        SELECT mp.Id, mp.CanonicalTitle, mp.Brand,
               MIN(p.Price) AS LowestPrice,
               COUNT(p.Id) AS OfferCount,
               MAX(p.ImageUrl) AS ImageUrl
        FROM MasterProducts mp
        JOIN Products p ON p.MasterProductId = mp.Id
        WHERE 1=1";

    var parameters = new DynamicParameters();

    if (!string.IsNullOrWhiteSpace(q))
    {
        sql += " AND (mp.CanonicalTitle LIKE @Query OR mp.Brand LIKE @Query)";
        parameters.Add("Query", $"%{q.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(brand))
    {
        sql += " AND mp.Brand = @Brand";
        parameters.Add("Brand", brand);
    }

    sql += " GROUP BY mp.Id, mp.CanonicalTitle, mp.Brand";

    if (minPrice.HasValue)
    {
        sql += " HAVING MIN(p.Price) >= @MinPrice";
        parameters.Add("MinPrice", minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        sql += minPrice.HasValue ? " AND MIN(p.Price) <= @MaxPrice" : " HAVING MIN(p.Price) <= @MaxPrice";
        parameters.Add("MaxPrice", maxPrice.Value);
    }

    sql += $" ORDER BY {orderBy}";

    using var db = new SqlConnection(connectionString);
    var masterProducts = await db.QueryAsync(sql, parameters);
    return Results.Ok(masterProducts);
});

app.MapGet("/api/deals", async (string? q, int? categoryId) =>
{
    var categoryMapper = new CategoryMapper(connectionString);

    var resolvedCategoryId = categoryId
        ?? await categoryMapper.ResolveAsync(q ?? "", null);

    if (!resolvedCategoryId.HasValue)
    {
        return Results.Ok(new { primaryUnit = "weight_or_volume", results = Array.Empty<object>(), otherUnitResults = Array.Empty<object>() });
    }

    using var db = new SqlConnection(connectionString);

    var rows = await db.QueryAsync<DealRow>(@"
        SELECT mp.Id AS MasterProductId, mp.CanonicalTitle, mp.Brand,
               mp.SizeValue, mp.SizeUnit, mp.PackQuantity,
               p.Source, p.Price, p.ImageUrl, p.ProductUrl
        FROM MasterProducts mp
        JOIN Products p ON p.MasterProductId = mp.Id
        WHERE mp.CanonicalCategoryId = @CategoryId",
        new { CategoryId = resolvedCategoryId });

    var withUnitPrice = rows.Select(r => new
    {
        r.MasterProductId,
        r.CanonicalTitle,
        r.Brand,
        r.Source,
        r.Price,
        r.ImageUrl,
        r.ProductUrl,
        r.SizeUnit,
        TotalSize = (r.SizeValue ?? 0) * r.PackQuantity,
        PricePerUnit = (r.SizeValue is null or 0) ? (decimal?)null
            : r.Price / ((r.SizeValue.Value * r.PackQuantity) / (r.SizeUnit == "G" || r.SizeUnit == "ML" ? 1000m : 1m))
    }).Where(r => r.PricePerUnit != null).ToList();

    if (withUnitPrice.Count == 0)
        return Results.Ok(Array.Empty<object>());

    // Rank ONLY within the dominant unit type for this category (weight or volume),
    // since mixing "price per kg" with "price per piece" isn't a valid comparison.
    var weightOrVolume = withUnitPrice.Where(r => r.SizeUnit == "G" || r.SizeUnit == "ML").ToList();
    var byCount = withUnitPrice.Where(r => r.SizeUnit == "ADET").ToList();

    var primaryGroup = weightOrVolume.Count >= byCount.Count ? weightOrVolume : byCount;
    var secondaryGroup = weightOrVolume.Count >= byCount.Count ? byCount : weightOrVolume;

    var sortedSizes = primaryGroup.Select(r => r.TotalSize).OrderBy(s => s).ToList();
    var median = sortedSizes.Count > 0 ? sortedSizes[sortedSizes.Count / 2] : 0;
    var cap = median * 3;

    var filteredPrimary = primaryGroup.Where(r => cap == 0 || r.TotalSize <= cap)
        .OrderBy(r => r.PricePerUnit).ToList();

    return Results.Ok(new
    {
        primaryUnit = weightOrVolume.Count >= byCount.Count ? "weight_or_volume" : "count",
        results = filteredPrimary,
        // count-based (or weight/volume) items shown separately, not ranked against primary
        otherUnitResults = secondaryGroup.OrderBy(r => r.PricePerUnit).ToList()
    });
});

app.MapGet("/api/categories", async () =>
{
    using var db = new SqlConnection(connectionString);
    var rows = await db.QueryAsync<CategoryDto>(@"
        SELECT c.Id, c.Name, c.Slug, c.Icon,
               COUNT(mp.Id) AS ProductCount
        FROM Categories c
        LEFT JOIN MasterProducts mp ON mp.CanonicalCategoryId = c.Id
        WHERE c.ParentCategoryId IS NULL
        GROUP BY c.Id, c.Name, c.Slug, c.Icon, c.DisplayOrder
        HAVING COUNT(mp.Id) > 0
        ORDER BY c.DisplayOrder");

    return Results.Ok(rows);
});

app.MapGet("/api/admin/category-review", async () =>
{
    using var db = new SqlConnection(connectionString);
    //var pending = await db.QueryAsync(@"
    var pending = await db.QueryAsync<CategoryReviewDto>(@"
        SELECT rq.Id, rq.RawCategoryText, rq.Score, c.Name AS SuggestedCategory, rq.SuggestedCategoryId
        FROM CategoryReviewQueue rq
        LEFT JOIN Categories c ON c.Id = rq.SuggestedCategoryId
        WHERE rq.Status = 'Pending'
        ORDER BY rq.Score DESC");
    return Results.Ok(pending);
});

app.MapPost("/api/admin/category-review/{id}/approve", async (int id) =>
{
    using var db = new SqlConnection(connectionString);
    var row = await db.QuerySingleOrDefaultAsync<(string RawCategoryText, int? SuggestedCategoryId)>(
        "SELECT RawCategoryText, SuggestedCategoryId FROM CategoryReviewQueue WHERE Id = @Id", new { Id = id });

    if (row.SuggestedCategoryId.HasValue)
    {
        await db.ExecuteAsync(
            "INSERT INTO CategoryRawMap (RawCategoryText, CategoryId) VALUES (@Raw, @CatId)",
            new { Raw = row.RawCategoryText, CatId = row.SuggestedCategoryId });
    }
    await db.ExecuteAsync("UPDATE CategoryReviewQueue SET Status = 'Approved' WHERE Id = @Id", new { Id = id });
    return Results.Ok();
});

app.MapPost("/api/admin/category-review/{id}/reject", async (int id) =>
{
    using var db = new SqlConnection(connectionString);
    await db.ExecuteAsync("UPDATE CategoryReviewQueue SET Status = 'Rejected' WHERE Id = @Id", new { Id = id });
    return Results.Ok();
});

app.Run();

public record DealRow(
    int MasterProductId,
    string CanonicalTitle,
    string? Brand,
    decimal? SizeValue,
    string? SizeUnit,
    int PackQuantity,
    string Source,
    decimal Price,
    string? ImageUrl,
    string? ProductUrl);

public record CategoryDto(int Id, string Name, string Slug, string? Icon, int ProductCount);
public record CategoryReviewDto(int Id, string RawCategoryText, double Score, string? SuggestedCategory, int? SuggestedCategoryId); //new