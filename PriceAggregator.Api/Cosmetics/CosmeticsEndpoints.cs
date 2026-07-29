using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Api.Cosmetics;

/// <summary>
/// Kozmetik kategorisine ait API endpoint'leri.
/// Mevcut /api/products endpoint'lerine dokunmaz; yeni /api/kozmetik/* altında çalışır.
/// </summary>
public static class CosmeticsEndpoints
{
    public static void MapCosmeticsEndpoints(this WebApplication app, string connectionString)
    {
        // GET /api/kozmetik/kategoriler
        // Dinamik olarak oluşturulmuş tüm kozmetik alt kategorilerini döndürür.
        app.MapGet("/api/kozmetik/kategoriler", async () =>
        {
            using var db = new SqlConnection(connectionString);
            // GROUP BY yalnızca Slug — her alt kategori tek satır, sayı tüm kaynakları kapsar
            var subCategories = await db.QueryAsync(@"
                SELECT sc.Slug,
                       MIN(sc.DisplayName)          AS DisplayName,
                       COUNT(DISTINCT p.MasterProductId) AS ProductCount
                FROM   dbo.SubCategories sc
                JOIN   dbo.Categories    c  ON c.Id = sc.CategoryId AND c.Slug = 'kozmetik'
                LEFT JOIN dbo.Products   p  ON p.SubCategoryId = sc.Id
                                            AND p.MasterProductId IS NOT NULL
                GROUP  BY sc.Slug
                ORDER  BY MIN(sc.DisplayName)");

            return Results.Ok(subCategories);
        });

        // GET /api/kozmetik/urunler?subCategory=cilt-bakimi&q=&brand=&sort=&minPrice=&maxPrice=
        // Filtrelenmiş kozmetik ürün listesi döndürür.
        // GROUP BY yalnızca MasterProduct bazındadır — alt kategori gruplamadan kaynaklı
        // tekrar kayıt oluşmaz.
        app.MapGet("/api/kozmetik/urunler", async (
            string? subCategory,
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

            // GROUP BY yalnızca mp.Id — aynı ürün birden fazla alt kategoride olsa
            // bile tek satır döner.
            var sql = @$"
                SELECT mp.Id,
                       mp.CanonicalTitle,
                       mp.Brand,
                       mp.SizeValue,
                       mp.SizeUnit,
                       MIN(p.Price)    AS LowestPrice,
                       COUNT(p.Id)     AS OfferCount,
                       MAX(p.ImageUrl) AS ImageUrl,
                       (SELECT TOP 1 REPLACE(p2.ProductUrl, 'www.mion.com.tr/urun/', 'www.migros.com.tr/mion/') FROM dbo.Products p2 WHERE p2.MasterProductId = mp.Id ORDER BY p2.Price ASC, p2.Id ASC) AS ProductUrl
                FROM   dbo.MasterProducts mp
                JOIN   dbo.Products       p   ON p.MasterProductId = mp.Id
                JOIN   dbo.SubCategories  sc  ON sc.Id = p.SubCategoryId
                JOIN   dbo.Categories     c   ON c.Id  = sc.CategoryId
                                              AND c.Slug = 'kozmetik'
                WHERE  1=1";

            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(subCategory))
            {
                sql += " AND sc.Slug = @SubCategory";
                parameters.Add("SubCategory", subCategory.Trim());
            }

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

            // GROUP BY yalnızca MasterProduct sütunları
            sql += " GROUP BY mp.Id, mp.CanonicalTitle, mp.Brand, mp.SizeValue, mp.SizeUnit";

            if (minPrice.HasValue)
            {
                sql += " HAVING MIN(p.Price) >= @MinPrice";
                parameters.Add("MinPrice", minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                sql += minPrice.HasValue
                    ? " AND MIN(p.Price) <= @MaxPrice"
                    : " HAVING MIN(p.Price) <= @MaxPrice";
                parameters.Add("MaxPrice", maxPrice.Value);
            }

            sql += $" ORDER BY {orderBy}";

            using var db = new SqlConnection(connectionString);
            var products = await db.QueryAsync(sql, parameters);
            return Results.Ok(products);
        });

        // GET /api/kozmetik/urunler/{masterId}
        // Belirli bir ürünün tüm mağaza tekliflerini döndürür.
        app.MapGet("/api/kozmetik/urunler/{masterId}", async (int masterId) =>
        {
            using var db = new SqlConnection(connectionString);
            var offers = await db.QueryAsync(@"
                SELECT mp.CanonicalTitle,
                       p.Source, p.Price, p.ImageUrl, 
                       REPLACE(p.ProductUrl, 'www.mion.com.tr/urun/', 'www.migros.com.tr/mion/') AS ProductUrl
                FROM   dbo.MasterProducts mp
                JOIN   dbo.Products       p  ON p.MasterProductId = mp.Id
                WHERE  mp.Id = @MasterId
                ORDER  BY p.Price ASC",
                new { MasterId = masterId });

            return Results.Ok(offers);
        });

        // GET /api/kozmetik/markalar?subCategory=cilt-bakimi
        // Kozmetik ürünlerdeki benzersiz markaları döndürür (filtre için).
        app.MapGet("/api/kozmetik/markalar", async (string? subCategory) =>
        {
            using var db = new SqlConnection(connectionString);

            var sql = @"
                SELECT DISTINCT mp.Brand
                FROM   dbo.MasterProducts mp
                JOIN   dbo.Products       p   ON p.MasterProductId = mp.Id
                JOIN   dbo.SubCategories  sc  ON sc.Id = p.SubCategoryId
                JOIN   dbo.Categories     c   ON c.Id  = sc.CategoryId AND c.Slug = 'kozmetik'
                WHERE  mp.Brand IS NOT NULL
                  AND  mp.Brand <> ''
                  AND  mp.Brand <> 'Unknown'";

            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(subCategory))
            {
                sql += " AND sc.Slug = @SubCategory";
                parameters.Add("SubCategory", subCategory.Trim());
            }

            sql += " ORDER BY mp.Brand";

            var brands = await db.QueryAsync<string>(sql, parameters);
            return Results.Ok(brands);
        });

        // GET /api/kozmetik/urunler/{masterId}/benzer
        // Aynı alt kategorideki BENZER İSİMLİ alternatif ürünleri döndürür.
        app.MapGet("/api/kozmetik/urunler/{masterId}/benzer", async (int masterId) =>
        {
            using var db = new SqlConnection(connectionString);

            // 1. Hedef ürünün adını ve alt kategorisini al
            var targetInfo = await db.QuerySingleOrDefaultAsync(@"
                SELECT TOP 1 mp.CanonicalTitle, p.SubCategoryId
                FROM dbo.MasterProducts mp
                JOIN dbo.Products p ON p.MasterProductId = mp.Id
                WHERE mp.Id = @MasterId", new { MasterId = masterId });

            if (targetInfo == null) return Results.NotFound();

            string targetTitle = targetInfo.CanonicalTitle;
            int subCatId = targetInfo.SubCategoryId;

            // 2. Aynı alt kategorideki tüm DİĞER ürünleri çek
            var candidates = await db.QueryAsync(@"
                SELECT mp.Id,
                       mp.CanonicalTitle,
                       mp.Brand,
                       mp.SizeValue,
                       mp.SizeUnit,
                       MIN(p.Price)    AS LowestPrice,
                       COUNT(p.Id)     AS OfferCount,
                       MAX(p.ImageUrl) AS ImageUrl,
                       (SELECT TOP 1 REPLACE(p2.ProductUrl, 'www.mion.com.tr/urun/', 'www.migros.com.tr/mion/') FROM dbo.Products p2 WHERE p2.MasterProductId = mp.Id ORDER BY p2.Price ASC, p2.Id ASC) AS ProductUrl
                FROM   dbo.MasterProducts mp
                JOIN   dbo.Products       p   ON p.MasterProductId = mp.Id
                WHERE  p.SubCategoryId = @TargetSubCategory
                  AND  mp.Id != @MasterId
                GROUP BY mp.Id, mp.CanonicalTitle, mp.Brand, mp.SizeValue, mp.SizeUnit",
                new { TargetSubCategory = subCatId, MasterId = masterId });

            // 3. Jaccard benzerliğine göre sırala
            var targetWords = new HashSet<string>(
                System.Text.RegularExpressions.Regex.Replace(targetTitle.ToLowerInvariant(), @"[^\w\s]", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

            var scoredCandidates = candidates.Select(c => {
                string cTitle = c.CanonicalTitle;
                var cWords = new HashSet<string>(
                    System.Text.RegularExpressions.Regex.Replace(cTitle.ToLowerInvariant(), @"[^\w\s]", " ")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));

                int intersection = 0;
                foreach (var w in targetWords) if (cWords.Contains(w)) intersection++;
                int union = targetWords.Count + cWords.Count - intersection;
                double score = union == 0 ? 0 : (double)intersection / union;

                return new { Product = c, Score = score };
            })
            // En yüksek benzerlik, sonra farklı marka tercih edilebilir ama şimdilik en benzeri alıyoruz.
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Product.OfferCount)
            .ThenBy(x => x.Product.LowestPrice)
            .Select(x => x.Product)
            .Take(5)
            .ToList();

            return Results.Ok(scoredCandidates);
        });
    }
}
