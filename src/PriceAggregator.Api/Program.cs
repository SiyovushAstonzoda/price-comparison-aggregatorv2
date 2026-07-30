using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using PriceAggregator.Application.Services;
using PriceAggregator.Infrastructure.Repositories;

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

var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Frontend"));
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

string connectionString = builder.Configuration.GetConnectionString("Default") 
    ?? "Server=127.0.0.1,1433;Database=Aggregator;User Id=sa;Password=StrongPassword123!;TrustServerCertificate=True;";

// 1. /api/categories
app.MapGet("/api/categories", async () =>
{
    using var db = new SqlConnection(connectionString);
    var sql = @"
        SELECT c.Id, c.Name, c.Slug, 
               (SELECT COUNT(*) FROM Products p WHERE p.CategoryId = c.Id OR p.CategoryId IN (SELECT Id FROM Categories WHERE ParentCategoryId = c.Id)) AS ProductCount,
               c.ParentCategoryId
        FROM Categories c
        WHERE c.IsActive = 1
        ORDER BY c.ParentCategoryId, c.Name";

    var rows = await db.QueryAsync(sql);
    
    // Group into hierarchy
    var allCategories = rows.Select(r => new {
        Id = (int)r.Id,
        Name = (string)r.Name,
        Slug = (string)r.Slug,
        ProductCount = (int)r.ProductCount,
        ParentCategoryId = (int?)r.ParentCategoryId
    }).ToList();

    var topLevel = allCategories.Where(c => c.ParentCategoryId == null).Select(c => new CategoryDto(
        c.Id, c.Name, c.Slug, null, c.ProductCount, 
        allCategories.Where(child => child.ParentCategoryId == c.Id)
                     .Select(child => new CategoryDto(child.Id, child.Name, child.Slug, null, child.ProductCount))
                     .ToList()
    )).ToList();

    return Results.Ok(topLevel);
});

// 2. /api/brands
app.MapGet("/api/brands", async (int? categoryId) =>
{
    using var db = new SqlConnection(connectionString);
    var sql = @"
        SELECT DISTINCT b.Name
        FROM Brands b
        JOIN Products p ON p.BrandId = b.Id
        WHERE 1=1";
    
    var parameters = new DynamicParameters();
    if (categoryId.HasValue)
    {
        sql += " AND p.CategoryId = @CategoryId";
        parameters.Add("CategoryId", categoryId.Value);
    }
    
    sql += " ORDER BY b.Name";
    var brands = await db.QueryAsync<string>(sql, parameters);
    return Results.Ok(brands);
});

