SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1. BRANDS Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Brands]') AND type in (N'U'))
BEGIN
    CREATE TABLE Brands (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL,
        LogoUrl NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END

-- 2. CATEGORIES Table (Self-referencing FK ile)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND type in (N'U'))
BEGIN
    CREATE TABLE Categories (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL,
        ParentCategoryId INT NULL FOREIGN KEY REFERENCES Categories(Id),
        Slug NVARCHAR(255) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END

-- 3. PRODUCTS Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND type in (N'U'))
BEGIN
    CREATE TABLE Products (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        BrandId INT NOT NULL FOREIGN KEY REFERENCES Brands(Id),
        CategoryId INT NOT NULL FOREIGN KEY REFERENCES Categories(Id),
        Name NVARCHAR(255) NOT NULL,
        Sku NVARCHAR(100) NULL,
        ImageUrl NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END

-- 4. SELLERS Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Sellers]') AND type in (N'U'))
BEGIN
    CREATE TABLE Sellers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL,
        WebSiteUrl NVARCHAR(MAX) NULL,
        Rating DECIMAL(3,2) NOT NULL DEFAULT 0.00,
        IsActive BIT NOT NULL DEFAULT 1
    );
END

-- 5. SELLERPRODUCTS Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SellerProducts]') AND type in (N'U'))
BEGIN
    CREATE TABLE SellerProducts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SellerId INT NOT NULL FOREIGN KEY REFERENCES Sellers(Id),
        SellerProductCode NVARCHAR(255) NOT NULL,
        ExternalTitle NVARCHAR(255) NOT NULL,
        ExternalUrl NVARCHAR(MAX) NULL,
        ExternalImageUrl NVARCHAR(MAX) NULL,
        CurrentPrice DECIMAL(18,2) NOT NULL,
        LastScrapedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END

-- 6. PRODUCTMATCHINGLOGS Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductMatchingLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE ProductMatchingLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SellerProductId INT NOT NULL FOREIGN KEY REFERENCES SellerProducts(Id),
        ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id),
        Status NVARCHAR(50) NOT NULL,
        MatchedBy NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        Notes NVARCHAR(MAX) NULL
    );
END

-- 7. OFFERS Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Offers]') AND type in (N'U'))
BEGIN
    CREATE TABLE Offers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id),
        SellerId INT NOT NULL FOREIGN KEY REFERENCES Sellers(Id),
        SellerProductId INT NOT NULL FOREIGN KEY REFERENCES SellerProducts(Id),
        Price DECIMAL(18,2) NOT NULL,
        CargoPrice DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        IsActive BIT NOT NULL DEFAULT 1,
        LastUpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        TotalCost AS (Price + CargoPrice) PERSISTED -- Hesaplanan alan (TotalCost)
    );
END

-- 8. OFFERSPRICEHISTORY Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OffersPriceHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE OffersPriceHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OfferId INT NOT NULL FOREIGN KEY REFERENCES Offers(Id) ON DELETE CASCADE,
        Price DECIMAL(18,2) NOT NULL,
        RecordedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
