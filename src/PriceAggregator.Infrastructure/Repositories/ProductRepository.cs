using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
using Dapper;

using Microsoft.Data.SqlClient;


namespace PriceAggregator.Infrastructure.Repositories;

public class ProductRepository
{
    private readonly string _connectionString;

    public ProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int?> SaveAsync(string source, ProductDto product)
    {
        try
        {
            using var db = new SqlConnection(_connectionString);
            var id = await db.QuerySingleAsync<int>(@"
            MERGE Products AS target
            USING (SELECT @Source AS Source, @ExternalId AS ExternalId) AS src
            ON target.Source = src.Source AND target.ExternalId = src.ExternalId
            WHEN MATCHED THEN
                UPDATE SET Title=@Title, ImageUrl=@ImageUrl, Price=@Price, RegularPrice=@RegularPrice,
                           SourceCategory=@SourceCategory, LastUpdated=GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (Source, ExternalId, Title, ImageUrl, Price, RegularPrice, ProductUrl, SourceCategory, LastUpdated)
                VALUES (@Source, @ExternalId, @Title, @ImageUrl, @Price, @RegularPrice, @ProductUrl, @SourceCategory, GETDATE())
            OUTPUT INSERTED.Id;",
                new
                {
                    Source = source,
                    product.ExternalId,
                    product.Title,
                    product.ImageUrl,
                    product.Price,
                    product.RegularPrice,
                    product.ProductUrl,
                    product.SourceCategory
                });

            return id;
        }
        catch (SqlException ex)
        {
            Logger.Log($"[DB] Failed to save product '{product.Title}' ({source}): {ex.Message}");
            return null;
        }
    }
}
