-- SectorClassifier.KozmetikSectorName used to be "Sağlık, Bakım, Kozmetik", a spelling that
-- matched neither store's real breadcrumb text. CategoryResolver.ResolveBreadcrumbOnlyAsync
-- feeds CosmeticsCategorySynonyms' canonical spelling back into FindOrCreateAsync scoped to
-- that sector root, so any mismatch there made every scrape run create a redundant sibling
-- "cosmetics root" node one level below the real sector root instead of collapsing into it —
-- first as "Kişisel Bakım, Kozmetik, Sağlık" (Migros's/MacroCenter's real section-root text),
-- then separately as the shorter "Kişisel Bakım" (also just the section root under its own
-- short name). SectorClassifier.cs and CosmeticsCategorySynonyms.cs now map both spellings to
-- the exact same string as the sector root, so this can't recur — this migration merges
-- whichever of these already exist in this database into the real sector root, moving their
-- products and child categories up rather than deleting them.

DECLARE @RootId INT;

SELECT @RootId = Id FROM Categories
WHERE ParentCategoryId IS NULL
AND Name IN (N'Sağlık, Bakım, Kozmetik', N'Kişisel Bakım, Kozmetik, Sağlık');

IF @RootId IS NOT NULL
BEGIN
    DECLARE @DupIds TABLE (Id INT);
    INSERT INTO @DupIds (Id)
    SELECT Id FROM Categories
    WHERE ParentCategoryId = @RootId
    AND Name IN (N'Sağlık, Bakım, Kozmetik', N'Kişisel Bakım, Kozmetik, Sağlık', N'Kişisel Bakım')
    AND Id <> @RootId;

    UPDATE Products SET CategoryId = @RootId WHERE CategoryId IN (SELECT Id FROM @DupIds);
    UPDATE Categories SET ParentCategoryId = @RootId WHERE ParentCategoryId IN (SELECT Id FROM @DupIds);
    DELETE FROM Categories WHERE Id IN (SELECT Id FROM @DupIds);

    UPDATE Categories
    SET Name = N'Kişisel Bakım, Kozmetik, Sağlık', Slug = 'kisisel-bakim-kozmetik-saglik'
    WHERE Id = @RootId;
END
