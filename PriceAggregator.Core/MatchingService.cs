using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core;

// Magazadan gelen urunu ana katalog urunuyle eslestirir ve teklif kayitlarini gunceller.
public class MatchingService
{
    private readonly string _connectionString;
    private readonly CategoryMapper _categoryMapper;

    // Veritabani baglantisini servis boyunca kullanmak uzere saklar.
    public MatchingService(string connectionString)
    {
        _connectionString = connectionString;
        _categoryMapper = new CategoryMapper(connectionString);
    }

    // SellerProducts kaydini Products, Offers, OffersPriceHistory ve ProductMatchingLogs tablolarina baglar.
    public async Task<bool> MatchProductAsync(int sellerProductId, string? brand, string title, string searchTerm, string? sourceCategory)
    {
        try
        {
<<<<<<< Updated upstream
            if (await _categoryMapper.IsLikelyFalsePositiveAsync(searchTerm, sourceCategory))
            {
                Logger.Log($"[Matching] Skipping likely false positive: '{title}' (category: {sourceCategory}) for search '{searchTerm}'");
                return false;
            }

            var safeBrand = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand;
            var (size, unit, packQty) = SizeParser.ExtractSize(title);
            var canonicalCategoryId = await _categoryMapper.ResolveAsync(searchTerm, sourceCategory);

            using var db = new SqlConnection(_connectionString);

            var candidates = await db.QueryAsync<(int Id, string CanonicalTitle)>(@"
                SELECT Id, CanonicalTitle FROM MasterProducts
                WHERE Brand = @Brand
                  AND (SizeValue = @Size OR (SizeValue IS NULL AND @Size IS NULL))
                  AND (SizeUnit = @Unit OR (SizeUnit IS NULL AND @Unit IS NULL))
                  AND PackQuantity = @PackQty",
                new { Brand = safeBrand, Size = size, Unit = unit, PackQty = packQty });

            int? bestMatchId = null;
            double bestScore = 0.0;
            double threshold = 0.70;

            foreach (var candidate in candidates)
            {
                double score = CalculateSimilarity(candidate.CanonicalTitle, title, safeBrand);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatchId = candidate.Id;
                }
            }

            int masterId;
            if (bestMatchId.HasValue && bestScore >= threshold)
            {
                masterId = bestMatchId.Value;
                if (canonicalCategoryId.HasValue)
                {
                    await db.ExecuteAsync(
                        "UPDATE MasterProducts SET CanonicalCategoryId = ISNULL(CanonicalCategoryId, @CatId) WHERE Id = @Id",
                        new { CatId = canonicalCategoryId, Id = masterId });
                }
            }
            else
            {
                masterId = await db.QuerySingleAsync<int>(@"
                    INSERT INTO MasterProducts (CanonicalTitle, Brand, SizeValue, SizeUnit, PackQuantity, CanonicalCategoryId)
                    OUTPUT INSERTED.Id
                    VALUES (@Title, @Brand, @Size, @Unit, @PackQty, @CategoryId)",
                    new { Title = title, Brand = safeBrand, Size = size, Unit = unit, PackQty = packQty, CategoryId = canonicalCategoryId });
            }

            await db.ExecuteAsync(
                "UPDATE Products SET MasterProductId = @MasterId WHERE Id = @ProductId",
                new { MasterId = masterId, ProductId = productId });

=======
            // Arama kelimesiyle celisen kategoriye sahip urunleri eslestirmeden atlar.
            if (CategoryMapper.IsLikelyFalsePositive(searchTerm, sourceCategory)) return false;
            using var db = new SqlConnection(_connectionString);
            // Marka yoksa zorunlu BrandId iliskisi icin Unknown markasi kullanilir.
            var brandName = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand.Trim();
            var brandId = await db.QuerySingleOrDefaultAsync<int?>("SELECT TOP 1 Id FROM Brands WHERE Name=@Name", new { Name = brandName })
                ?? await db.QuerySingleAsync<int>("INSERT INTO Brands (Name) OUTPUT INSERTED.Id VALUES (@Name)", new { Name = brandName });
            // Kategori yoksa zorunlu CategoryId iliskisi icin Diger kategorisi kullanilir.
            var categoryName = string.IsNullOrWhiteSpace(sourceCategory) ? "Diğer" : sourceCategory.Trim();
            var categoryId = await db.QuerySingleOrDefaultAsync<int?>("SELECT TOP 1 Id FROM Categories WHERE Name=@Name", new { Name = categoryName })
                ?? await db.QuerySingleAsync<int>("INSERT INTO Categories (Name, Slug) OUTPUT INSERTED.Id VALUES (@Name, @Slug)", new { Name = categoryName, Slug = TurkishNormalizer.Normalize(categoryName).Replace(' ', '-') });
            // Ayni marka ve kategorideki katalog urunleri aday eslesme olarak okunur.
            var candidates = await db.QueryAsync<(int Id, string Name)>("SELECT Id, Name FROM Products WHERE BrandId=@BrandId AND CategoryId=@CategoryId", new { BrandId = brandId, CategoryId = categoryId });
            // Urun adlari token benzerligi ile puanlanir; 0.70 ve uzeri ayni urun kabul edilir.
            var match = candidates.Select(x => (x.Id, Score: CalculateSimilarity(x.Name, title, brandName))).OrderByDescending(x => x.Score).FirstOrDefault();
            var productId = match.Id != 0 && match.Score >= .70 ? match.Id : await db.QuerySingleAsync<int>("INSERT INTO Products (BrandId, CategoryId, Name) OUTPUT INSERTED.Id VALUES (@BrandId,@CategoryId,@Name)", new { BrandId = brandId, CategoryId = categoryId, Name = title });
            // Magaza urunu icin daha once olusturulmus teklif kaydi aranir.
            var offer = await db.QuerySingleOrDefaultAsync<(int Id, decimal Price)>("SELECT TOP 1 Id, Price FROM Offers WHERE SellerProductId=@Id AND ProductId=@ProductId", new { Id=sellerProductId, ProductId=productId });
            var price = await db.QuerySingleAsync<decimal>("SELECT CurrentPrice FROM SellerProducts WHERE Id=@Id", new { Id=sellerProductId });
            var sellerId = await db.QuerySingleAsync<int>("SELECT SellerId FROM SellerProducts WHERE Id=@Id", new { Id=sellerProductId });
            // Teklif yoksa eklenir; fiyat degismisse aktif teklif ve fiyat gecmisi guncellenir.
            var offerId = offer.Id == 0 ? await db.QuerySingleAsync<int>("INSERT INTO Offers (ProductId,SellerId,SellerProductId,Price) OUTPUT INSERTED.Id VALUES (@ProductId,@SellerId,@SellerProductId,@Price)", new { ProductId=productId,SellerId=sellerId,SellerProductId=sellerProductId,Price=price }) : offer.Id;
            if (offer.Id == 0 || offer.Price != price) { if(offer.Id != 0) await db.ExecuteAsync("UPDATE Offers SET Price=@Price,LastUpdatedAt=GETDATE() WHERE Id=@Id",new{Id=offerId,Price=price}); await db.ExecuteAsync("INSERT INTO OffersPriceHistory (OfferId,Price) VALUES (@OfferId,@Price)",new{OfferId=offerId,Price=price}); }
            // Otomatik eslestirmenin sonucu ve benzerlik puani denetim icin kaydedilir.
            await db.ExecuteAsync("INSERT INTO ProductMatchingLogs (SellerProductId,ProductId,Status,MatchedBy,Notes) VALUES (@SellerProductId,@ProductId,'Matched','AutomaticSimilarity',@Notes)",new{SellerProductId=sellerProductId,ProductId=productId,Notes=$"Score: {match.Score:F2}"});
>>>>>>> Stashed changes
            return true;
        }
        // Veritabani hatasi scraper akisini durdurmaz; hata loglanir.
        catch (SqlException ex) { Logger.Log($"[Matching] DB error: {ex.Message}"); return false; }
    }
    private static readonly HashSet<string> UnitWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ml", "lt", "l", "g", "gr", "kg", "adet", "cc", "pet"
    };

    private static readonly HashSet<string> DescriptorFillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "pet", "sise", "su", "dogal", "kaynak", "sade"
    };
    private static readonly Dictionary<string, string> ShapeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kalem"] = "penne rigate",
        // ["fiyonk"] = "farfalle",
        // ["burgu"] = "fusilli",
    };

    private string ApplyShapeAliases(string text)
    {
        foreach (var (alias, canonical) in ShapeAliases)
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(alias)}\b", canonical, RegexOptions.IgnoreCase);
        }
        return text;
    }

    // Urun adini marka, birim ve gereksiz kelimelerden arindirarak eslestirme tokenlarina ayirir.
    private HashSet<string> Tokenize(string text, string? brand)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>();

        text = ApplyShapeAliases(text);
        var normalized = TurkishNormalizer.Normalize(text);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        var brandTokens = string.IsNullOrWhiteSpace(brand)
            ? new HashSet<string>()
            : TurkishNormalizer.Normalize(brand).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Where(w => !brandTokens.Contains(w))
            .Where(w => !UnitWords.Contains(w))
            .Where(w => !DescriptorFillerWords.Contains(w))
            .Where(w => !w.All(char.IsDigit))
            .ToHashSet();
    }

    // İki kelimenin aynı ya da kök-ek ilişkisiyle benzer olup olmadığını kontrol eder.
    // Kısa Türkçe ek farklılıklarını da eşleşme olarak kabul eder.
    private static bool TokensMatch(string a, string b)
    {
        if (a == b) return true;

        // Bir kelime diğerinin kısa ek almış hâliyse eşleşme kabul edilir.
        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;
        if (longer.StartsWith(shorter, StringComparison.Ordinal) && (longer.Length - shorter.Length) <= 3)
            return true;

        // Yazım farkı küçükse kelimeler benzer kabul edilir.
        // Ornek: Spaghetti ve Spagetti gibi kucuk yazim farklarini da yakalar.
        if (shorter.Length >= 5)
        {
            int maxAllowedDistance = shorter.Length <= 7 ? 1 : 2;
            if (LevenshteinDistance(a, b) <= maxAllowedDistance)
                return true;
        }

        return false;
    }

    // Iki kelime arasindaki en az karakter degisimi sayisini hesaplar.
    private static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }

    // Iki urun adinin ortak tokenlarina gore 0 ile 1 arasynda benzerlik puani hesaplar.
    private double CalculateSimilarity(string title1, string title2, string brand)
    {
        var tokens1 = Tokenize(title1, brand);
        var tokens2 = Tokenize(title2, brand);

        if (tokens1.Count == 0 && tokens2.Count == 0) return 1.0;
        if (tokens1.Count == 0 || tokens2.Count == 0) return 1.0; // Her iki baslikta anlamli token kalmazsa eslesme kabul edilir.

        var remaining = new HashSet<string>(tokens2);
        int matches = 0;

        foreach (var t1 in tokens1)
        {
            // Urun adlari token benzerligi ile puanlanir; 0.70 ve uzeri ayni urun kabul edilir.
            var match = remaining.FirstOrDefault(t2 => TokensMatch(t1, t2));
            if (match != null)
            {
                matches++;
                remaining.Remove(match);
            }
        }

        int union = tokens1.Count + tokens2.Count - matches;
        return (double)matches / union;
    }

    /*private double CalculateSimilarity(string title1, string title2, string brand)
    {
        var tokens1 = Tokenize(title1, brand);
        var tokens2 = Tokenize(title2, brand);

        if (tokens1.Count == 0 && tokens2.Count == 0) return 1.0;
        if (tokens1.Count == 0 || tokens2.Count == 0) return 0.0;

        // Full containment: every token of the smaller set has a fuzzy match in the larger set,
        // with nothing left conflicting. Handles "official name" vs "shorter alias" cases
        // (e.g. "Penne Rigate (Kalem)" vs "Kalem") without being fooled by variant codes
        // (e.g. "6-7" vs "4-0"), since containment requires ALL smaller-set tokens to match.
        bool oneWayContainment(HashSet<string> smaller, HashSet<string> larger) =>
            smaller.All(s => larger.Any(l => TokensMatch(s, l)));

        if (tokens1.Count <= tokens2.Count && oneWayContainment(tokens1, tokens2))
            return 1.0;
        if (tokens2.Count < tokens1.Count && oneWayContainment(tokens2, tokens1))
            return 1.0;

        var remaining = new HashSet<string>(tokens2);
        int matches = 0;

        foreach (var t1 in tokens1)
        {
            // Urun adlari token benzerligi ile puanlanir; 0.70 ve uzeri ayni urun kabul edilir.
            var match = remaining.FirstOrDefault(t2 => TokensMatch(t1, t2));
            if (match != null)
            {
                matches++;
                remaining.Remove(match);
            }
        }

        int union = tokens1.Count + tokens2.Count - matches;
        return (double)matches / union;
    }*/
}
