USE SmartStore;
GO

DELETE c
FROM dbo.OrderTemplateComponents c
INNER JOIN dbo.OrderTemplates ot ON ot.TemplateID = c.TemplateID
WHERE ot.OrderName LIKE N'׳%';

DELETE FROM dbo.OrderTemplates
WHERE OrderName LIKE N'׳%';

DECLARE @SeededProducts TABLE (ProductName nvarchar(200) NOT NULL);
INSERT INTO @SeededProducts (ProductName) VALUES
    (N'קופסת מתנה'),
    (N'סרט סאטן'),
    (N'שוקולד מריר'),
    (N'שוקולד חלב'),
    (N'פרלינים'),
    (N'מרשמלו'),
    (N'עוגיות'),
    (N'סוכריות גומי'),
    (N'סוכריות קשות'),
    (N'פיצוחים מעורבים'),
    (N'בוטנים'),
    (N'שקדים'),
    (N'אגוזי מלך'),
    (N'צלופנים'),
    (N'מקלות שוקולד'),
    (N'בקבוק בירה'),
    (N'כוס חד פעמית'),
    (N'נייר אריזה זהב'),
    (N'נייר אריזה ורוד'),
    (N'ברכת מזל טוב'),
    (N'נר יום הולדת'),
    (N'קונפטי'),
    (N'שוקולד לבן'),
    (N'שוקולד זהב'),
    (N'פיצוחים'),
    (N'חטיף שוקולד'),
    (N'עוגת שוקולד'),
    (N'פקאנים'),
    (N'קפה'),
    (N'תה'),
    (N'דבש'),
    (N'עלה זהב אכיל'),
    (N'מגש הגשה'),
    (N'שקית צלופן'),
    (N'סוכריות שוקולד'),
    (N'אריזת מתנה'),
    (N'בירה בוטל'),
    (N'פריכיות'),
    (N'חטיפים'),
    (N'סוכר'),
    (N'קרם שוקולד'),
    (N'שוקולד'),
    (N'פרלין'),
    (N'סוכריות'),
    (N'חטיף'),
    (N'חטיפי שוקולד'),
    (N'אגוזים'),
    (N'שקית'),
    (N'קופסה'),
    (N'אריזה'),
    (N'סרט'),
    (N'ברכה'),
    (N'נר'),
    (N'מגש'),
    (N'בירה'),
    (N'כוס');

DECLARE @SeededOrders TABLE (OrderName nvarchar(200) NOT NULL, Description nvarchar(max) NULL);
INSERT INTO @SeededOrders (OrderName, Description) VALUES
    (N'פצוחים ושוקולדים', N'מארז פצוחים ושוקולדים'),
    (N'מארז פצוחים ושוקולדים', N'מארז פצוחים ושוקולדים'),
    (N'פצוחים אישי', N'מארז פצוחים אישי'),
    (N'פינוקים אישי', N'מארז פינוקים אישי'),
    (N'פינוקי שוקולד', N'מארז פינוקי שוקולד'),
    (N'עלה זהב', N'מארז עלה זהב'),
    (N'מתנה לחבר', N'מארז מתנה לחבר'),
    (N'מתנה ורודה', N'מארז מתנה ורודה'),
    (N'מגש פרות', N'מגש פרות'),
    (N'מגש זהב', N'מגש זהב'),
    (N'מארז שוקולד מהודר', N'מארז שוקולד מהודר'),
    (N'מארז קטן', N'מארז קטן'),
    (N'מארז חנוכה', N'מארז חנוכה'),
    (N'חבילת בירה', N'חבילת בירה'),
    (N'חבילה לחתן', N'חבילה לחתן'),
    (N'מארז יום הולדת', N'מארז יום הולדת'),
    (N'Birthday Bundle', N'Birthday Bundle'),
    (N'Anniversary Bundle', N'Anniversary Bundle');

DELETE c
FROM dbo.OrderTemplateComponents c
INNER JOIN dbo.OrderTemplates ot ON ot.TemplateID = c.TemplateID
INNER JOIN @SeededOrders so ON REPLACE(REPLACE(LTRIM(RTRIM(ot.OrderName)), CHAR(13), N''), CHAR(10), N'') = REPLACE(REPLACE(LTRIM(RTRIM(so.OrderName)), CHAR(13), N''), CHAR(10), N'');

