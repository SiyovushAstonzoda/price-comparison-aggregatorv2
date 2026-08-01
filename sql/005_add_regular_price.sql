-- Adds RegularPrice (pre-discount price) alongside the existing Price/CurrentPrice columns,
-- on top of the normalized schema from 003_normalize_schema.sql. Run manually against the
-- target SQL Server instance (no migration tooling in this repo).
--
-- Every scraper already parses this from the source store's API (see ProductDto.RegularPrice)
-- but it was previously discarded before reaching the DB. NULL means "no discount" or
-- "source doesn't expose it" — only a real RegularPrice > Price counts as a campaign.

IF COL_LENGTH('dbo.SellerProducts', 'RegularPrice') IS NULL
    ALTER TABLE SellerProducts ADD RegularPrice DECIMAL(18,2) NULL;

IF COL_LENGTH('dbo.Offers', 'RegularPrice') IS NULL
    ALTER TABLE Offers ADD RegularPrice DECIMAL(18,2) NULL;
