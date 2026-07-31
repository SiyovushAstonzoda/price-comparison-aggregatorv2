-- FurnitureCategoryClassifier used naive substring/prefix keyword matching against noisy
-- product titles (not just controlled category vocabulary), which produced a handful of
-- personal-care/appliance products misfiled under the Mobilya sector:
--   * "koltuk" matched the "koltuk altı" (underarm) idiom in deodorant/wax-strip titles,
--     filing them under the furniture "Koltuk" bucket instead of Market's "Kişisel Bakım".
--   * the generic "aksesuar" catch-all matched a "Saç Aksesuarları" (hair accessories)
--     breadcrumb, filing a hair clip under furniture "Dekoratif Aksesuar" instead of the
--     real "Kişisel Bakım > Saç Tarayıcılar > Toka" category that already existed.
--   * an oven's own "Lambalı" (has-a-lamp) spec matched the "Aydınlatma" (lighting) rule's
--     "lamba" keyword, filing it under a "Fırın" leaf inside the lighting bucket.
-- FurnitureCategoryClassifier.cs now excludes/redirects all three going forward; this
-- one-time fix moves the specific already-scraped rows to the categories they'd resolve to
-- under the corrected classifier. Run manually against the target SQL Server instance (no
-- migration tooling in this repo).

-- Veet Pure Bikini Ve Koltuk Altı Ağda Bandı 16'lı -> Market > Kişisel Bakım
UPDATE Products SET CategoryId = 44 WHERE Id = 879;

-- NIVEA Kadın Stick Deodorant ... Koltuk Altı Kararmasına Karşı 50 Ml -> Market > Kişisel Bakım
UPDATE Products SET CategoryId = 44 WHERE Id = 880;

-- Akel AF930LF ... Lambalı ... Midi Fırın -> Market > Elektrikli Ev Aletleri
UPDATE Products SET CategoryId = 75 WHERE Id = 1118;

-- Rengarenk Ekose 2'li Maşa Toka Mn23 -> existing Sağlık/Bakım/Kozmetik > Kişisel Bakım >
-- Saç Tarayıcılar > Toka node (Id 1211), instead of the duplicate Mobilya > Dekoratif
-- Aksesuar > Toka node this scrape had created.
UPDATE Products SET CategoryId = 1211 WHERE Id = 765;

-- The four Mobilya-sector leaf categories these rows moved out of are now empty (1 product,
-- 0 children each, verified before running this) and would otherwise sit in the Mobilya mega
-- menu as dead, product-less links — /api/categories returns every row unconditionally.
DELETE FROM Categories WHERE Id IN (117, 148, 149, 184);
