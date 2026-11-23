-- Generated from EF Core SQL Server model. Run on an empty TechShopDT01 database.
CREATE TABLE [AspNetRoles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetUsers] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Brands] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(80) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Brands] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(80) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Coupons] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(30) NOT NULL,
    [DiscountPercent] int NOT NULL,
    [ExpiredAt] datetime2 NOT NULL,
    [UsageLimit] int NOT NULL,
    [UsedCount] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Coupon_Usage] CHECK (UsedCount >= 0 AND UsedCount <= UsageLimit)
);
GO


CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Addresses] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [Recipient] nvarchar(100) NOT NULL,
    [Phone] nvarchar(20) NOT NULL,
    [Detail] nvarchar(300) NOT NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Addresses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserTokens] (
    [UserId] int NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Orders] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [IdempotencyKey] uniqueidentifier NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [ShippingAddress] nvarchar(max) NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    [Discount] decimal(18,2) NOT NULL,
    [ShippingFee] decimal(18,2) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [CouponCode] nvarchar(max) NULL,
    [CouponId] int NULL,
    [Status] nvarchar(max) NOT NULL,
    [PaymentMethod] nvarchar(max) NOT NULL,
    [PaymentStatus] nvarchar(max) NOT NULL,
    [PaymentTransactionId] nvarchar(max) NULL,
    [StockRestored] bit NOT NULL,
    [InternalNote] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [SalePrice] decimal(18,2) NULL,
    [Stock] int NOT NULL,
    [CategoryId] int NOT NULL,
    [BrandId] int NULL,
    [Description] nvarchar(4000) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Product_Price] CHECK (Price > 0),
    CONSTRAINT [CK_Product_Stock] CHECK (Stock >= 0),
    CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]),
    CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [OrderHistory] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Note] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OrderHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderHistory_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ReturnRequests] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [UserId] int NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ReturnRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReturnRequests_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [OrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ProductId] int NOT NULL,
    [ProductName] nvarchar(max) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ProductImages] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [Url] nvarchar(300) NOT NULL,
    [IsMain] bit NOT NULL,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ProductSpecs] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [SpecName] nvarchar(80) NOT NULL,
    [SpecValue] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_ProductSpecs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductSpecs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Reviews] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [UserId] int NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(1000) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_Addresses_UserId] ON [Addresses] ([UserId]);
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO


CREATE UNIQUE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Brands_Name] ON [Brands] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
GO


CREATE INDEX [IX_OrderHistory_OrderId] ON [OrderHistory] ([OrderId]);
GO


CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
GO


CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
GO


CREATE UNIQUE INDEX [IX_Orders_IdempotencyKey] ON [Orders] ([IdempotencyKey]);
GO


CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);
GO


CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
GO


CREATE INDEX [IX_ProductSpecs_ProductId] ON [ProductSpecs] ([ProductId]);
GO


CREATE INDEX [IX_Products_BrandId] ON [Products] ([BrandId]);
GO


CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
GO


CREATE INDEX [IX_Products_IsActive_CategoryId] ON [Products] ([IsActive], [CategoryId]);
GO


CREATE UNIQUE INDEX [IX_ReturnRequests_OrderId] ON [ReturnRequests] ([OrderId]);
GO


CREATE UNIQUE INDEX [IX_Reviews_ProductId_UserId] ON [Reviews] ([ProductId], [UserId]);
GO


CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
GO



