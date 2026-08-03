/****** Nesnesi: Table [dbo].[Brands] Betik Tarihi: 31.07.2026 16:04:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Brands](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[LogoUrl] [nvarchar](max) NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[Categories] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Categories](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[ParentCategoryId] [int] NULL,
	[SectorId] [int] NULL,
	[Slug] [nvarchar](255) NOT NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[Offers] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Offers](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[SellerId] [int] NOT NULL,
	[SellerProductId] [int] NOT NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[CargoPrice] [decimal](18, 2) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[LastUpdatedAt] [datetime] NOT NULL,
	[TotalCost]  AS ([Price]+[CargoPrice]) PERSISTED,
	[RegularPrice] [decimal](18, 2) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Offers_Product_Seller] UNIQUE NONCLUSTERED 
(
	[ProductId] ASC,
	[SellerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[OffersPriceHistory] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OffersPriceHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OfferId] [int] NOT NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[RecordedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[ProductAttributes] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductAttributes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[AttributeName] [nvarchar](50) NOT NULL,
	[AttributeValue] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ProductAttributes] UNIQUE NONCLUSTERED 
(
	[ProductId] ASC,
	[AttributeName] ASC,
	[AttributeValue] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[ProductMatchingLogs] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductMatchingLogs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SellerProductId] [int] NOT NULL,
	[ProductId] [int] NOT NULL,
	[Status] [nvarchar](50) NOT NULL,
	[MatchedBy] [nvarchar](100) NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
	[Notes] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[Products] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Products](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[BrandId] [int] NOT NULL,
	[CategoryId] [int] NULL,
	[Name] [nvarchar](500) NOT NULL,
	[Sku] [nvarchar](100) NULL,
	[ImageUrl] [nvarchar](max) NULL,
	[SizeValue] [decimal](18, 3) NULL,
	[SizeUnit] [nvarchar](50) NULL,
	[PackQuantity] [int] NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[SellerProducts] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SellerProducts](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SellerId] [int] NOT NULL,
	[SellerProductCode] [nvarchar](255) NOT NULL,
	[ExternalTitle] [nvarchar](500) NOT NULL,
	[ExternalUrl] [nvarchar](max) NULL,
	[ExternalImageUrl] [nvarchar](max) NULL,
	[CurrentPrice] [decimal](18, 2) NOT NULL,
	[UnitType] [nvarchar](20) NULL,
	[UnitAmount] [decimal](18, 3) NULL,
	[SourceUnitPrice] [decimal](18, 2) NULL,
	[LastScrapedAt] [datetime] NOT NULL,
	[RegularPrice] [decimal](18, 2) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_SellerProducts_Seller_Code] UNIQUE NONCLUSTERED 
(
	[SellerId] ASC,
	[SellerProductCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


/****** Nesnesi: Table [dbo].[Sellers] Betik Tarihi: 31.07.2026 16:04:19 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Sellers](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[WebSiteUrl] [nvarchar](max) NULL,
	[Rating] [decimal](3, 2) NOT NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


ALTER TABLE [dbo].[Brands] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO


ALTER TABLE [dbo].[Categories] ADD  DEFAULT ((1)) FOR [IsActive]
GO


ALTER TABLE [dbo].[Offers] ADD  DEFAULT ((0.00)) FOR [CargoPrice]
GO


ALTER TABLE [dbo].[Offers] ADD  DEFAULT ((1)) FOR [IsActive]
GO


ALTER TABLE [dbo].[Offers] ADD  DEFAULT (getdate()) FOR [LastUpdatedAt]
GO


ALTER TABLE [dbo].[OffersPriceHistory] ADD  DEFAULT (getdate()) FOR [RecordedAt]
GO


ALTER TABLE [dbo].[ProductMatchingLogs] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO


ALTER TABLE [dbo].[Products] ADD  DEFAULT ((1)) FOR [PackQuantity]
GO


ALTER TABLE [dbo].[Products] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO


ALTER TABLE [dbo].[SellerProducts] ADD  DEFAULT (getdate()) FOR [LastScrapedAt]
GO


ALTER TABLE [dbo].[Sellers] ADD  DEFAULT ((0.00)) FOR [Rating]
GO


ALTER TABLE [dbo].[Sellers] ADD  DEFAULT ((1)) FOR [IsActive]
GO


ALTER TABLE [dbo].[Categories]  WITH CHECK ADD FOREIGN KEY([ParentCategoryId])
REFERENCES [dbo].[Categories] ([Id])
GO


ALTER TABLE [dbo].[Categories]  WITH CHECK ADD FOREIGN KEY([SectorId])
REFERENCES [dbo].[Categories] ([Id])
GO


ALTER TABLE [dbo].[Offers]  WITH CHECK ADD FOREIGN KEY([ProductId])
REFERENCES [dbo].[Products] ([Id])
GO


ALTER TABLE [dbo].[Offers]  WITH CHECK ADD FOREIGN KEY([SellerId])
REFERENCES [dbo].[Sellers] ([Id])
GO


ALTER TABLE [dbo].[Offers]  WITH CHECK ADD FOREIGN KEY([SellerProductId])
REFERENCES [dbo].[SellerProducts] ([Id])
GO


ALTER TABLE [dbo].[OffersPriceHistory]  WITH CHECK ADD FOREIGN KEY([OfferId])
REFERENCES [dbo].[Offers] ([Id])
ON DELETE CASCADE
GO


ALTER TABLE [dbo].[ProductAttributes]  WITH CHECK ADD FOREIGN KEY([ProductId])
REFERENCES [dbo].[Products] ([Id])
GO


ALTER TABLE [dbo].[ProductMatchingLogs]  WITH CHECK ADD FOREIGN KEY([ProductId])
REFERENCES [dbo].[Products] ([Id])
GO


ALTER TABLE [dbo].[ProductMatchingLogs]  WITH CHECK ADD FOREIGN KEY([SellerProductId])
REFERENCES [dbo].[SellerProducts] ([Id])
GO


ALTER TABLE [dbo].[Products]  WITH CHECK ADD FOREIGN KEY([BrandId])
REFERENCES [dbo].[Brands] ([Id])
GO


ALTER TABLE [dbo].[Products]  WITH CHECK ADD FOREIGN KEY([CategoryId])
REFERENCES [dbo].[Categories] ([Id])
GO


ALTER TABLE [dbo].[SellerProducts]  WITH CHECK ADD FOREIGN KEY([SellerId])
REFERENCES [dbo].[Sellers] ([Id])
GO
