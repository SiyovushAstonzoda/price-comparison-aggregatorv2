-- Bu script'i bir kere, veritabanını kurarken çalıştırman gerekiyor.
-- Azure Data Studio, SSMS veya sqlcmd ile bağlanıp buradaki her şeyi çalıştır.

CREATE DATABASE Tutumlu;
GO
USE Tutumlu;
GO

CREATE TABLE Categories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(200) NOT NULL UNIQUE,
    ParentId INT NULL REFERENCES Categories(Id),
    DisplayOrder INT NOT NULL DEFAULT 0
);

CREATE TABLE MasterProducts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CanonicalTitle NVARCHAR(500) NOT NULL,
    Brand NVARCHAR(200) NULL,
    SizeValue DECIMAL(10,2) NULL,
    SizeUnit NVARCHAR(10) NULL,
    PackQuantity INT NOT NULL DEFAULT 1,
    CategoryId INT NULL REFERENCES Categories(Id)
);

CREATE TABLE Products (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Source NVARCHAR(50) NOT NULL,
    ExternalId BIGINT NOT NULL,
    Title NVARCHAR(500) NOT NULL,
    ImageUrl NVARCHAR(1000) NULL,
    Price DECIMAL(10,2) NOT NULL,
    RegularPrice DECIMAL(10,2) NOT NULL,
    ProductUrl NVARCHAR(1000) NOT NULL,
    LastUpdated DATETIME NOT NULL DEFAULT GETDATE(),
    MasterProductId INT NULL REFERENCES MasterProducts(Id),
    CONSTRAINT UQ_Products_Source_ExternalId UNIQUE (Source, ExternalId)
);
GO

-- ============================================================
-- Kategori ağacı: 3 ana kategori + her birinin alt kategorileri
-- ============================================================

INSERT INTO Categories (Name, Slug, ParentId, DisplayOrder) VALUES
    (N'Market', N'market', NULL, 1),
    (N'Mobilya', N'mobilya', NULL, 2),
    (N'Kozmetik', N'kozmetik', NULL, 3);
GO

DECLARE @Market INT = (SELECT Id FROM Categories WHERE Slug = N'market');
DECLARE @Mobilya INT = (SELECT Id FROM Categories WHERE Slug = N'mobilya');
DECLARE @Kozmetik INT = (SELECT Id FROM Categories WHERE Slug = N'kozmetik');

INSERT INTO Categories (Name, Slug, ParentId, DisplayOrder) VALUES
    -- Market alt kategorileri
    (N'İçecekler', N'market-icecekler', @Market, 1),
    (N'Süt & Kahvaltılık', N'market-sut-kahvaltilik', @Market, 2),
    (N'Temel Gıda', N'market-temel-gida', @Market, 3),
    (N'Atıştırmalık & Şekerleme', N'market-atistirmalik', @Market, 4),
    (N'Dondurulmuş Gıda', N'market-dondurulmus', @Market, 5),
    (N'Ev & Temizlik', N'market-ev-temizlik', @Market, 6),
    (N'Bebek Gıda & Bezi', N'market-bebek', @Market, 7),

    -- Mobilya alt kategorileri
    (N'Oturma Odası', N'mobilya-oturma-odasi', @Mobilya, 1),
    (N'Yatak Odası', N'mobilya-yatak-odasi', @Mobilya, 2),
    (N'Mutfak & Yemek Odası', N'mobilya-mutfak-yemek', @Mobilya, 3),
    (N'Ofis Mobilyaları', N'mobilya-ofis', @Mobilya, 4),
    (N'Bahçe & Balkon', N'mobilya-bahce-balkon', @Mobilya, 5),
    (N'Depolama & Organizasyon', N'mobilya-depolama', @Mobilya, 6),
    (N'Aydınlatma', N'mobilya-aydinlatma', @Mobilya, 7),
    (N'Dekorasyon', N'mobilya-dekorasyon', @Mobilya, 8),

    -- Kozmetik alt kategorileri
    (N'Cilt Bakımı', N'kozmetik-cilt-bakimi', @Kozmetik, 1),
    (N'Saç Bakımı', N'kozmetik-sac-bakimi', @Kozmetik, 2),
    (N'Makyaj', N'kozmetik-makyaj', @Kozmetik, 3),
    (N'Parfüm & Deodorant', N'kozmetik-parfum-deodorant', @Kozmetik, 4),
    (N'Ağız Bakımı', N'kozmetik-agiz-bakimi', @Kozmetik, 5),
    (N'Tıraş & Hijyen', N'kozmetik-tiras-hijyen', @Kozmetik, 6),
    (N'Erkek Bakım', N'kozmetik-erkek-bakim', @Kozmetik, 7);
