using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

public class MatchingService
{
    private readonly string _connectionString;

    public MatchingService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> MatchProductAsync(int sellerProductId, ProductDto product, string source)
    {
        var brand = product.Brand;
        var title = product.Title;
        try
        {
            var safeBrand = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand;
            var (size, unit, packQty) = ResolveSize(product);

            using var db = new SqlConnection(_connectionString);

            var sellerId = await FindOrCreateSellerAsync(db, source);
            var brandId = await FindOrCreateBrandAsync(db, safeBrand);

            var candidates = await db.QueryAsync<(int Id, string CanonicalTitle)>(@"
            SELECT Id, Name AS CanonicalTitle FROM Products
            WHERE BrandId = @BrandId
            AND (SizeValue = @Size OR (SizeValue IS NULL AND @Size IS NULL))
            AND (SizeUnit = @Unit OR (SizeUnit IS NULL AND @Unit IS NULL))
            AND PackQuantity = @PackQty",
            new { BrandId = brandId, Size = size, Unit = unit, PackQty = packQty });

            int? bestMatchId = null;
            double bestScore = 0.0;
            double threshold = 0.70;

            foreach (var candidate in candidates)
            {
                double score = TitleSimilarity.Calculate(candidate.CanonicalTitle, title, safeBrand);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatchId = candidate.Id;
                }
            }

            int masterId;
            string matchStatus;
            string matchedBy;
            string? matchNotes;

            if (bestMatchId.HasValue && bestScore >= threshold)
            {
                masterId = bestMatchId.Value;
                matchStatus = "Matched";
                matchedBy = "TitleSimilarity";
                matchNotes = $"score={bestScore:F2}";

                // Backfill: this product was created before category/image data existed for
                // it (or by an offer with no such data). Retry now — this offer might carry
                // real data that fills the gap.
                var existing = await db.QuerySingleAsync<(bool HasCategory, bool HasImage)>(
                    @"SELECT CAST(CASE WHEN CategoryId IS NULL THEN 0 ELSE 1 END AS BIT) AS HasCategory,
                             CAST(CASE WHEN ImageUrl IS NULL THEN 0 ELSE 1 END AS BIT) AS HasImage
                      FROM Products WHERE Id = @Id",
                    new { Id = masterId });

                if (!existing.HasCategory)
                {
                    var backfillCategoryId = await CategoryResolver.ResolveAsync(db, product, source);
                    if (backfillCategoryId is not null)
                    {
                        await db.ExecuteAsync(
                            "UPDATE Products SET CategoryId = @CategoryId WHERE Id = @Id AND CategoryId IS NULL",
                            new { CategoryId = backfillCategoryId, Id = masterId });
                    }
                }

                if (!existing.HasImage && product.ImageUrl is not null)
                {
                    await db.ExecuteAsync(
                        "UPDATE Products SET ImageUrl = @ImageUrl WHERE Id = @Id AND ImageUrl IS NULL",
                        new { ImageUrl = product.ImageUrl, Id = masterId });
                }
            }
            else
            {
                var categoryId = await CategoryResolver.ResolveAsync(db, product, source);
                matchStatus = "Created";
                matchedBy = "NewProduct";
                matchNotes = bestMatchId.HasValue ? $"bestScore={bestScore:F2} (below threshold)" : null;

                masterId = await db.QuerySingleAsync<int>(@"
                INSERT INTO Products (Name, BrandId, SizeValue, SizeUnit, PackQuantity, CategoryId, ImageUrl)
                OUTPUT INSERTED.Id
                VALUES (@Title, @BrandId, @Size, @Unit, @PackQty, @CategoryId, @ImageUrl)",
                new { Title = title, BrandId = brandId, Size = size, Unit = unit, PackQty = packQty, CategoryId = categoryId, product.ImageUrl });
            }

            await db.ExecuteAsync(
                "INSERT INTO ProductMatchingLogs (SellerProductId, ProductId, Status, MatchedBy, Notes) VALUES (@SellerProductId, @ProductId, @Status, @MatchedBy, @Notes)",
                new { SellerProductId = sellerProductId, ProductId = masterId, Status = matchStatus, MatchedBy = matchedBy, Notes = matchNotes });

            var regularPrice = product.RegularPrice > product.Price ? product.RegularPrice : (decimal?)null;
            await UpsertOfferAsync(db, masterId, sellerId, sellerProductId, product.Price, regularPrice);
            await UpsertAttributesAsync(db, masterId, product, size, unit, packQty, source);

            return true;
        }
        catch (SqlException ex)
        {
            Logger.Log($"[Matching] DB error matching product {sellerProductId} ('{title}'): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Matching] Unexpected error matching product {sellerProductId} ('{title}'): {ex.Message}");
            return false;
        }
    }

    // The store's own structured unit/unitAmount (Migros/MacroCenter/Mion) is preferred over
    // guessing from the title. Only weight/volume units carry a meaningful birim fiyat — a
    // bare "PIECE" with amount 1 would otherwise show a redundant "₺X / Adet" identical to
    // the price itself, so that case (and sources without structured data) falls back to
    // SizeParser's title regex, same as before.
    private static (decimal? value, string? unit, int packQuantity) ResolveSize(ProductDto product)
    {
        if (!string.IsNullOrWhiteSpace(product.SourceUnitType) && product.SourceUnitAmount is > 0)
        {
            var normalized = product.SourceUnitType!.ToUpperInvariant();

            if (normalized is "GRAM" or "G" or "KG" or "KILOGRAM")
            {
                var grams = normalized is "KG" or "KILOGRAM" ? product.SourceUnitAmount.Value * 1000 : product.SourceUnitAmount.Value;
                return (grams, "G", 1);
            }

            if (normalized is "ML" or "MILLILITER" or "MILLILITRE" or "LT" or "LITRE" or "LITER")
            {
                var mls = normalized is "LT" or "LITRE" or "LITER" ? product.SourceUnitAmount.Value * 1000 : product.SourceUnitAmount.Value;
                return (mls, "ML", 1);
            }
        }

        return SizeParser.ExtractSize(product.Title);
    }

    private static async Task<int> FindOrCreateBrandAsync(SqlConnection db, string name)
    {
        var existing = await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Brands WHERE Name = @Name", new { Name = name });
        if (existing.HasValue) return existing.Value;

        try
        {
            return await db.QuerySingleAsync<int>(
                "INSERT INTO Brands (Name) OUTPUT INSERTED.Id VALUES (@Name)", new { Name = name });
        }
        catch (SqlException)
        {
            // Unique constraint hit — a concurrent scrape created this brand first.
            return await db.QuerySingleAsync<int>(
                "SELECT Id FROM Brands WHERE Name = @Name", new { Name = name });
        }
    }

    private static async Task<int> FindOrCreateSellerAsync(SqlConnection db, string name)
    {
        var existing = await db.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Sellers WHERE Name = @Name", new { Name = name });
        if (existing.HasValue) return existing.Value;

        try
        {
            return await db.QuerySingleAsync<int>(
                "INSERT INTO Sellers (Name) OUTPUT INSERTED.Id VALUES (@Name)", new { Name = name });
        }
        catch (SqlException)
        {
            return await db.QuerySingleAsync<int>(
                "SELECT Id FROM Sellers WHERE Name = @Name", new { Name = name });
        }
    }

    private static async Task UpsertOfferAsync(SqlConnection db, int productId, int sellerId, int sellerProductId, decimal price, decimal? regularPrice)
    {
        var existingOffer = await db.QuerySingleOrDefaultAsync<OfferRow>(
            "SELECT Id, Price FROM Offers WHERE ProductId = @ProductId AND SellerId = @SellerId",
            new { ProductId = productId, SellerId = sellerId });

        int offerId;
        if (existingOffer is not null)
        {
            offerId = existingOffer.Id;

            if (existingOffer.Price != price)
            {
                await db.ExecuteAsync(
                    "INSERT INTO OffersPriceHistory (OfferId, Price) VALUES (@OfferId, @Price)",
                    new { OfferId = offerId, Price = existingOffer.Price });
            }

            await db.ExecuteAsync(
                "UPDATE Offers SET Price = @Price, RegularPrice = @RegularPrice, SellerProductId = @SellerProductId, LastUpdatedAt = GETDATE() WHERE Id = @Id",
                new { Price = price, RegularPrice = regularPrice, SellerProductId = sellerProductId, Id = offerId });
        }
        else
        {
            offerId = await db.QuerySingleAsync<int>(@"
                INSERT INTO Offers (ProductId, SellerId, SellerProductId, Price, RegularPrice)
                OUTPUT INSERTED.Id
                VALUES (@ProductId, @SellerId, @SellerProductId, @Price, @RegularPrice)",
                new { ProductId = productId, SellerId = sellerId, SellerProductId = sellerProductId, Price = price, RegularPrice = regularPrice });

            await db.ExecuteAsync(
                "INSERT INTO OffersPriceHistory (OfferId, Price) VALUES (@OfferId, @Price)",
                new { OfferId = offerId, Price = price });
        }
    }

    // Combines the source's own structured attributes (Renk/Malzeme from Evidea, Renk/Seri
    // from İkea) with the title-keyword-tagged ones (Özellik for Market groceries) and writes
    // any not already on this product. Never removes an existing value — a color spotted on
    // one offer among several merged into the same canonical product doesn't stop being a
    // real variant just because a later offer for the same product didn't repeat it.
    private static async Task UpsertAttributesAsync(
        SqlConnection db, int productId, ProductDto product, decimal? size, string? unit, int packQty, string source)
    {
        var attributes = new List<(string Name, string Value)>();

        if (product.Attributes is not null)
        {
            foreach (var (name, rawValue) in product.Attributes)
            {
                // Sources sometimes combine several real values into one string — İkea's
                // "paslanmaz çelik-bej" (material-color) or Evidea's "Kumaş / Sünger /
                // Plastik" (material list) — so each raw value is split into its own filter
                // values instead of being kept as one long, near-unique compound (that's what
                // made the color filter checkbox list unusably long/"abartı").
                foreach (var part in SplitCompoundValue(rawValue))
                    attributes.Add((name, part));
            }
        }

        foreach (var tag in ProductPropertyTagger.Tag(product.Title))
            attributes.Add(("Özellik", tag));

        // "Cinsiyet" (Kadın/Erkek/Çocuk) only makes sense for personal care — a furniture
        // "Çocuk Yatağı" (kids' bed) or grocery product would false-tag on "çocuk"/"kadın"
        // otherwise, so this is gated behind the same sector check CategoryResolver uses.
        if (SectorClassifier.GetSectorName(source, product) == SectorClassifier.KozmetikSectorName)
        {
            foreach (var tag in PersonalCareGenderTagger.Tag(product.Title))
                attributes.Add(("Cinsiyet", tag));
        }

        // Same SizeValue/SizeUnit/PackQuantity UnitPriceCalculator already turns into birim
        // fiyat, displayed instead of priced — lets "İçecek" be filtered by Litre and a
        // by-weight grocery item by Kg with no extra scraping.
        var sizeLabel = SizeFormatter.Format(size, unit, packQty);
        if (sizeLabel is not null)
            attributes.Add(sizeLabel.Value);

        foreach (var (name, value) in attributes)
        {
            try
            {
                await db.ExecuteAsync(
                    @"IF NOT EXISTS (SELECT 1 FROM ProductAttributes WHERE ProductId = @ProductId AND AttributeName = @Name AND AttributeValue = @Value)
                      INSERT INTO ProductAttributes (ProductId, AttributeName, AttributeValue) VALUES (@ProductId, @Name, @Value)",
                    new { ProductId = productId, Name = name, Value = value });
            }
            catch (SqlException)
            {
                // Unique constraint hit — a concurrent scrape already wrote this exact
                // (ProductId, AttributeName, AttributeValue) row first. Nothing to do.
            }
        }
    }

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private static IEnumerable<string> SplitCompoundValue(string rawValue)
    {
        return rawValue
            .Split(['/', '-', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            // Sources disagree on casing (Evidea sends lowercase "beyaz", İkea "Beyaz") —
            // normalized the same way so they land as one filter value, not two. Lowercasing
            // first avoids .NET's ToTitleCase quirk of leaving all-caps input untouched, and
            // both steps use tr-TR culture so "İ"/"I" fold correctly (the "Turkish I problem").
            .Select(part => TurkishCulture.TextInfo.ToTitleCase(part.ToLower(TurkishCulture)))
            .Distinct(StringComparer.Ordinal);
    }

    private record OfferRow(int Id, decimal Price);
}
