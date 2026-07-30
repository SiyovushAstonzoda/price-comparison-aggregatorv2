using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
var app = builder.Build();
app.UseCors("AllowFrontend");
var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "Frontend");
var distPath = Path.Combine(frontendPath, "dist");
if (Directory.Exists(distPath)) frontendPath = distPath;
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(frontendPath) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(frontendPath) });
string connectionString = "Server=localhost\\SQLEXPRESS;Database=Aggregator;User Id=sa;Password=123456;TrustServerCertificate=True;";

// Marka filtresinde kullanilmak uzere tum marka adlarini listeler.
app.MapGet("/api/brands", async (int? categoryId) =>
{
    using var db = new SqlConnection(connectionString);
    const string allBrandsSql = "SELECT Name FROM Brands ORDER BY Name";
    const string categoryBrandsSql = @"WITH CategoryTree AS (
        SELECT Id FROM Categories WHERE Id = @CategoryId
        UNION ALL
        SELECT child.Id FROM Categories child JOIN CategoryTree tree ON child.ParentCategoryId = tree.Id
    )
    SELECT DISTINCT b.Name FROM Brands b
    JOIN Products p ON p.BrandId = b.Id
    JOIN CategoryTree tree ON tree.Id = p.CategoryId
    ORDER BY b.Name OPTION (MAXRECURSION 100)";

    var brands = await db.QueryAsync<string>(
        categoryId.HasValue ? categoryBrandsSql : allBrandsSql,
        new { CategoryId = categoryId });
    return Results.Ok(brands);
});
// Secilen urunun aktif magaza tekliflerini, fiyatlarini ve urun baglantilarini listeler.
app.MapGet("/api/products/{productId}", async (int productId) => { using var db=new SqlConnection(connectionString); return Results.Ok(await db.QueryAsync(@"SELECT p.Name AS CanonicalTitle, s.Name AS Source, o.Price, sp.ExternalImageUrl AS ImageUrl, sp.ExternalUrl AS ProductUrl FROM Products p JOIN Offers o ON o.ProductId=p.Id JOIN Sellers s ON s.Id=o.SellerId JOIN SellerProducts sp ON sp.Id=o.SellerProductId WHERE p.Id=@ProductId AND o.IsActive=1 ORDER BY o.Price",new{ProductId=productId})); });
// Ana urun ekranini arama, marka, fiyat ve siralama filtreleriyle besler.
app.MapGet("/api/products", async (string? q,string? brand,string? sort,decimal? minPrice,decimal? maxPrice) => { using var db=new SqlConnection(connectionString); var sql="SELECT p.Id,p.Name AS CanonicalTitle,b.Name AS Brand,MIN(o.Price) AS LowestPrice,COUNT(o.Id) AS OfferCount,MAX(sp.ExternalImageUrl) AS ImageUrl FROM Products p JOIN Brands b ON b.Id=p.BrandId JOIN Offers o ON o.ProductId=p.Id JOIN SellerProducts sp ON sp.Id=o.SellerProductId WHERE o.IsActive=1"; var a=new DynamicParameters(); if(!string.IsNullOrWhiteSpace(q)){sql+=" AND (p.Name LIKE @q OR b.Name LIKE @q)";a.Add("q",$"%{q.Trim()}% ".TrimEnd());} if(!string.IsNullOrWhiteSpace(brand)){sql+=" AND b.Name=@brand";a.Add("brand",brand);} sql+=" GROUP BY p.Id,p.Name,b.Name"; if(minPrice.HasValue){sql+=" HAVING MIN(o.Price)>=@min";a.Add("min",minPrice);} if(maxPrice.HasValue){sql+=minPrice.HasValue?" AND MIN(o.Price)<=@max":" HAVING MIN(o.Price)<=@max";a.Add("max",maxPrice);} sql+=" ORDER BY "+(sort=="price_desc"?"LowestPrice DESC":sort=="price_asc"?"LowestPrice ASC":sort=="name_desc"?"CanonicalTitle DESC":"CanonicalTitle ASC"); return Results.Ok(await db.QueryAsync(sql,a)); });
// Aranan urun adina gore aktif teklifleri fiyat sirasi ile listeler.
app.MapGet("/api/deals", async (string q) => { using var db=new SqlConnection(connectionString); var rows=await db.QueryAsync(@"SELECT p.Id AS MasterProductId,p.Name AS CanonicalTitle,b.Name AS Brand,s.Name AS Source,o.Price,sp.ExternalImageUrl AS ImageUrl,sp.ExternalUrl AS ProductUrl FROM Products p JOIN Brands b ON b.Id=p.BrandId JOIN Offers o ON o.ProductId=p.Id JOIN Sellers s ON s.Id=o.SellerId JOIN SellerProducts sp ON sp.Id=o.SellerProductId WHERE o.IsActive=1 AND p.Name LIKE @q ORDER BY o.Price",new{q=$"%{q}%"}); return Results.Ok(new{primaryUnit="price",results=rows,otherUnitResults=Array.Empty<object>()}); });
// ParentCategoryId degerine gore kok veya alt kategori dugumlerini listeler.
app.MapGet("/api/categories", async (int? parentId) =>
{
    using var db = new SqlConnection(connectionString);
    var categories = await db.QueryAsync(@"WITH CategoryTree AS (
        SELECT Id AS RootCategoryId, Id AS DescendantCategoryId FROM Categories
        UNION ALL
        SELECT tree.RootCategoryId, child.Id
        FROM CategoryTree tree
        JOIN Categories child ON child.ParentCategoryId = tree.DescendantCategoryId
    )
    SELECT c.Id, c.Name, c.ParentCategoryId,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM Categories child WHERE child.ParentCategoryId = c.Id) THEN 1 ELSE 0 END AS bit) AS HasChildren,
        COUNT(DISTINCT p.Id) AS ProductCount
    FROM Categories c
    LEFT JOIN CategoryTree tree ON tree.RootCategoryId = c.Id
    LEFT JOIN Products p ON p.CategoryId = tree.DescendantCategoryId
    WHERE c.IsActive = 1
      AND ((@ParentId IS NULL AND c.ParentCategoryId IS NULL) OR c.ParentCategoryId = @ParentId)
    GROUP BY c.Id, c.Name, c.ParentCategoryId
    ORDER BY c.Name OPTION (MAXRECURSION 100)", new { ParentId = parentId });
    return Results.Ok(categories);
});
<<<<<<< Updated upstream

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
=======
// Secilen yaprak kategoriye bagli urunleri teklif, marka ve gorsel bilgileriyle listeler.
app.MapGet("/api/categories/{categoryId:int}/products", async (int categoryId) =>
>>>>>>> Stashed changes
{
    var categoryMapper = new CategoryMapper(connectionString);

    var resolvedCategoryId = categoryId
        ?? await categoryMapper.ResolveAsync(q ?? "", null);

    if (!resolvedCategoryId.HasValue)
    {
        return Results.Ok(new { primaryUnit = "weight_or_volume", results = Array.Empty<object>(), otherUnitResults = Array.Empty<object>() });
    }

    using var db = new SqlConnection(connectionString);
<<<<<<< Updated upstream

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
=======
    var products = await db.QueryAsync(@"WITH CategoryTree AS (
        SELECT Id FROM Categories WHERE Id = @CategoryId
        UNION ALL SELECT c.Id FROM Categories c JOIN CategoryTree tree ON c.ParentCategoryId = tree.Id
    )
    SELECT p.Id, p.Name AS CanonicalTitle, b.Name AS Brand, MIN(o.Price) AS LowestPrice,
           COUNT(DISTINCT o.SellerId) AS OfferCount, MAX(sp.ExternalImageUrl) AS ImageUrl
    FROM Products p JOIN Brands b ON b.Id=p.BrandId JOIN Offers o ON o.ProductId=p.Id AND o.IsActive=1
    JOIN Sellers s ON s.Id=o.SellerId JOIN SellerProducts sp ON sp.Id=o.SellerProductId
    WHERE p.CategoryId = @CategoryId
    GROUP BY p.Id,p.Name,b.Name ORDER BY LowestPrice",new{CategoryId=categoryId});
    return Results.Ok(products);
});
app.Run();
>>>>>>> Stashed changes
