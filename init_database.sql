-- ============================================================
--  Tutumlu – Price Comparison Aggregator
--  Database: Tutumlu  (SQL Server 2019+)
--  Run this script once to create all required tables.
-- ============================================================

USE master;
GO

-- 1. Veritabanını oluştur (zaten varsa atla)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'Tutumlu')
BEGIN
    CREATE DATABASE Tutumlu;
    PRINT 'Veritabanı ''Tutumlu'' oluşturuldu.';
END
GO

USE Tutumlu;
GO

-- ============================================================
-- TABLE 1: Categories
--   Üst kategori tanımları. Her ekip kendi kategorisini ekler.
--   Örnek satırlar: (1,'kozmetik','Kozmetik & Kişisel Bakım')
--                   (2,'gida','Gıda & Market')
-- ============================================================
IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        Id          INT           IDENTITY(1,1) PRIMARY KEY,
        Slug        NVARCHAR(100) NOT NULL,      -- URL-friendly anahtar  (örn: 'kozmetik')
        DisplayName NVARCHAR(200) NOT NULL,      -- Kullanıcıya gösterilen ad
        CreatedAt   DATETIME2     NOT NULL DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX UX_Categories_Slug ON dbo.Categories (Slug);

    -- Kozmetik kategorisini başlangıç verisi olarak ekle
    INSERT INTO dbo.Categories (Slug, DisplayName)
    VALUES ('kozmetik', 'Kozmetik & Kişisel Bakım');

    PRINT 'Tablo ''Categories'' oluşturuldu.';
END
GO

-- ============================================================
-- TABLE 2: SubCategories
--   Her kaynaktan dinamik olarak çekilen alt kategoriler.
--   Örnek: Cilt Bakımı, Saç Bakımı, Makyaj, Parfüm…
-- ============================================================
IF OBJECT_ID(N'dbo.SubCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubCategories (
        Id           INT           IDENTITY(1,1) PRIMARY KEY,
        CategoryId   INT           NOT NULL,           -- Categories.Id FK
        Slug         NVARCHAR(200) NOT NULL,           -- Normalize alt kategori adı (örn: 'cilt-bakimi')
        DisplayName  NVARCHAR(300) NOT NULL,           -- Kullanıcıya gösterilen ad
        Source       NVARCHAR(100) NOT NULL,           -- Hangi mağazadan çekildi (örn: 'migros')
        ExternalSlug NVARCHAR(500) NULL,               -- Kaynak sitedeki kategori yolu
        CreatedAt    DATETIME2     NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_SubCategories_Categories
            FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id)
    );

    -- Aynı mağazadan aynı slug tekrar eklenmemeli
    CREATE UNIQUE INDEX UX_SubCategories_Source_Slug
        ON dbo.SubCategories (CategoryId, Source, Slug);

    CREATE INDEX IX_SubCategories_CategoryId ON dbo.SubCategories (CategoryId);

    PRINT 'Tablo ''SubCategories'' oluşturuldu.';
END
GO

-- ============================================================
-- TABLE 3: MasterProducts
--   Scraper'ın farklı kaynaklardan çektiği ürünleri
--   marka + boyut bazında tekilleştiren ana ürün tablosu.
--   MatchingService bu tabloya INSERT / SELECT yapar.
-- ============================================================
IF OBJECT_ID(N'dbo.MasterProducts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MasterProducts (
        Id             INT           IDENTITY(1,1) PRIMARY KEY,
        CanonicalTitle NVARCHAR(500) NOT NULL,          -- Ürünün normalize edilmiş adı
        Brand          NVARCHAR(200) NULL,               -- Marka (örn: "Coca-Cola")
        SizeValue      DECIMAL(10,3) NULL,               -- Hacim / Ağırlık sayısal değeri (örn: 1.5)
        SizeUnit       NVARCHAR(20)  NULL,               -- Birim (örn: "L", "ml", "kg", "g")
        CreatedAt      DATETIME2     NOT NULL DEFAULT GETDATE()
    );

    -- Aynı marka+boyut kombinasyonunun tekrar eklenmesini önler
    CREATE UNIQUE INDEX UX_MasterProducts_Brand_Size
        ON dbo.MasterProducts (Brand, SizeValue, SizeUnit)
        WHERE Brand IS NOT NULL AND SizeValue IS NOT NULL AND SizeUnit IS NOT NULL;

    PRINT 'Tablo ''MasterProducts'' oluşturuldu.';