GO

-- ============================================================
-- Basit anahtar kelime eşleştirmesiyle mevcut ürünleri
-- kategorilere otomatik atama (yaklaşık bir başlangıç noktasıdır,
-- gerekirse elle düzelt).
-- ============================================================

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-icecekler'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%su%' OR mp.CanonicalTitle LIKE N'%kola%' OR
    mp.CanonicalTitle LIKE N'%meşrubat%' OR mp.CanonicalTitle LIKE N'%çay%' OR
    mp.CanonicalTitle LIKE N'%kahve%' OR mp.CanonicalTitle LIKE N'%ayran%' OR
    mp.CanonicalTitle LIKE N'%meyve suyu%' OR mp.CanonicalTitle LIKE N'%gazoz%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-sut-kahvaltilik'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%süt%' OR mp.CanonicalTitle LIKE N'%peynir%' OR
    mp.CanonicalTitle LIKE N'%yoğurt%' OR mp.CanonicalTitle LIKE N'%yumurta%' OR
    mp.CanonicalTitle LIKE N'%tereyağ%' OR mp.CanonicalTitle LIKE N'%bal%' OR
    mp.CanonicalTitle LIKE N'%reçel%' OR mp.CanonicalTitle LIKE N'%zeytin%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-atistirmalik'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%çikolata%' OR mp.CanonicalTitle LIKE N'%bisküvi%' OR
    mp.CanonicalTitle LIKE N'%cips%' OR mp.CanonicalTitle LIKE N'%şeker%' OR
    mp.CanonicalTitle LIKE N'%gofret%' OR mp.CanonicalTitle LIKE N'%kraker%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-dondurulmus'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%dondurma%' OR mp.CanonicalTitle LIKE N'%donmuş%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-ev-temizlik'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%deterjan%' OR mp.CanonicalTitle LIKE N'%bulaşık%' OR
    mp.CanonicalTitle LIKE N'%yumuşatıcı%' OR mp.CanonicalTitle LIKE N'%çamaşır suyu%' OR
    mp.CanonicalTitle LIKE N'%temizleyici%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-bebek'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%bebek bezi%' OR mp.CanonicalTitle LIKE N'%bebek maması%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-temel-gida'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%makarna%' OR mp.CanonicalTitle LIKE N'%pirinç%' OR
    mp.CanonicalTitle LIKE N'%bulgur%' OR mp.CanonicalTitle LIKE N'%un%' OR
    mp.CanonicalTitle LIKE N'%yağ%' OR mp.CanonicalTitle LIKE N'%bakliyat%' OR
    mp.CanonicalTitle LIKE N'%mercimek%' OR mp.CanonicalTitle LIKE N'%nohut%' OR
    mp.CanonicalTitle LIKE N'%şeker%' AND mp.CanonicalTitle NOT LIKE N'%çikolata%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-sut-kahvaltilik'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%fındık kreması%' OR mp.CanonicalTitle LIKE N'%çikolata kreması%' OR
    mp.CanonicalTitle LIKE N'%kakaolu%' OR mp.CanonicalTitle LIKE N'%nutella%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'kozmetik-cilt-bakimi'