DELETE ot
FROM dbo.OrderTemplates ot
INNER JOIN @SeededOrders so ON REPLACE(REPLACE(LTRIM(RTRIM(ot.OrderName)), CHAR(13), N''), CHAR(10), N'') = REPLACE(REPLACE(LTRIM(RTRIM(so.OrderName)), CHAR(13), N''), CHAR(10), N'');

MERGE dbo.Products AS target
USING (VALUES
    (N'קופסת מתנה'),
    (N'סרט סאטן'),
    (N'שוקולד מריר'),
    (N'שוקולד חלב'),
    (N'פרלינים'),
    (N'מרשמלו'),
    (N'עוגיות'),
    (N'סוכריות גומי'),
    (N'סוכריות קשות'),
    (N'פיצוחים מעורבים'),
    (N'בוטנים'),
    (N'שקדים'),
    (N'אגוזי מלך'),
    (N'צלופנים'),
    (N'מקלות שוקולד'),
    (N'בקבוק בירה'),
    (N'כוס חד פעמית'),
    (N'נייר אריזה זהב'),
    (N'נייר אריזה ורוד'),
    (N'ברכת מזל טוב'),
    (N'נר יום הולדת'),
    (N'קונפטי'),
    (N'שוקולד לבן'),
    (N'שוקולד זהב'),
    (N'פיצוחים'),
    (N'חטיף שוקולד'),
    (N'עוגת שוקולד'),
    (N'פקאנים'),
    (N'קפה'),
    (N'תה'),
    (N'דבש'),
    (N'עלה זהב אכיל'),
    (N'מגש הגשה'),
    (N'שקית צלופן'),
    (N'סוכריות שוקולד'),
    (N'אריזת מתנה'),
    (N'בירה בוטל'),
    (N'פריכיות'),
    (N'חטיפים'),
    (N'סוכר'),
    (N'קרם שוקולד'),
    (N'שוקולד'),
    (N'פרלין'),
    (N'סוכריות'),
    (N'חטיף'),
    (N'חטיפי שוקולד'),
    (N'אגוזים'),
    (N'שקית'),
    (N'קופסה'),
    (N'אריזה'),
    (N'סרט'),
    (N'ברכה'),
    (N'נר'),
    (N'מגש'),
    (N'בירה'),
    (N'כוס')
) AS source(ProductName)
ON target.ProductName = source.ProductName
WHEN NOT MATCHED THEN
    INSERT (ProductName, CurrentQuantity, MinQuantity)
    VALUES (source.ProductName, 50, 10);

UPDATE dbo.Products
SET CurrentQuantity = CASE
        WHEN CurrentQuantity < MinQuantity THEN MinQuantity * 5
        ELSE CurrentQuantity
    END
WHERE ProductName IN (SELECT ProductName FROM @SeededProducts);
GO

CREATE OR ALTER PROCEDURE dbo.AddOrderComponent
    @OrderName nvarchar(200),
    @ProductName nvarchar(200),
    @QuantityRequired int
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cleanOrderName nvarchar(200) = REPLACE(REPLACE(LTRIM(RTRIM(@OrderName)), CHAR(13), N''), CHAR(10), N'');
    DECLARE @cleanProductName nvarchar(200) = REPLACE(REPLACE(LTRIM(RTRIM(@ProductName)), CHAR(13), N''), CHAR(10), N'');

    DECLARE @TemplateID int;
    DECLARE @ProductID int;

    SELECT @TemplateID = TemplateID
    FROM dbo.OrderTemplates
    WHERE REPLACE(REPLACE(LTRIM(RTRIM(OrderName)), CHAR(13), N''), CHAR(10), N'') = @cleanOrderName;

    SELECT @ProductID = ProductID
    FROM dbo.Products
    WHERE REPLACE(REPLACE(LTRIM(RTRIM(ProductName)), CHAR(13), N''), CHAR(10), N'') = @cleanProductName;

    IF @TemplateID IS NOT NULL AND @ProductID IS NOT NULL
    BEGIN
        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.OrderTemplateComponents
            WHERE TemplateID = @TemplateID AND ProductID = @ProductID
        )
        BEGIN
            INSERT INTO dbo.OrderTemplateComponents (TemplateID, ProductID, QuantityRequired)
            VALUES (@TemplateID, @ProductID, @QuantityRequired);
        END
    END
