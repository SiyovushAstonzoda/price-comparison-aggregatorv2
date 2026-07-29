-- Run once on Aggregator DB if MidCategory / ProductType columns are missing.
IF COL_LENGTH('Products', 'MidCategory') IS NULL
    ALTER TABLE Products ADD MidCategory NVARCHAR(256) NULL;

IF COL_LENGTH('Products', 'ProductType') IS NULL
    ALTER TABLE Products ADD ProductType NVARCHAR(256) NULL;
