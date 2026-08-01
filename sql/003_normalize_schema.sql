-- Replaces the ad-hoc MasterProducts/Products (Source string) schema with a normalized
-- Brands/Categories/Products/Sellers/SellerProducts/Offers/OffersPriceHistory/ProductMatchingLogs
-- model. Run manually against the target SQL Server instance (no migration tooling in this repo).
--
-- Destructive by design: all scraped data here is a refreshed cache (the Worker rescrapes
-- every 6h — see PriceAggregator.Worker/Worker.cs), so this drops and rebuilds rather than
-- migrating existing rows. Note: string literals use N'' so Turkish characters survive
-- regardless of the client/console code page (e.g. sqlcmd -f 65001).

-- Required for the Offers.TotalCost persisted computed column below.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Drop old schema (children first for FK order)
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.MasterProducts', 'U') IS NOT NULL DROP TABLE dbo.MasterProducts;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;

-- Drop new-named tables too, in case this script is re-run
IF OBJECT_ID('dbo.OffersPriceHistory', 'U') IS NOT NULL DROP TABLE dbo.OffersPriceHistory;
IF OBJECT_ID('dbo.ProductMatchingLogs', 'U') IS NOT NULL DROP TABLE dbo.ProductMatchingLogs;
IF OBJECT_ID('dbo.Offers', 'U') IS NOT NULL DROP TABLE dbo.Offers;
IF OBJECT_ID('dbo.SellerProducts', 'U') IS NOT NULL DROP TABLE dbo.SellerProducts;
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.Sellers', 'U') IS NOT NULL DROP TABLE dbo.Sellers;
IF OBJECT_ID('dbo.Brands', 'U') IS NOT NULL DROP TABLE dbo.Brands;

-- 1. BRANDS
CREATE TABLE Brands (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL UNIQUE,
    LogoUrl NVARCHAR(MAX) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

-- 2. CATEGORIES (SectorId is self-referencing: a sector root's own Id, used by
-- CategoryResolver.cs to scope name matching per top-level sector — Market vs Mobilya —
-- so e.g. an unrelated "Elektronik" leaf in one sector can't be reused by another)
CREATE TABLE Categories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    ParentCategoryId INT NULL FOREIGN KEY REFERENCES Categories(Id),
    SectorId INT NULL FOREIGN KEY REFERENCES Categories(Id),
    Slug NVARCHAR(255) NOT NULL UNIQUE,
    IsActive BIT NOT NULL DEFAULT 1
);

-- 3. PRODUCTS (canonical/matched product — replaces MasterProducts).
-- SizeValue/SizeUnit/PackQuantity feed UnitPriceCalculator's per-Kg/Litre/Adet birim fiyat.
CREATE TABLE Products (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BrandId INT NOT NULL FOREIGN KEY REFERENCES Brands(Id),
    CategoryId INT NULL FOREIGN KEY REFERENCES Categories(Id),
    Name NVARCHAR(500) NOT NULL,
    Sku NVARCHAR(100) NULL,
    ImageUrl NVARCHAR(MAX) NULL,
    SizeValue DECIMAL(18,3) NULL,
    SizeUnit NVARCHAR(50) NULL,
    PackQuantity INT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

-- 4. SELLERS (normalizes the old Products.Source free-text column)
CREATE TABLE Sellers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL UNIQUE,
    WebSiteUrl NVARCHAR(MAX) NULL,
    Rating DECIMAL(3,2) NOT NULL DEFAULT 0.00,
    IsActive BIT NOT NULL DEFAULT 1
);

-- 5. SELLERPRODUCTS (raw per-store scraped snapshot — replaces the old Products table).
-- UnitType/UnitAmount/SourceUnitPrice capture the store's own structured size/unit-price
-- data when the source API provides it (Migros/MacroCenter/Mion do; others leave these
-- null and SizeParser's title-regex fallback is used instead).
CREATE TABLE SellerProducts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SellerId INT NOT NULL FOREIGN KEY REFERENCES Sellers(Id),
    SellerProductCode NVARCHAR(255) NOT NULL,
    ExternalTitle NVARCHAR(500) NOT NULL,
    ExternalUrl NVARCHAR(MAX) NULL,
    ExternalImageUrl NVARCHAR(MAX) NULL,
    CurrentPrice DECIMAL(18,2) NOT NULL,
    UnitType NVARCHAR(20) NULL,
    UnitAmount DECIMAL(18,3) NULL,
    SourceUnitPrice DECIMAL(18,2) NULL,
    LastScrapedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_SellerProducts_Seller_Code UNIQUE (SellerId, SellerProductCode)
);

-- 6. OFFERS (one seller's current price for one canonical product)
CREATE TABLE Offers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id),
    SellerId INT NOT NULL FOREIGN KEY REFERENCES Sellers(Id),
    SellerProductId INT NOT NULL FOREIGN KEY REFERENCES SellerProducts(Id),
    Price DECIMAL(18,2) NOT NULL,
    CargoPrice DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    IsActive BIT NOT NULL DEFAULT 1,
    LastUpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    TotalCost AS (Price + CargoPrice) PERSISTED,
    CONSTRAINT UQ_Offers_Product_Seller UNIQUE (ProductId, SellerId)
);

-- 7. OFFERSPRICEHISTORY
CREATE TABLE OffersPriceHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OfferId INT NOT NULL FOREIGN KEY REFERENCES Offers(Id) ON DELETE CASCADE,
    Price DECIMAL(18,2) NOT NULL,
    RecordedAt DATETIME NOT NULL DEFAULT GETDATE()
);

-- 8. PRODUCTMATCHINGLOGS (audit trail for MatchingService's fuzzy-match decisions)
CREATE TABLE ProductMatchingLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SellerProductId INT NOT NULL FOREIGN KEY REFERENCES SellerProducts(Id),
    ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id),
    Status NVARCHAR(50) NOT NULL,
    MatchedBy NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    Notes NVARCHAR(MAX) NULL
);