WHERE mp.CategoryId IS NULL AND (
    (
        mp.CanonicalTitle LIKE N'%krem%' OR mp.CanonicalTitle LIKE N'%serum%' OR
        mp.CanonicalTitle LIKE N'%nemlendirici%' OR mp.CanonicalTitle LIKE N'%cilt%'
    )
    AND mp.CanonicalTitle NOT LIKE N'%fındık%'
    AND mp.CanonicalTitle NOT LIKE N'%çikolata%'
    AND mp.CanonicalTitle NOT LIKE N'%kakao%'
    AND mp.CanonicalTitle NOT LIKE N'%karamel%'
    AND mp.CanonicalTitle NOT LIKE N'%muz%'
    AND mp.CanonicalTitle NOT LIKE N'%vanilya%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'kozmetik-sac-bakimi'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%şampuan%' OR mp.CanonicalTitle LIKE N'%saç kremi%' OR
    mp.CanonicalTitle LIKE N'%saç bakım%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'kozmetik-makyaj'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%ruj%' OR mp.CanonicalTitle LIKE N'%fondöten%' OR
    mp.CanonicalTitle LIKE N'%maskara%' OR mp.CanonicalTitle LIKE N'%oje%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'kozmetik-parfum-deodorant'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%parfüm%' OR mp.CanonicalTitle LIKE N'%deodorant%' OR
    mp.CanonicalTitle LIKE N'%kolonya%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'kozmetik-agiz-bakimi'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%diş macunu%' OR mp.CanonicalTitle LIKE N'%diş fırçası%' OR
    mp.CanonicalTitle LIKE N'%ağız bakım%'
);

UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'kozmetik-tiras-hijyen'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%tıraş%' OR mp.CanonicalTitle LIKE N'%ıslak mendil%' OR
    mp.CanonicalTitle LIKE N'%pamuk%' OR mp.CanonicalTitle LIKE N'%ped%'
);

-- ============================================================
-- Mobilya alt kategorileri: anahtar kelime eşleştirmesi
-- ============================================================

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-oturma-odasi'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%koltuk%' OR mp.CanonicalTitle LIKE N'%kanepe%' OR
    mp.CanonicalTitle LIKE N'%sehpa%' OR mp.CanonicalTitle LIKE N'%berjer%' OR
    mp.CanonicalTitle LIKE N'%puf%' OR mp.CanonicalTitle LIKE N'%tv ünitesi%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-yatak-odasi'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%yatak%' OR mp.CanonicalTitle LIKE N'%gardırop%' OR
    mp.CanonicalTitle LIKE N'%şifonyer%' OR mp.CanonicalTitle LIKE N'%komodin%' OR
    mp.CanonicalTitle LIKE N'%baza%' OR mp.CanonicalTitle LIKE N'%yatak odası%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-mutfak-yemek'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%yemek masası%' OR mp.CanonicalTitle LIKE N'%sandalye%' OR
    mp.CanonicalTitle LIKE N'%mutfak dolabı%' OR mp.CanonicalTitle LIKE N'%bar taburesi%' OR
    mp.CanonicalTitle LIKE N'%mutfak masası%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-ofis'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%ofis koltuğu%' OR mp.CanonicalTitle LIKE N'%çalışma masası%' OR
    mp.CanonicalTitle LIKE N'%ofis sandalyesi%' OR mp.CanonicalTitle LIKE N'%bilgisayar masası%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-bahce-balkon'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%bahçe mobilyası%' OR mp.CanonicalTitle LIKE N'%şezlong%' OR
    mp.CanonicalTitle LIKE N'%bahçe koltuğu%' OR mp.CanonicalTitle LIKE N'%balkon takımı%' OR
    mp.CanonicalTitle LIKE N'%bahçe masası%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-depolama'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%raf%' OR mp.CanonicalTitle LIKE N'%dolap%' OR
    mp.CanonicalTitle LIKE N'%kutu organizer%' OR mp.CanonicalTitle LIKE N'%saklama kutusu%' OR
    mp.CanonicalTitle LIKE N'%ayakkabılık%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-aydinlatma'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%avize%' OR mp.CanonicalTitle LIKE N'%aplik%' OR
    mp.CanonicalTitle LIKE N'%lamba%' OR mp.CanonicalTitle LIKE N'%abajur%' OR
    mp.CanonicalTitle LIKE N'%led şerit%'
);

UPDATE mp SET mp.CategoryId = c.Id
FROM MasterProducts mp JOIN Categories c ON c.Slug = N'mobilya-dekorasyon'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%halı%' OR mp.CanonicalTitle LIKE N'%ayna%' OR
    mp.CanonicalTitle LIKE N'%perde%' OR mp.CanonicalTitle LIKE N'%tablo%' OR
    mp.CanonicalTitle LIKE N'%vazo%' OR mp.CanonicalTitle LIKE N'%duvar saati%'
);
