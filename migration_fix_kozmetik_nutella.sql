-- ============================================================
-- DÜZELTME: "fındık kreması" gibi ürünler (Nutella vb.) yanlışlıkla
-- "krem" kelimesi geçtiği için Kozmetik / Cilt Bakımı kategorisine
-- düşmüştü. Bu script mevcut veritabanındaki bu hatayı düzeltir.
--
-- migration_add_categories.sql ve schema.sql zaten güncellendi
-- (yeni kurulumlar için), ama bu script hâlihazırda çalışan bir
-- veritabanındaki YANLIŞ ATANMIŞ satırları düzeltmek için gerekli.
--
-- Tek seferlik çalıştırman yeterli, tekrar çalıştırmak zararsızdır.
-- ============================================================

USE Tutumlu;
GO

-- 1) Cilt Bakımı'na yanlışlıkla düşmüş gıda ürünlerinin kategorisini boşalt
UPDATE mp
SET mp.CategoryId = NULL
FROM MasterProducts mp
JOIN Categories c ON c.Id = mp.CategoryId
WHERE c.Slug = N'kozmetik-cilt-bakimi'
  AND (
    mp.CanonicalTitle LIKE N'%fındık%' OR mp.CanonicalTitle LIKE N'%çikolata%' OR
    mp.CanonicalTitle LIKE N'%kakao%' OR mp.CanonicalTitle LIKE N'%karamel%' OR
    mp.CanonicalTitle LIKE N'%muz%' OR mp.CanonicalTitle LIKE N'%vanilya%' OR
    mp.CanonicalTitle LIKE N'%nutella%'
  );
GO

-- 2) Şimdi bu ürünleri doğru kategoriye (Market / Süt & Kahvaltılık) ata
UPDATE mp
SET mp.CategoryId = c.Id
FROM MasterProducts mp
JOIN Categories c ON c.Slug = N'market-sut-kahvaltilik'
WHERE mp.CategoryId IS NULL AND (
    mp.CanonicalTitle LIKE N'%fındık kreması%' OR mp.CanonicalTitle LIKE N'%çikolata kreması%' OR
    mp.CanonicalTitle LIKE N'%kakaolu%' OR mp.CanonicalTitle LIKE N'%nutella%'
);
GO

-- Kontrol etmek istersen:
-- SELECT Id, CanonicalTitle, Brand, CategoryId FROM MasterProducts WHERE CanonicalTitle LIKE N'%nutella%';
