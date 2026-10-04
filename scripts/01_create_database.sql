USE master;
GO

IF DB_ID(N'SmartStore') IS NULL
BEGIN
    CREATE DATABASE SmartStore;
END
GO

USE SmartStore;
GO

IF OBJECT_ID(N'dbo.OrderTemplateComponents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderTemplateComponents
    (
        TemplateID int NOT NULL,
        ProductID int NOT NULL,
        QuantityRequired int NOT NULL,
        CONSTRAINT PK_OrderTemplateComponents PRIMARY KEY (TemplateID, ProductID)
    );
END
GO

IF OBJECT_ID(N'dbo.OrderTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderTemplates
    (
        TemplateID int IDENTITY(1,1) NOT NULL,
        OrderName nvarchar(200) NOT NULL,
        Description nvarchar(max) NULL,
        CONSTRAINT PK_OrderTemplates PRIMARY KEY (TemplateID)
    );
END
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        ProductID int IDENTITY(1,1) NOT NULL,
        ProductName nvarchar(200) NOT NULL,
        CurrentQuantity int NOT NULL CONSTRAINT DF_Products_CurrentQuantity DEFAULT (0),
        MinQuantity int NOT NULL CONSTRAINT DF_Products_MinQuantity DEFAULT (10),
        CONSTRAINT PK_Products PRIMARY KEY (ProductID)
    );
END
GO

IF OBJECT_ID(N'dbo.PurchaseRecommendations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseRecommendations
    (
        RecommendationID int IDENTITY(1,1) NOT NULL,
        ProductID int NOT NULL,
        SupplierName nvarchar(200) NOT NULL,
        Price decimal(18,2) NOT NULL,
        PurchaseUrl nvarchar(1000) NULL,
        CreatedDate datetime2 NOT NULL CONSTRAINT DF_PurchaseRecommendations_CreatedDate DEFAULT (SYSUTCDATETIME()),
        Status nvarchar(50) NOT NULL CONSTRAINT DF_PurchaseRecommendations_Status DEFAULT (N'Pending'),
        CONSTRAINT PK_PurchaseRecommendations PRIMARY KEY (RecommendationID)
    );
END
GO

IF OBJECT_ID(N'dbo.AI_Logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AI_Logs
    (
        LogID int IDENTITY(1,1) NOT NULL,
        TransactionDate datetime2 NOT NULL CONSTRAINT DF_AI_Logs_TransactionDate DEFAULT (SYSUTCDATETIME()),
        ImageName nvarchar(200) NULL,
        AI_Analysis nvarchar(max) NULL,
        CONSTRAINT PK_AI_Logs PRIMARY KEY (LogID)
    );
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetOrderComponentsByName
    @OrderName nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cleanOrderName nvarchar(200) = REPLACE(REPLACE(LTRIM(RTRIM(@OrderName)), CHAR(13), N''), CHAR(10), N'');

    SELECT
        p.ProductID,
        p.ProductName,
        c.QuantityRequired,
        p.CurrentQuantity,
        p.MinQuantity
    FROM dbo.OrderTemplates ot
    INNER JOIN dbo.OrderTemplateComponents c ON c.TemplateID = ot.TemplateID
    INNER JOIN dbo.Products p ON p.ProductID = c.ProductID
    WHERE REPLACE(REPLACE(LTRIM(RTRIM(ot.OrderName)), CHAR(13), N''), CHAR(10), N'') = @cleanOrderName
    ORDER BY p.ProductName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertRecommendation
    @ProductID int,
    @SupplierName nvarchar(200),
    @Price decimal(18,2),
    @PurchaseUrl nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PurchaseRecommendations
        (ProductID, SupplierName, Price, PurchaseUrl)
    VALUES
        (@ProductID, @SupplierName, @Price, @PurchaseUrl);
END
GO
