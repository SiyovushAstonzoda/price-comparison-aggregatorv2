-- "Adet" (pack count) is no longer a filter — a raw piece count isn't a meaningful shopping
-- facet the way Hacim/Ağırlık ranges are, and MatchingService/SizeFormatter no longer write it
-- for new products. /api/attributes already excludes AttributeName = 'Adet' defensively, but the
-- old rows are dead weight; this deletes them. Run manually against the target SQL Server
-- instance (no migration tooling in this repo).

DELETE FROM ProductAttributes WHERE AttributeName = 'Adet';