END
GO

MERGE dbo.OrderTemplates AS target
USING (VALUES
    (N'פצוחים ושוקולדים', N'מארז פצוחים ושוקולדים'),
    (N'מארז פצוחים ושוקולדים', N'מארז פצוחים ושוקולדים'),
    (N'פצוחים אישי', N'מארז פצוחים אישי'),
    (N'פינוקים אישי', N'מארז פינוקים אישי'),
    (N'פינוקי שוקולד', N'מארז פינוקי שוקולד'),
    (N'עלה זהב', N'מארז עלה זהב'),
    (N'מתנה לחבר', N'מארז מתנה לחבר'),
    (N'מתנה ורודה', N'מארז מתנה ורודה'),
    (N'מגש פרות', N'מגש פרות'),
    (N'מגש זהב', N'מגש זהב'),
    (N'מארז שוקולד מהודר', N'מארז שוקולד מהודר'),
    (N'מארז קטן', N'מארז קטן'),
    (N'מארז חנוכה', N'מארז חנוכה'),
    (N'חבילת בירה', N'חבילת בירה'),
    (N'חבילה לחתן', N'חבילה לחתן'),
    (N'מארז יום הולדת', N'מארז יום הולדת'),
    (N'Birthday Bundle', N'Birthday Bundle'),
    (N'Anniversary Bundle', N'Anniversary Bundle')
) AS source(OrderName, Description)
ON REPLACE(REPLACE(LTRIM(RTRIM(target.OrderName)), CHAR(13), N''), CHAR(10), N'') =
   REPLACE(REPLACE(LTRIM(RTRIM(source.OrderName)), CHAR(13), N''), CHAR(10), N'')
WHEN NOT MATCHED THEN
    INSERT (OrderName, Description)
    VALUES (source.OrderName, source.Description);
GO

EXEC dbo.AddOrderComponent @OrderName = N'פצוחים ושוקולדים', @ProductName = N'פיצוחים מעורבים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים ושוקולדים', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים ושוקולדים', @ProductName = N'שוקולד חלב', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים ושוקולדים', @ProductName = N'שקדים', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים ושוקולדים', @ProductName = N'קופסת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מארז פצוחים ושוקולדים', @ProductName = N'פיצוחים מעורבים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז פצוחים ושוקולדים', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז פצוחים ושוקולדים', @ProductName = N'שוקולד חלב', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז פצוחים ושוקולדים', @ProductName = N'שקדים', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מארז פצוחים ושוקולדים', @ProductName = N'קופסת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'פצוחים אישי', @ProductName = N'פיצוחים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים אישי', @ProductName = N'בוטנים', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים אישי', @ProductName = N'שקדים', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים אישי', @ProductName = N'שוקולד מריר', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'פצוחים אישי', @ProductName = N'שקית צלופן', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'פינוקים אישי', @ProductName = N'מרשמלו', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקים אישי', @ProductName = N'סוכריות גומי', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקים אישי', @ProductName = N'סוכריות קשות', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקים אישי', @ProductName = N'עוגיות', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקים אישי', @ProductName = N'אריזת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'פינוקי שוקולד', @ProductName = N'שוקולד מריר', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקי שוקולד', @ProductName = N'שוקולד חלב', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקי שוקולד', @ProductName = N'פרלינים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקי שוקולד', @ProductName = N'מקלות שוקולד', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'פינוקי שוקולד', @ProductName = N'קופסת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'עלה זהב', @ProductName = N'עלה זהב אכיל', @QuantityRequired = 4;
EXEC dbo.AddOrderComponent @OrderName = N'עלה זהב', @ProductName = N'שוקולד לבן', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'עלה זהב', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'עלה זהב', @ProductName = N'פרלינים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'עלה זהב', @ProductName = N'נייר אריזה זהב', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מתנה לחבר', @ProductName = N'חטיף שוקולד', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה לחבר', @ProductName = N'סוכריות שוקולד', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה לחבר', @ProductName = N'קפה', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה לחבר', @ProductName = N'ברכת מזל טוב', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה לחבר', @ProductName = N'אריזת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מתנה ורודה', @ProductName = N'מרשמלו', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה ורודה', @ProductName = N'סוכריות גומי', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה ורודה', @ProductName = N'נייר אריזה ורוד', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה ורודה', @ProductName = N'סרט סאטן', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מתנה ורודה', @ProductName = N'קופסת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מגש פרות', @ProductName = N'מגש הגשה', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מגש פרות', @ProductName = N'פיצוחים מעורבים', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מגש פרות', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מגש פרות', @ProductName = N'שוקולד חלב', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מגש פרות', @ProductName = N'סוכריות קשות', @QuantityRequired = 2;

