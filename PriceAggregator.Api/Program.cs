using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using PriceAggregator.Api.Cosmetics;

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

string connectionString = "Server=localhost,1433;Database=Tutumlu;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;";

// ── Mevcut genel endpoint'ler (değiştirilmedi) ──────────────────────────────

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
        "price_asc"  => "LowestPrice ASC",
        "name_desc"  => "CanonicalTitle DESC",
        _            => "CanonicalTitle ASC"
    };

    var sql = $@"
        SELECT mp.Id, mp.CanonicalTitle, mp.Brand,
                MIN(p.Price) AS LowestPrice,
                COUNT(p.Id) AS OfferCount,
                MAX(p.ImageUrl) AS ImageUrl,
                (SELECT TOP 1 p2.ProductUrl FROM Products p2 WHERE p2.MasterProductId = mp.Id ORDER BY p2.Price ASC, p2.Id ASC) AS ProductUrl
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

// ── Kozmetik endpoint'leri ─────────────────────────────────────────────────
app.MapCosmeticsEndpoints(connectionString);

app.Run();
