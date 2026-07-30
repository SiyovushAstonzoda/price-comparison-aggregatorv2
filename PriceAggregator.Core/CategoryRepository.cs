using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class CategoryRepository
{
    private readonly string _connectionString;

    public CategoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Her yol kökten yaprağa işlenir; aynı ebeveyn altındaki aynı slug yeniden eklenmez.
    public async Task<int> SavePathsAsync(IEnumerable<IReadOnlyList<string>> categoryPaths)
    {
        var insertedCount = 0;

        try
        {
            using var db = new SqlConnection(_connectionString);
            await db.OpenAsync();
            using var transaction = db.BeginTransaction();

            foreach (var categoryPath in categoryPaths)
            {
                int? parentCategoryId = null;

                foreach (var rawName in categoryPath.Where(name => !string.IsNullOrWhiteSpace(name)))
                {
                    var name = rawName.Trim();
                    var slug = ToSlug(name);

                    var categoryId = await db.QuerySingleOrDefaultAsync<int?>(@"
                        SELECT TOP (1) Id
                        FROM dbo.Categories
                        WHERE Slug = @Slug
                          AND ((ParentCategoryId = @ParentCategoryId)
                               OR (ParentCategoryId IS NULL AND @ParentCategoryId IS NULL));",
                        new { Slug = slug, ParentCategoryId = parentCategoryId }, transaction);

                    if (!categoryId.HasValue)
                    {
                        categoryId = await db.QuerySingleAsync<int>(@"
                            INSERT INTO dbo.Categories (Name, ParentCategoryId, Slug, IsActive)
                            OUTPUT INSERTED.Id
                            VALUES (@Name, @ParentCategoryId, @Slug, 1);",
                            new { Name = name, ParentCategoryId = parentCategoryId, Slug = slug }, transaction);

                        insertedCount++;
                    }

                    parentCategoryId = categoryId.Value;
                }
            }

            transaction.Commit();
            return insertedCount;
        }
        catch (SqlException ex)
        {
            Logger.Log($"[Categories] Failed to save category hierarchy: {ex.Message}");
            return 0;
        }
    }

    // Donusumu gerceklestirir.
    private static string ToSlug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('İ', 'i')
            .Replace('ş', 's')
            .Replace('Ş', 's')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'g')
            .Replace('ü', 'u')
            .Replace('Ü', 'u')
            .Replace('ö', 'o')
            .Replace('Ö', 'o')
            .Replace('ç', 'c')
            .Replace('Ç', 'c')
            .Normalize(NormalizationForm.FormD);

        var slug = new StringBuilder();
        var needsSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                if (needsSeparator && slug.Length > 0)
                    slug.Append('-');

                slug.Append(character);
                needsSeparator = false;
            }
            else
            {
                needsSeparator = true;
            }
        }

        return slug.Length == 0 ? "category" : slug.ToString();
    }
}
