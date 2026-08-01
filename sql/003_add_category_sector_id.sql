-- Categories from unrelated sectors (Market vs Mobilya) could end up sharing a parent
-- when a furniture-store category name (e.g. "Elektronik") happened to match a name
-- already used somewhere in the grocery tree — CategoryResolver.FindOrCreateAsync's
-- exact-slug lookup had no sector boundary, so it would silently reuse that unrelated
-- node. SectorId lets that lookup stay scoped to the product's own sector; it's the Id
-- of the top-level root (Market/Mobilya/...) each category structurally descends from,
-- and a root row's SectorId is its own Id.
ALTER TABLE Categories ADD SectorId INT NULL;
GO

;WITH RootChain AS (
    SELECT Id, ParentCategoryId, Id AS RootId
    FROM Categories
    WHERE ParentCategoryId IS NULL
    UNION ALL
    SELECT c.Id, c.ParentCategoryId, rc.RootId
    FROM Categories c
    JOIN RootChain rc ON c.ParentCategoryId = rc.Id
)
UPDATE cat
SET cat.SectorId = rc.RootId
FROM Categories cat
JOIN RootChain rc ON rc.Id = cat.Id;
GO
