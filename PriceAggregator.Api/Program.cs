using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwrootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

app.MapGet("/api/sources", async () =>
{
    using var db = new SqlConnection(connectionString);
    var sources = await db.QueryAsync<string>(@"
        SELECT DISTINCT Source
        FROM Products
        WHERE Source IS NOT NULL AND Source <> ''
        ORDER BY Source");
    return Results.Ok(sources);
});

app.MapGet("/api/brands", async () =>
{
    using var db = new SqlConnection(connectionString);
    var brands = await db.QueryAsync<string>(@"
        SELECT DISTINCT Brand
        FROM (
            SELECT mp.Brand
            FROM MasterProducts mp
            JOIN Products p ON p.MasterProductId = mp.Id
            WHERE mp.Brand IS NOT NULL AND mp.Brand <> '' AND p.Source NOT IN ('ikea', 'ozdilek')
            UNION
            SELECT COALESCE(NULLIF(mp.Brand, ''), p.Source) AS Brand
            FROM Products p
            LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id
            WHERE p.Source NOT IN ('ikea', 'ozdilek')
        ) b
        WHERE Brand IS NOT NULL AND Brand <> ''
        ORDER BY Brand");
    return Results.Ok(brands);
});

app.MapGet("/api/products/{id:int}", async (int id) =>
{
    using var db = new SqlConnection(connectionString);

    if (id > 0)
    {
        var offers = await db.QueryAsync(@"
            SELECT mp.CanonicalTitle, p.Source, p.Price, p.ImageUrl, p.ProductUrl
            FROM MasterProducts mp
            JOIN Products p ON p.MasterProductId = mp.Id
            WHERE mp.Id = @Id
            ORDER BY p.Price ASC",
            new { Id = id });
        return Results.Ok(offers);
    }

    var productId = Math.Abs(id);
    var single = await db.QueryAsync(@"
        SELECT p.Title AS CanonicalTitle, p.Source, p.Price, p.ImageUrl, p.ProductUrl
        FROM Products p
        WHERE p.Id = @ProductId",
        new { ProductId = productId });
    return Results.Ok(single);
});

app.MapGet("/api/products", async (
    string? q,
    string? brand,
    string? source,
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

    var sql = @"
        WITH ProductGroups AS (
            SELECT
                CASE WHEN p.MasterProductId IS NOT NULL THEN p.MasterProductId ELSE -p.Id END AS GroupId,
                COALESCE(mp.CanonicalTitle, p.Title) AS CanonicalTitle,
                COALESCE(NULLIF(mp.Brand, ''), p.Source) AS Brand,
                p.Price,
                p.ImageUrl,
                p.Source
            FROM Products p
            LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id
            WHERE p.Source NOT IN ('ikea', 'ozdilek')
        )
        SELECT
            GroupId AS Id,
            MAX(CanonicalTitle) AS CanonicalTitle,
            MAX(Brand) AS Brand,
            MIN(Price) AS LowestPrice,
            COUNT(*) AS OfferCount,
            MAX(ImageUrl) AS ImageUrl,
            STRING_AGG(Source, ',') AS Sources
        FROM ProductGroups
        WHERE 1 = 1";

    var parameters = new DynamicParameters();

    if (!string.IsNullOrWhiteSpace(q))
    {
        sql += " AND (CanonicalTitle LIKE @Query OR Brand LIKE @Query)";
        parameters.Add("Query", $"%{q.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(brand))
    {
        sql += " AND Brand = @Brand";
        parameters.Add("Brand", brand);
    }

    if (!string.IsNullOrWhiteSpace(source))
    {
        sql += " AND Source = @Source";
        parameters.Add("Source", source);
    }

    sql += " GROUP BY GroupId";

    if (minPrice.HasValue)
    {
        sql += " HAVING MIN(Price) >= @MinPrice";
        parameters.Add("MinPrice", minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        sql += minPrice.HasValue ? " AND MIN(Price) <= @MaxPrice" : " HAVING MIN(Price) <= @MaxPrice";
        parameters.Add("MaxPrice", maxPrice.Value);
    }

    sql += $" ORDER BY {orderBy}";

    using var db = new SqlConnection(connectionString);
    var masterProducts = await db.QueryAsync(sql, parameters);
    return Results.Ok(masterProducts);
});

app.MapGet("/api/mobilya/brands", async () =>
{
    using var db = new SqlConnection(connectionString);
    var brands = await db.QueryAsync<string>(@"
        SELECT DISTINCT Brand
        FROM (
            SELECT mp.Brand
            FROM MasterProducts mp
            JOIN Products p ON p.MasterProductId = mp.Id
            WHERE mp.Brand IS NOT NULL AND mp.Brand <> '' AND p.Source IN ('ikea', 'ozdilek')
            UNION
            SELECT COALESCE(NULLIF(mp.Brand, ''), CASE WHEN p.Source = 'ikea' THEN 'IKEA' WHEN p.Source = 'ozdilek' THEN 'Özdilek' ELSE p.Source END) AS Brand
            FROM Products p
            LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id
            WHERE p.Source IN ('ikea', 'ozdilek')
        ) b
        WHERE Brand IS NOT NULL AND Brand <> ''
        ORDER BY Brand");
    return Results.Ok(brands);
});

app.MapGet("/api/mobilya/products", async (
    string? q,
    string? brand,
    string? category,
    string? midCategory,
    string? subCategory,
    string? color,
    string? dimensions,
    string? productType,
    string? material,
    decimal? minPrice,
    decimal? maxPrice,
    string? sort) =>
{
    var orderBy = sort switch
    {
        "price_desc" => "Price DESC",
        "price_asc" => "Price ASC",
        "name_desc" => "Title DESC",
        _ => "Title ASC"
    };

    var sql = @"
        SELECT p.Id, p.ExternalId, p.Title, p.ImageUrl, p.Price, p.RegularPrice, p.ProductUrl,
               p.Category, p.MidCategory, p.SubCategory, p.Color, p.Dimensions, p.ProductType, p.Material,
               COALESCE(NULLIF(mp.Brand, ''), CASE WHEN p.Source = 'ikea' THEN 'IKEA' WHEN p.Source = 'ozdilek' THEN 'Özdilek' ELSE p.Source END) AS Brand, p.Source
        FROM Products p
        LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id
        WHERE p.Source IN ('ikea', 'ozdilek')";

    var parameters = new DynamicParameters();

    if (!string.IsNullOrWhiteSpace(q))
    {
        sql += @" AND (p.Title LIKE @Query OR p.Category LIKE @Query OR p.MidCategory LIKE @Query
                 OR p.SubCategory LIKE @Query OR p.ProductType LIKE @Query OR p.Material LIKE @Query OR mp.Brand LIKE @Query)";
        parameters.Add("Query", $"%{q.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(brand))
    {
        sql += " AND (mp.Brand = @Brand OR p.Source = @Brand OR (p.Source = 'ikea' AND @Brand = 'IKEA') OR (p.Source = 'ozdilek' AND (@Brand = 'Özdilek' OR @Brand = 'Ozdilek')))";
        parameters.Add("Brand", brand);
    }

    if (!string.IsNullOrWhiteSpace(category))
    {
        sql += " AND p.Category = @Category";
        parameters.Add("Category", category);
    }

    if (!string.IsNullOrWhiteSpace(midCategory))
    {
        sql += " AND p.MidCategory = @MidCategory";
        parameters.Add("MidCategory", midCategory);
    }

    if (!string.IsNullOrWhiteSpace(subCategory))
    {
        sql += " AND p.SubCategory = @SubCategory";
        parameters.Add("SubCategory", subCategory);
    }

    if (!string.IsNullOrWhiteSpace(color))
    {
        sql += " AND p.Color = @Color";
        parameters.Add("Color", color);
    }

    if (!string.IsNullOrWhiteSpace(dimensions))
    {
        sql += " AND p.Dimensions = @Dimensions";
        parameters.Add("Dimensions", dimensions);
    }

    if (!string.IsNullOrWhiteSpace(productType))
    {
        sql += " AND p.ProductType = @ProductType";
        parameters.Add("ProductType", productType);
    }

    if (!string.IsNullOrWhiteSpace(material))
    {
        sql += " AND p.Material = @Material";
        parameters.Add("Material", material);
    }

    if (minPrice.HasValue)
    {
        sql += " AND p.Price >= @MinPrice";
        parameters.Add("MinPrice", minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        sql += " AND p.Price <= @MaxPrice";
        parameters.Add("MaxPrice", maxPrice.Value);
    }

    sql += $" ORDER BY {orderBy}";

    using var db = new SqlConnection(connectionString);
    var mobilyaProducts = await db.QueryAsync(sql, parameters);
    return Results.Ok(mobilyaProducts);
});

app.MapGet("/api/ikea/products", async (
    string? q,
    string? brand,
    string? category,
    string? midCategory,
    string? subCategory,
    string? color,
    string? dimensions,
    string? productType,
    string? material,
    decimal? minPrice,
    decimal? maxPrice,
    string? sort) =>
{
    var orderBy = sort switch
    {
        "price_desc" => "Price DESC",
        "price_asc" => "Price ASC",
        "name_desc" => "Title DESC",
        _ => "Title ASC"
    };

    var sql = @"
        SELECT p.Id, p.ExternalId, p.Title, p.ImageUrl, p.Price, p.RegularPrice, p.ProductUrl,
               p.Category, p.MidCategory, p.SubCategory, p.Color, p.Dimensions, p.ProductType, p.Material,
               COALESCE(NULLIF(mp.Brand, ''), p.Source) AS Brand, p.Source
        FROM Products p
        LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id
        WHERE p.Source = 'ikea'";

    var parameters = new DynamicParameters();

    if (!string.IsNullOrWhiteSpace(q))
    {
        sql += @" AND (p.Title LIKE @Query OR p.Category LIKE @Query OR p.MidCategory LIKE @Query
                 OR p.SubCategory LIKE @Query OR p.ProductType LIKE @Query OR p.Material LIKE @Query OR mp.Brand LIKE @Query)";
        parameters.Add("Query", $"%{q.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(brand))
    {
        sql += " AND (mp.Brand = @Brand OR p.Source = @Brand)";
        parameters.Add("Brand", brand);
    }

    if (!string.IsNullOrWhiteSpace(category))
    {
        sql += " AND p.Category = @Category";
        parameters.Add("Category", category);
    }

    if (!string.IsNullOrWhiteSpace(midCategory))
    {
        sql += " AND p.MidCategory = @MidCategory";
        parameters.Add("MidCategory", midCategory);
    }

    if (!string.IsNullOrWhiteSpace(subCategory))
    {
        sql += " AND p.SubCategory = @SubCategory";
        parameters.Add("SubCategory", subCategory);
    }

    if (!string.IsNullOrWhiteSpace(color))
    {
        sql += " AND p.Color = @Color";
        parameters.Add("Color", color);
    }

    if (!string.IsNullOrWhiteSpace(dimensions))
    {
        sql += " AND p.Dimensions = @Dimensions";
        parameters.Add("Dimensions", dimensions);
    }

    if (!string.IsNullOrWhiteSpace(productType))
    {
        sql += " AND p.ProductType = @ProductType";
        parameters.Add("ProductType", productType);
    }

    if (!string.IsNullOrWhiteSpace(material))
    {
        sql += " AND p.Material = @Material";
        parameters.Add("Material", material);
    }

    if (minPrice.HasValue)
    {
        sql += " AND p.Price >= @MinPrice";
        parameters.Add("MinPrice", minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        sql += " AND p.Price <= @MaxPrice";
        parameters.Add("MaxPrice", maxPrice.Value);
    }

    sql += $" ORDER BY {orderBy}";

    using var db = new SqlConnection(connectionString);
    var mobilyaProducts = await db.QueryAsync(sql, parameters);
    return Results.Ok(mobilyaProducts);
});

app.MapGet("/api/ikea/filters", async () =>
{
    using var db = new SqlConnection(connectionString);

    var categoryRows = await db.QueryAsync(@"
        SELECT DISTINCT Category, MidCategory, SubCategory
        FROM Products
        WHERE Source = 'ikea'
          AND Category IS NOT NULL AND Category <> ''
        ORDER BY Category, MidCategory, SubCategory");

    var topCategories = await db.QueryAsync<string>(@"
        SELECT DISTINCT Category FROM Products
        WHERE Source = 'ikea' AND Category IS NOT NULL AND Category <> ''
        ORDER BY Category");

    var midCategories = await db.QueryAsync<string>(@"
        SELECT DISTINCT MidCategory FROM Products
        WHERE Source = 'ikea' AND MidCategory IS NOT NULL AND MidCategory <> ''
        ORDER BY MidCategory");

    var subCategories = await db.QueryAsync<string>(@"
        SELECT DISTINCT SubCategory FROM Products
        WHERE Source = 'ikea' AND SubCategory IS NOT NULL AND SubCategory <> ''
        ORDER BY SubCategory");

    var colors = await db.QueryAsync<string>(@"
        SELECT DISTINCT Color FROM Products WHERE Source = 'ikea' AND Color IS NOT NULL AND Color <> '' ORDER BY Color");

    var dimensions = await db.QueryAsync<string>(@"
        SELECT DISTINCT Dimensions FROM Products WHERE Source = 'ikea' AND Dimensions IS NOT NULL AND Dimensions <> '' ORDER BY Dimensions");

    var productTypes = await db.QueryAsync<string>(@"
        SELECT DISTINCT ProductType FROM Products WHERE Source = 'ikea' AND ProductType IS NOT NULL AND ProductType <> '' ORDER BY ProductType");

    var materials = await db.QueryAsync<string>(@"
        SELECT DISTINCT Material FROM Products WHERE Source = 'ikea' AND Material IS NOT NULL AND Material <> '' ORDER BY Material");

    return Results.Ok(new
    {
        CategoryTree = categoryRows,
        TopCategories = topCategories,
        MidCategories = midCategories,
        SubCategories = subCategories,
        Colors = colors,
        Dimensions = dimensions,
        ProductTypes = productTypes,
        Materials = materials
    });
});

app.MapGet("/api/mobilya/filters", async () =>
{
    using var db = new SqlConnection(connectionString);

    var brands = await db.QueryAsync<string>(@"
        SELECT DISTINCT Brand
        FROM (
            SELECT mp.Brand FROM MasterProducts mp JOIN Products p ON p.MasterProductId = mp.Id WHERE mp.Brand IS NOT NULL AND mp.Brand <> '' AND p.Source IN ('ikea', 'ozdilek')
            UNION
            SELECT COALESCE(NULLIF(mp.Brand, ''), p.Source) AS Brand FROM Products p LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id WHERE p.Source IN ('ikea', 'ozdilek')
        ) b WHERE Brand IS NOT NULL AND Brand <> '' ORDER BY Brand");

    var categoryRows = await db.QueryAsync(@"
        SELECT DISTINCT Category, MidCategory, SubCategory
        FROM Products
        WHERE Source IN ('ikea', 'ozdilek')
          AND Category IS NOT NULL AND Category <> ''
        ORDER BY Category, MidCategory, SubCategory");

    var topCategories = await db.QueryAsync<string>(@"
        SELECT DISTINCT Category FROM Products
        WHERE Source IN ('ikea', 'ozdilek') AND Category IS NOT NULL AND Category <> ''
        ORDER BY Category");

    var midCategories = await db.QueryAsync<string>(@"
        SELECT DISTINCT MidCategory FROM Products
        WHERE Source IN ('ikea', 'ozdilek') AND MidCategory IS NOT NULL AND MidCategory <> ''
        ORDER BY MidCategory");

    var subCategories = await db.QueryAsync<string>(@"
        SELECT DISTINCT SubCategory FROM Products
        WHERE Source IN ('ikea', 'ozdilek') AND SubCategory IS NOT NULL AND SubCategory <> ''
        ORDER BY SubCategory");

    var colors = await db.QueryAsync<string>(@"
        SELECT DISTINCT Color FROM Products WHERE Source IN ('ikea', 'ozdilek') AND Color IS NOT NULL AND Color <> '' ORDER BY Color");

    var dimensions = await db.QueryAsync<string>(@"
        SELECT DISTINCT Dimensions FROM Products WHERE Source IN ('ikea', 'ozdilek') AND Dimensions IS NOT NULL AND Dimensions <> '' ORDER BY Dimensions");

    var productTypes = await db.QueryAsync<string>(@"
        SELECT DISTINCT ProductType FROM Products WHERE Source IN ('ikea', 'ozdilek') AND ProductType IS NOT NULL AND ProductType <> '' ORDER BY ProductType");

    var materials = await db.QueryAsync<string>(@"
        SELECT DISTINCT Material FROM Products WHERE Source IN ('ikea', 'ozdilek') AND Material IS NOT NULL AND Material <> '' ORDER BY Material");

    return Results.Ok(new
    {
        Brands = brands,
        CategoryTree = categoryRows,
        TopCategories = topCategories,
        MidCategories = midCategories,
        SubCategories = subCategories,
        Colors = colors,
        Dimensions = dimensions,
        ProductTypes = productTypes,
        Materials = materials
    });
});

app.MapGet("/api/suggestions", async (string? q, string? tab) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        return Results.Ok(Array.Empty<string>());

    using var db = new SqlConnection(connectionString);
    var normalizedTab = tab?.Trim().ToLowerInvariant();

    var sql = normalizedTab switch
    {
        "mobilya" or "ikea" => @"SELECT TOP 6 Title FROM Products WHERE Source IN ('ikea', 'ozdilek') AND Title LIKE @Query ORDER BY Title",
        _ => @"SELECT TOP 6 COALESCE(mp.CanonicalTitle, p.Title) AS Title FROM Products p LEFT JOIN MasterProducts mp ON p.MasterProductId = mp.Id WHERE p.Source NOT IN ('ikea', 'ozdilek') AND (p.Title LIKE @Query OR mp.CanonicalTitle LIKE @Query) GROUP BY COALESCE(mp.CanonicalTitle, p.Title) ORDER BY Title"
    };

    var suggestions = await db.QueryAsync<string>(sql, new { Query = $"%{q.Trim()}%" });
    return Results.Ok(suggestions);
});

if (Directory.Exists(wwwrootPath))
{
    app.MapFallbackToFile("index.html");
}

app.Run();
