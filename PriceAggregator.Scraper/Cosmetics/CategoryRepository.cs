using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Scraper.Cosmetics;

/// <summary>
/// Kozmetik kategorisine ait SubCategories ve Products
/// DB işlemlerini yönetir. MasterProducts mantığını değiştirmez.
/// </summary>
public class CategoryRepository
{
    private readonly string _connectionString;

    public CategoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// 'kozmetik' kategorisinin DB ID'sini döndürür.
    /// init_database.sql çalıştırılmışsa her zaman bulunur.
    /// </summary>
    public async Task<int> GetCosmeticsCategoryIdAsync()
    {
        using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleAsync<int>(
            "SELECT Id FROM dbo.Categories WHERE Slug = 'kozmetik'");
    }

    /// <summary>
    /// Alt kategoriyi MERGE ile kaydeder; zaten varsa günceller.
    /// </summary>
    public async Task<int> UpsertSubCategoryAsync(
        int categoryId,
        string source,
        CosmeticSubCategory sub)
    {
        using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleAsync<int>(@"
            MERGE dbo.SubCategories AS target
            USING (SELECT @CategoryId AS CategoryId,
                          @Source     AS Source,
                          @Slug       AS Slug) AS src
            ON  target.CategoryId = src.CategoryId
            AND target.Source     = src.Source
            AND target.Slug       = src.Slug
            WHEN MATCHED THEN
                UPDATE SET DisplayName  = @DisplayName,
                           ExternalSlug = @ExternalSlug
            WHEN NOT MATCHED THEN
                INSERT (CategoryId, Slug, DisplayName, Source, ExternalSlug)
                VALUES (@CategoryId, @Slug, @DisplayName, @Source, @ExternalSlug)
            OUTPUT INSERTED.Id;",
            new
            {
                CategoryId   = categoryId,
                Source       = source,
                Slug         = sub.Slug,
                DisplayName  = sub.DisplayName,
                ExternalSlug = sub.ExternalSlug
            });
    }

    /// <summary>
    /// Ürünü Products tablosuna MERGE ile kaydeder ve
    /// SubCategoryId'yi set eder. Var olan ürünleri günceller.
    /// </summary>
    public async Task<int> SaveProductAsync(
        string source,
        int subCategoryId,
        ProductDto product)
    {
        using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleAsync<int>(@"
            MERGE dbo.Products AS target
            USING (SELECT @Source     AS Source,
                          @ExternalId AS ExternalId) AS src
            ON target.Source = src.Source AND target.ExternalId = src.ExternalId
            WHEN MATCHED THEN
                UPDATE SET Title         = @Title,
                           ImageUrl      = @ImageUrl,
                           Price         = @Price,
                           RegularPrice  = @RegularPrice,
                           SubCategoryId = @SubCategoryId,
                           LastUpdated   = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (Source, ExternalId, Title, ImageUrl,
                        Price, RegularPrice, ProductUrl, SubCategoryId, LastUpdated)
                VALUES (@Source, @ExternalId, @Title, @ImageUrl,
                        @Price, @RegularPrice, @ProductUrl, @SubCategoryId, GETDATE())
            OUTPUT INSERTED.Id;",
            new
            {
                Source        = source,
                product.ExternalId,
                product.Title,
                product.ImageUrl,
                product.Price,
                product.RegularPrice,
                product.ProductUrl,
                SubCategoryId = subCategoryId
            });
    }
}