// 3. /api/products
app.MapGet("/api/products", async (
    string? q,
    string? brand,
    string? sort,
    decimal? minPrice,
    decimal? maxPrice,
    int? categoryId) =>
{
    var orderBy = sort switch
    {
        "price_desc" => "LowestPrice DESC",
        "price_asc"  => "LowestPrice ASC",
        "name_desc"  => "p.Name DESC",
        _            => "p.Name ASC"
    };

    var sql = $@"
        SELECT p.Id, p.Name, b.Name AS Brand,
               MIN(o.TotalCost) AS LowestPrice,
               COUNT(o.Id) AS OfferCount,
               MAX(p.ImageUrl) AS ImageUrl,
               CAST(NULL AS NVARCHAR(MAX)) AS ProductUrl
        FROM Products p
        LEFT JOIN Brands b ON p.BrandId = b.Id
        LEFT JOIN Offers o ON o.ProductId = p.Id AND o.IsActive = 1
        WHERE 1=1";

    var parameters = new DynamicParameters();

    if (categoryId.HasValue)
    {
        sql += " AND (p.CategoryId = @CategoryId OR p.CategoryId IN (SELECT Id FROM Categories WHERE ParentCategoryId = @CategoryId))";
        parameters.Add("CategoryId", categoryId.Value);
    }

    if (!string.IsNullOrWhiteSpace(q))
    {
        sql += " AND (p.Name LIKE @Query OR b.Name LIKE @Query)";
        parameters.Add("Query", $"%{q.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(brand))
    {
        sql += " AND b.Name = @Brand";
        parameters.Add("Brand", brand);
    }

    sql += " GROUP BY p.Id, p.Name, b.Name";

    if (minPrice.HasValue)
    {
        sql += " HAVING MIN(o.TotalCost) >= @MinPrice";
        parameters.Add("MinPrice", minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        sql += minPrice.HasValue ? " AND MIN(o.TotalCost) <= @MaxPrice" : " HAVING MIN(o.TotalCost) <= @MaxPrice";
        parameters.Add("MaxPrice", maxPrice.Value);
    }

    sql += $" ORDER BY {orderBy}";

    using var db = new SqlConnection(connectionString);
    var products = await db.QueryAsync<ProductListDto>(sql, parameters);
    return Results.Ok(products);
});

// 4. /api/products/{id} (Offers for a product)
app.MapGet("/api/products/{id}", async (int id) =>
{
    using var db = new SqlConnection(connectionString);
    var sql = @"
        SELECT s.Name AS SellerName, o.Price, o.CargoPrice, o.TotalCost, 
               sp.ExternalUrl, sp.ExternalImageUrl, s.Rating AS SellerRating
        FROM Offers o
        JOIN Sellers s ON o.SellerId = s.Id
        JOIN SellerProducts sp ON o.SellerProductId = sp.Id
        WHERE o.ProductId = @ProductId AND o.IsActive = 1
        ORDER BY o.TotalCost ASC";
        
    var offers = await db.QueryAsync<OfferDto>(sql, new { ProductId = id });
    return Results.Ok(offers);
});

// 5. /api/products/{id}/similar (Recommendation based on Jaccard Similarity)
app.MapGet("/api/products/{id}/similar", async (int id) =>
{
    using var db = new SqlConnection(connectionString);

    // 1. Get the target product's Name and CategoryId
    var targetInfo = await db.QuerySingleOrDefaultAsync(@"
        SELECT p.Name, p.CategoryId, c.ParentCategoryId 
        FROM Products p
        JOIN Categories c ON p.CategoryId = c.Id
        WHERE p.Id = @ProductId", new { ProductId = id });

    if (targetInfo == null) return Results.NotFound();

    // Check if it's in the Kozmetik category (CategoryId = 2 or ParentCategoryId = 2)
    // The user strictly requested this only for the Kozmetik category!
    if (targetInfo.CategoryId != 2 && targetInfo.ParentCategoryId != 2)
    {
        return Results.Ok(new List<ProductListDto>()); // return empty list if not cosmetics
    }

    string targetTitle = targetInfo.Name;
    int categoryId = targetInfo.CategoryId;

    // 2. Fetch other products in the same category
    var candidates = await db.QueryAsync<ProductListDto>(@"
        SELECT p.Id, p.Name, b.Name AS Brand,
               MIN(o.TotalCost) AS LowestPrice,
               COUNT(o.Id) AS OfferCount,
               MAX(p.ImageUrl) AS ImageUrl,
               CAST(NULL AS NVARCHAR(MAX)) AS ProductUrl
        FROM Products p
        LEFT JOIN Brands b ON p.BrandId = b.Id
        LEFT JOIN Offers o ON o.ProductId = p.Id AND o.IsActive = 1
        WHERE p.CategoryId = @CategoryId AND p.Id != @ProductId
        GROUP BY p.Id, p.Name, b.Name", 
        new { CategoryId = categoryId, ProductId = id });

    // 3. Jaccard Similarity sorting
    var targetWords = new HashSet<string>(
        System.Text.RegularExpressions.Regex.Replace(targetTitle.ToLowerInvariant(), @"[^\w\s]", " ")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    var scoredCandidates = candidates.Select(c => {
        string cTitle = c.Name;
        var cWords = new HashSet<string>(
            System.Text.RegularExpressions.Regex.Replace(cTitle.ToLowerInvariant(), @"[^\w\s]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        int intersection = 0;
        foreach (var w in targetWords) if (cWords.Contains(w)) intersection++;
        int union = targetWords.Count + cWords.Count - intersection;
        double score = union == 0 ? 0 : (double)intersection / union;

        return new { Product = c, Score = score };
    })
    .OrderByDescending(x => x.Score)
    .ThenByDescending(x => x.Product.OfferCount)
    .ThenBy(x => x.Product.LowestPrice)
    .Select(x => x.Product)
    .Take(5)
    .ToList();

    return Results.Ok(scoredCandidates);
});

app.Run();
