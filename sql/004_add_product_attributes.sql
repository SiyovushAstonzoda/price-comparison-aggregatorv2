-- Adds product-level attribute filtering (color for furniture, dietary/lifestyle tags for
-- groceries) on top of the normalized schema from 003_normalize_schema.sql. Run manually
-- against the target SQL Server instance (no migration tooling in this repo).
--
-- One product can carry several values for the same attribute name — Evidea/İkea offers of
-- the same canonical furniture item can merge several color variants, and a grocery item can
-- be both "Organik" and "Laktozsuz" at once — hence AttributeValue is part of the uniqueness,
-- not AttributeName alone.

IF OBJECT_ID('dbo.ProductAttributes', 'U') IS NOT NULL DROP TABLE dbo.ProductAttributes;

CREATE TABLE ProductAttributes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id),
    AttributeName NVARCHAR(50) NOT NULL,
    AttributeValue NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_ProductAttributes UNIQUE (ProductId, AttributeName, AttributeValue)
);
