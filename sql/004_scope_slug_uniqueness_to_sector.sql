-- Slug had a bare UNIQUE constraint, so two different sectors could never both have
-- a category named e.g. "Elektronik" — CategoryResolver.FindOrCreateAsync would fail
-- to INSERT the new sector's row (unique violation) and its fallback would return the
-- *other* sector's existing row, silently misfiling the product (see SectorId, added in
-- 003). Uniqueness now applies per sector instead of globally.
ALTER TABLE Categories DROP CONSTRAINT UQ__Categori__BC7B5FB636AD4870;
GO

CREATE UNIQUE INDEX UQ_Categories_Slug_SectorId ON Categories (Slug, SectorId);
GO
