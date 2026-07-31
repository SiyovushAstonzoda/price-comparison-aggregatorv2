-- Adds hierarchical product categorization to the Aggregator database.
-- Run manually against the target SQL Server instance (no migration tooling in this repo).
-- Note: string literals use the N'' (Unicode) prefix so Turkish characters survive
-- regardless of the client/console code page running this script (e.g. sqlcmd -f 65001).

CREATE TABLE Categories (
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Slug NVARCHAR(100) NOT NULL UNIQUE,
    ParentCategoryId INT NULL FOREIGN KEY REFERENCES Categories(Id)
);

ALTER TABLE MasterProducts ADD CategoryId INT NULL FOREIGN KEY REFERENCES Categories(Id);

-- Parent categories
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'İçecekler', 'icecekler', NULL);
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Kahvaltılık', 'kahvaltilik-ana', NULL);
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Temel Gıda', 'temel-gida', NULL);

-- Leaf categories (children) — slugs must match PriceAggregator.Core.CategoryClassifier rule slugs
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Su', 'su', (SELECT Id FROM Categories WHERE Slug = 'icecekler'));
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Çay & Kahve', 'cay-kahve', (SELECT Id FROM Categories WHERE Slug = 'icecekler'));

INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Süt Ürünleri', 'sut-urunleri', (SELECT Id FROM Categories WHERE Slug = 'kahvaltilik-ana'));
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Tatlı & Kahvaltılık', 'kahvaltilik', (SELECT Id FROM Categories WHERE Slug = 'kahvaltilik-ana'));

INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Makarna & Bakliyat', 'makarna-bakliyat', (SELECT Id FROM Categories WHERE Slug = 'temel-gida'));
INSERT INTO Categories (Name, Slug, ParentCategoryId) VALUES (N'Yağ & Sos', 'yaglar', (SELECT Id FROM Categories WHERE Slug = 'temel-gida'));