EXEC dbo.AddOrderComponent @OrderName = N'מגש זהב', @ProductName = N'מגש הגשה', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מגש זהב', @ProductName = N'נייר אריזה זהב', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מגש זהב', @ProductName = N'שוקולד זהב', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מגש זהב', @ProductName = N'פרלינים', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מגש זהב', @ProductName = N'אגוזי מלך', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מארז שוקולד מהודר', @ProductName = N'שוקולד מריר', @QuantityRequired = 4;
EXEC dbo.AddOrderComponent @OrderName = N'מארז שוקולד מהודר', @ProductName = N'שוקולד חלב', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מארז שוקולד מהודר', @ProductName = N'פרלינים', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מארז שוקולד מהודר', @ProductName = N'קרם שוקולד', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מארז שוקולד מהודר', @ProductName = N'קופסת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מארז קטן', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז קטן', @ProductName = N'סוכריות שוקולד', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז קטן', @ProductName = N'מרשמלו', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז קטן', @ProductName = N'שקית צלופן', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מארז חנוכה', @ProductName = N'עוגיות', @QuantityRequired = 4;
EXEC dbo.AddOrderComponent @OrderName = N'מארז חנוכה', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז חנוכה', @ProductName = N'שוקולד חלב', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'מארז חנוכה', @ProductName = N'סוכר', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מארז חנוכה', @ProductName = N'אריזת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'חבילת בירה', @ProductName = N'בירה בוטל', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'חבילת בירה', @ProductName = N'פיצוחים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'חבילת בירה', @ProductName = N'בוטנים', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'חבילת בירה', @ProductName = N'כוס חד פעמית', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'חבילת בירה', @ProductName = N'אריזת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'חבילה לחתן', @ProductName = N'בירה בוטל', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'חבילה לחתן', @ProductName = N'פיצוחים מעורבים', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'חבילה לחתן', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'חבילה לחתן', @ProductName = N'ברכת מזל טוב', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'חבילה לחתן', @ProductName = N'אריזת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'מארז יום הולדת', @ProductName = N'נר יום הולדת', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מארז יום הולדת', @ProductName = N'קונפטי', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'מארז יום הולדת', @ProductName = N'שוקולד חלב', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מארז יום הולדת', @ProductName = N'סוכריות גומי', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'מארז יום הולדת', @ProductName = N'ברכת מזל טוב', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'Birthday Bundle', @ProductName = N'נר יום הולדת', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'Birthday Bundle', @ProductName = N'קונפטי', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'Birthday Bundle', @ProductName = N'שוקולד מריר', @QuantityRequired = 2;
EXEC dbo.AddOrderComponent @OrderName = N'Birthday Bundle', @ProductName = N'סוכריות שוקולד', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'Birthday Bundle', @ProductName = N'אריזת מתנה', @QuantityRequired = 1;

EXEC dbo.AddOrderComponent @OrderName = N'Anniversary Bundle', @ProductName = N'שוקולד מריר', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'Anniversary Bundle', @ProductName = N'פרלינים', @QuantityRequired = 3;
EXEC dbo.AddOrderComponent @OrderName = N'Anniversary Bundle', @ProductName = N'דבש', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'Anniversary Bundle', @ProductName = N'קפה', @QuantityRequired = 1;
EXEC dbo.AddOrderComponent @OrderName = N'Anniversary Bundle', @ProductName = N'ברכת מזל טוב', @QuantityRequired = 1;
GO
