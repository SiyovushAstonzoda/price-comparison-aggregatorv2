using Dapper;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Allow your frontend (running on a different port/file) to call this API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

string connectionString = "Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;";

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

app.MapGet("/api/products", async () =>
{
    using var db = new SqlConnection(connectionString);
    var masterProducts = await db.QueryAsync(@"
        SELECT mp.Id, mp.CanonicalTitle, mp.Brand,
               MIN(p.Price) AS LowestPrice,
               COUNT(p.Id) AS OfferCount
        FROM MasterProducts mp
        JOIN Products p ON p.MasterProductId = mp.Id
        GROUP BY mp.Id, mp.CanonicalTitle, mp.Brand
        ORDER BY mp.CanonicalTitle");

    return Results.Ok(masterProducts);
});

app.MapGet("/api/products/search", async (string q) =>
{
    using var db = new SqlConnection(connectionString);
    var results = await db.QueryAsync(@"
        SELECT mp.Id, mp.CanonicalTitle, mp.Brand,
               MIN(p.Price) AS LowestPrice,
               COUNT(p.Id) AS OfferCount
        FROM MasterProducts mp
        JOIN Products p ON p.MasterProductId = mp.Id
        WHERE mp.CanonicalTitle LIKE @Query
        GROUP BY mp.Id, mp.CanonicalTitle, mp.Brand
        ORDER BY mp.CanonicalTitle",
        new { Query = $"%{q}%" });

    return Results.Ok(results);
});

app.Run();