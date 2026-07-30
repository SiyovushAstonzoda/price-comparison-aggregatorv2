using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using Dapper;


using Microsoft.Data.SqlClient;



namespace PriceAggregator.Infrastructure.Scrapers.Cosmetics;

public class CategoryRepository
{
    private readonly string _connectionString;

    public CategoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> GetCosmeticsCategoryIdAsync()
    {
        using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleAsync<int>(
            "SELECT Id FROM dbo.Categories WHERE Slug = 'kozmetik'");
    }

    public async Task<int> UpsertSubCategoryAsync(
        int categoryId,
        string source,
        CosmeticSubCategory sub)
    {
        using var db = new SqlConnection(_connectionString);

        var existingId = await db.QueryFirstOrDefaultAsync<int?>(
            "SELECT Id FROM dbo.Categories WHERE ParentCategoryId = @CategoryId AND Slug = @Slug",
            new { CategoryId = categoryId, Slug = sub.Slug });

        if (existingId.HasValue)
        {
            await db.ExecuteAsync(
                "UPDATE dbo.Categories SET Name = @DisplayName WHERE Id = @Id",
                new { DisplayName = sub.DisplayName, Id = existingId.Value });
            return existingId.Value;
        }

        return await db.QuerySingleAsync<int>(@"
            INSERT INTO dbo.Categories (Name, ParentCategoryId, Slug, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@DisplayName, @CategoryId, @Slug, 1);",
            new
            {
                DisplayName = sub.DisplayName,
                CategoryId = categoryId,
                Slug = sub.Slug
            });
    }
}
