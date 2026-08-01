-- Clears the hand-seeded placeholder categories from 001. Safe: no MasterProducts
-- reference any Category yet at the time this was written. Categories are now
-- created on the fly from real source data by PriceAggregator.Core.CategoryResolver.
DELETE FROM Categories;
DBCC CHECKIDENT ('Categories', RESEED, 0);