END
GO

-- ============================================================
-- TABLE 4: Products
--   Her kaynaktan (Migros, MacroCenter, MarketFiyati …) 
--   çekilen ham ürün teklifleri.
--   ProductRepository bu tabloya MERGE yapar.
-- ============================================================
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products (
        Id              INT            IDENTITY(1,1) PRIMARY KEY,
        Source          NVARCHAR(100)  NOT NULL,           -- Kaynak mağaza (örn: "migros", "macrocenter")
        ExternalId      BIGINT         NOT NULL,           -- Kaynak sitedeki ürün ID'si
        Title           NVARCHAR(500)  NOT NULL,           -- Ham ürün adı
        ImageUrl        NVARCHAR(2000) NULL,               -- Ürün görseli URL'i
        Price           DECIMAL(18,2)  NOT NULL,           -- Güncel satış fiyatı
        RegularPrice    DECIMAL(18,2)  NOT NULL,           -- İndirim öncesi liste fiyatı
        ProductUrl      NVARCHAR(2000) NOT NULL,           -- Ürün sayfası URL'i
        LastUpdated     DATETIME2      NOT NULL DEFAULT GETDATE(),  -- Son scrape zamanı
        MasterProductId INT            NULL,               -- MasterProducts.Id FK (matching sonrası dolar)
        SubCategoryId   INT            NULL,               -- SubCategories.Id FK (kategori bazlı scrape'de dolar)

        CONSTRAINT FK_Products_MasterProducts
            FOREIGN KEY (MasterProductId) REFERENCES dbo.MasterProducts(Id),
        CONSTRAINT FK_Products_SubCategories
            FOREIGN KEY (SubCategoryId) REFERENCES dbo.SubCategories(Id)
    );

    -- Source + ExternalId çifti her kaynak için tekil olmalı
    CREATE UNIQUE INDEX UX_Products_Source_ExternalId
        ON dbo.Products (Source, ExternalId);

    -- API sorgularında sık filtrelenen sütunlar
    CREATE INDEX IX_Products_MasterProductId ON dbo.Products (MasterProductId);
    CREATE INDEX IX_Products_SubCategoryId   ON dbo.Products (SubCategoryId);
    CREATE INDEX IX_Products_Price           ON dbo.Products (Price);

    PRINT 'Tablo ''Products'' oluşturuldu.';
END
ELSE
BEGIN
    -- Tablo zaten varsa sadece eksik sütunları ekle (idempotent)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'SubCategoryId')
    BEGIN
        ALTER TABLE dbo.Products ADD SubCategoryId INT NULL
            CONSTRAINT FK_Products_SubCategories FOREIGN KEY REFERENCES dbo.SubCategories(Id);
        CREATE INDEX IX_Products_SubCategoryId ON dbo.Products (SubCategoryId);
        PRINT 'Products tablosuna SubCategoryId sütunu eklendi.';
    END
END
GO

-- ============================================================
-- (İSTEĞE BAĞLI) Örnek doğrulama sorguları
-- ============================================================
/*
-- Tüm kozmetik alt kategorilerini listele
SELECT sc.Source, sc.DisplayName, sc.Slug, COUNT(p.Id) AS ProductCount
FROM   dbo.SubCategories sc
JOIN   dbo.Categories    c  ON c.Id = sc.CategoryId AND c.Slug = 'kozmetik'
LEFT JOIN dbo.Products   p  ON p.SubCategoryId = sc.Id
GROUP  BY sc.Source, sc.DisplayName, sc.Slug
ORDER  BY sc.Source, sc.DisplayName;

-- Kozmetik ürünlerini en ucuzdan listele
SELECT mp.CanonicalTitle, mp.Brand, sc.DisplayName AS SubCategory,
       MIN(p.Price) AS LowestPrice, COUNT(p.Id) AS OfferCount
FROM   dbo.MasterProducts mp
JOIN   dbo.Products       p  ON p.MasterProductId = mp.Id
JOIN   dbo.SubCategories  sc ON sc.Id = p.SubCategoryId
JOIN   dbo.Categories     c  ON c.Id = sc.CategoryId AND c.Slug = 'kozmetik'
GROUP  BY mp.Id, mp.CanonicalTitle, mp.Brand, sc.DisplayName
ORDER  BY LowestPrice ASC;
*/
GO

PRINT '✅ Tüm tablolar başarıyla oluşturuldu.';
