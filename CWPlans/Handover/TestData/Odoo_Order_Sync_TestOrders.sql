/*
  Integration.Odoo — test orders for the order send (CWPlans/Odoo_Order_Sync_Spec.md §8, T7–T23)

  Unlike Odoo_Order_Sync_Demo.sql, these orders are meant to be SENT: pressing Sync on them creates real sale orders
  (and, for the home address, a delivery contact) on the Odoo test instance. Run on a local development database only.

  Every order is a copy of @SourceOrderId's header (customer, billing address, payment method), with its own lines,
  shipping, totals, payment status and store. Numbers are TEST-nn after the test case they serve:

    TEST-07   plain, store 2, Beanie x2 + STKS 420, shipping to the branch's address, paid          T7, T8, T16, T22
    TEST-12A  plain, home address (12 Home Street), paid                                                 T12
    TEST-12B  plain, the same home address in a second address row, paid                                 T12, T22a
    TEST-13   plain, ordered by a customer in no branch                                                  T13
    TEST-14   plain, a line whose product has variants but no combination chosen (no Odoo variant)       T14
    TEST-15   plain, with shipping, payment authorised (kept out of "Paid")                              T15
    TEST-22C  plain, store 3 (PackName, Subsidy, Exchanges switched off there), STKS 420 x3 at 10.00   T20, T22c, T23
    TEST-22D  plain, store 3, STKS 420 x6 at 10.00 (Odoo 6 x 1.67 = 10.02: two cents, a warning)      T22c
    TEST-09   packname, two destinations (Alice: the branch; Bob: home), PO-100 on both, print name on a
              line, settlement row (subsidy 30.00, store credit 5.00)                                     T9, T10, T11, T19
    TEST-10   packname, one destination, Alice (PO-100) and Carol (PO-200)                               T10

  The ordering customer of every order but TEST-13 is linked here to the branch @BranchId (head office card
  code 57 = the Odoo company), and the packnames are created in a list of that branch.

  Clean-up only: run the block marked CLEAN-UP on its own. It does not touch Odoo: cancel the Odoo orders there.
*/

SET NOCOUNT ON;

DECLARE @SourceOrderId int = 1;
DECLARE @CustomerId int = 28;          -- the ordering customer, linked to @BranchId
DECLARE @NoBranchCustomerId int = 1;   -- a customer in no branch (TEST-13)
DECLARE @BranchId int = 2;             -- a branch of store 2, under head office card code 57
DECLARE @BranchAddressId int = 4;      -- the branch's address: 5 Test Lane, Brisbane 4000

IF NOT EXISTS (SELECT 1 FROM [Order] WHERE Id = @SourceOrderId)
    THROW 50000, 'The source order does not exist: set @SourceOrderId to an existing order.', 1;

BEGIN TRANSACTION;

-- ── CLEAN-UP ────────────────────────────────────────────────────────────────────────────────────
DECLARE @Old TABLE (Id int);
INSERT INTO @Old SELECT Id FROM [Order] WHERE CustomOrderNumber LIKE 'TEST-%';
DELETE FROM Odoo_OrderMapping WHERE NopOrderId IN (SELECT Id FROM @Old);
DELETE FROM CORE_PN_OrderSettlement WHERE OrderId IN (SELECT Id FROM @Old);
DELETE FROM CORE_PN_OrderShipping WHERE OrderId IN (SELECT Id FROM @Old);
DELETE p FROM CORE_PN_PackNameOrderItem p JOIN OrderItem i ON i.Id = p.OrderItemId WHERE i.OrderId IN (SELECT Id FROM @Old);
DELETE FROM OrderItem WHERE OrderId IN (SELECT Id FROM @Old);
DELETE FROM OrderNote WHERE OrderId IN (SELECT Id FROM @Old);
DELETE FROM [Order] WHERE Id IN (SELECT Id FROM @Old);
DELETE FROM Address WHERE Company = 'TEST-ODOO';
DELETE FROM CORE_PN_PackName WHERE PackNameListId IN (SELECT Id FROM CORE_PN_PackNameList WHERE Name = 'TEST Odoo packnames');
DELETE FROM CORE_PN_PackNameList WHERE Name = 'TEST Odoo packnames';
DELETE FROM Setting WHERE StoreId = 3 AND Name IN ('multistoresettings.packnameenable', 'multistoresettings.subsidyenable', 'multistoresettings.exchangesenable');
-- ── end of CLEAN-UP ─────────────────────────────────────────────────────────────────────────────

-- The ordering customer in the branch
IF EXISTS (SELECT 1 FROM CORE_CustomerBranch WHERE Id = @CustomerId)
    UPDATE CORE_CustomerBranch SET BranchId = @BranchId, LastModifiedDateUtc = SYSUTCDATETIME() WHERE Id = @CustomerId;
ELSE
    INSERT INTO CORE_CustomerBranch (Id, BranchId, JimCardCode, LastModifiedDateUtc) VALUES (@CustomerId, @BranchId, NULL, SYSUTCDATETIME());

-- T23: store 3 with its features off
INSERT INTO Setting (Name, Value, StoreId) VALUES
    ('multistoresettings.packnameenable', 'False', 3),
    ('multistoresettings.subsidyenable', 'False', 3),
    ('multistoresettings.exchangesenable', 'False', 3);

-- Addresses: a copy of the branch address per order that ships there, and the home address (twice, for T12).
-- Company 'TEST-ODOO' marks them for the clean-up; the send names a new delivery contact by first and last name.
DECLARE @AddressColumns nvarchar(max) =
    (SELECT STRING_AGG(QUOTENAME(name), ', ') FROM sys.columns WHERE object_id = OBJECT_ID('Address') AND is_identity = 0);
DECLARE @Sql nvarchar(max), @NewId int;

DECLARE @Addr TABLE (Tag varchar(20), AddressId int);
DECLARE @Tag varchar(20);
DECLARE addr CURSOR LOCAL FAST_FORWARD FOR
    SELECT v FROM (VALUES ('BRANCH-07'), ('BRANCH-15'), ('BRANCH-22C'), ('BRANCH-22D'),('BRANCH-09'), ('BRANCH-10'),
                          ('HOME-12A'), ('HOME-12B'), ('HOME-09'), ('BRANCH-13'), ('BRANCH-14')) t(v);
OPEN addr;
FETCH NEXT FROM addr INTO @Tag;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'INSERT INTO Address (' + @AddressColumns + N') SELECT ' + @AddressColumns
             + N' FROM Address WHERE Id = @BranchAddressId; SET @NewId = SCOPE_IDENTITY();';
    EXEC sp_executesql @Sql, N'@BranchAddressId int, @NewId int OUTPUT', @BranchAddressId, @NewId OUTPUT;

    IF @Tag LIKE 'HOME-%'
        UPDATE Address SET FirstName = 'Jane', LastName = 'Tester', Address1 = '12 Home Street', Address2 = NULL,
                           City = 'Brisbane', ZipPostalCode = '4000', Email = 'jane.tester@example.com', PhoneNumber = '0400000000'
        WHERE Id = @NewId;
    UPDATE Address SET Company = 'TEST-ODOO', CreatedOnUtc = SYSUTCDATETIME() WHERE Id = @NewId;

    INSERT INTO @Addr VALUES (@Tag, @NewId);
    FETCH NEXT FROM addr INTO @Tag;
END
CLOSE addr;
DEALLOCATE addr;

-- Packnames, in a list of the branch
INSERT INTO CORE_PN_PackNameList (Name, StoreId, BranchId, Active, AllManagers, ParentManagers, IsDefault, CreatedOnUtc, Deleted)
VALUES ('TEST Odoo packnames', 2, @BranchId, 1, 0, 0, 0, SYSUTCDATETIME(), 0);
DECLARE @ListId int = SCOPE_IDENTITY();

INSERT INTO CORE_PN_PackName (Name, Colour, Gender, PrintName, [Group], Active, PackNameListId, CustomerId, DepartmentId, CostCenterId,
                              Manager, EmployeeNumber, POEntry, Comments, Deleted)
VALUES ('TEST Alice', '1A73E8', NULL, 'ALICE', 0, 1, @ListId, NULL, NULL, NULL, 0, NULL, 'PO-100', NULL, 0),
       ('TEST Bob',   'E8731A', NULL, NULL,    0, 1, @ListId, NULL, NULL, NULL, 0, NULL, 'PO-100', NULL, 0),
       ('TEST Carol', '1AE873', NULL, NULL,    0, 1, @ListId, NULL, NULL, NULL, 0, NULL, 'PO-200', NULL, 0);
DECLARE @Alice int = (SELECT Id FROM CORE_PN_PackName WHERE Name = 'TEST Alice' AND PackNameListId = @ListId);
DECLARE @Bob int   = (SELECT Id FROM CORE_PN_PackName WHERE Name = 'TEST Bob' AND PackNameListId = @ListId);
DECLARE @Carol int = (SELECT Id FROM CORE_PN_PackName WHERE Name = 'TEST Carol' AND PackNameListId = @ListId);

-- The orders: store, customer, payment status (Pending 10, Authorized 20, Paid 30), shipping excl. tax, address, days ago
DECLARE @Orders TABLE (Number varchar(10), StoreId int, CustomerId int, PaymentStatusId int, ShippingExcl decimal(18,4), AddressTag varchar(20), DaysAgo int);
INSERT INTO @Orders VALUES
    ('TEST-07',  2, @CustomerId,        30, 6.8200, 'BRANCH-07',  0),
    ('TEST-12A', 2, @CustomerId,        30, 0,      'HOME-12A',   0),
    ('TEST-12B', 2, @CustomerId,        30, 0,      'HOME-12B',   0),
    ('TEST-13',  1, @NoBranchCustomerId, 10, 0,     'BRANCH-13',  0),
    ('TEST-14',  1, @CustomerId,        10, 0,      'BRANCH-14',  0),
    ('TEST-15',  2, @CustomerId,        20, 6.8200, 'BRANCH-15',  0),
    ('TEST-22C', 3, @CustomerId,        30, 0,      'BRANCH-22C', 0),
    ('TEST-22D', 3, @CustomerId,        30, 0,      'BRANCH-22D', 0),
    ('TEST-09',  2, @CustomerId,        30, 13.6400,'BRANCH-09',  0),
    ('TEST-10',  2, @CustomerId,        30, 6.8200, 'BRANCH-10',  0);

-- The lines: product, quantity, unit price excl. tax, attributes, attribute text, packname (packname orders only)
DECLARE @SourceAttributes nvarchar(max) = (SELECT TOP 1 AttributesXml FROM OrderItem WHERE OrderId = @SourceOrderId AND ProductId = 3);
DECLARE @Lines TABLE (Number varchar(10), ProductId int, Quantity int, UnitExcl decimal(18,4), PriceExcl decimal(18,4) NULL,
                      AttributesXml nvarchar(max) NULL, AttributeDescription nvarchar(max) NULL, PackNameId int NULL);
INSERT INTO @Lines VALUES
    ('TEST-07',  5,  2, 50.0000, NULL,    NULL, NULL, NULL),
    ('TEST-07',  12, 1, 10.0000, NULL,    NULL, NULL, NULL),
    ('TEST-12A', 13, 1, 10.0000, NULL,    NULL, NULL, NULL),
    ('TEST-12B', 15, 1, 10.0000, NULL,    NULL, NULL, NULL),
    ('TEST-13',  5,  1, 50.0000, NULL,    NULL, NULL, NULL),
    ('TEST-14',  3,  1, 20.0000, NULL,    NULL, NULL, NULL),
    ('TEST-15',  16, 1, 10.0000, NULL,    NULL, NULL, NULL),
    ('TEST-22C', 12, 3, 3.3333,  10.0000, NULL, NULL, NULL),
    ('TEST-22D', 12, 6, 1.6667,  10.0000, NULL, NULL, NULL),
    ('TEST-09',  3,  1, 20.0000, NULL,    @SourceAttributes, 'Size: S<br />Print name: ALICE', @Alice),
    ('TEST-09',  5,  1, 50.0000, NULL,    NULL, NULL, @Alice),
    ('TEST-09',  12, 2, 10.0000, NULL,    NULL, NULL, @Bob),
    ('TEST-10',  15, 1, 10.0000, NULL,    NULL, NULL, @Alice),
    ('TEST-10',  16, 1, 10.0000, NULL,    NULL, NULL, @Carol);
UPDATE @Lines SET PriceExcl = UnitExcl * Quantity WHERE PriceExcl IS NULL;

DECLARE @OrderColumns nvarchar(max) =
    (SELECT STRING_AGG(QUOTENAME(name), ', ') FROM sys.columns
     WHERE object_id = OBJECT_ID('[Order]') AND is_identity = 0 AND name <> 'OrderGuid');
DECLARE @ItemColumns nvarchar(max) =
    (SELECT STRING_AGG(QUOTENAME(name), ', ') FROM sys.columns
     WHERE object_id = OBJECT_ID('OrderItem') AND is_identity = 0 AND name NOT IN ('OrderItemGuid', 'OrderId'));
DECLARE @SourceItemId int = (SELECT MIN(Id) FROM OrderItem WHERE OrderId = @SourceOrderId);

DECLARE @Number varchar(10), @StoreId int, @OrderCustomerId int, @PaymentStatusId int, @ShippingExcl decimal(18,4), @AddressTag varchar(20), @DaysAgo int;
DECLARE ord CURSOR LOCAL FAST_FORWARD FOR SELECT Number, StoreId, CustomerId, PaymentStatusId, ShippingExcl, AddressTag, DaysAgo FROM @Orders;
OPEN ord;
FETCH NEXT FROM ord INTO @Number, @StoreId, @OrderCustomerId, @PaymentStatusId, @ShippingExcl, @AddressTag, @DaysAgo;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'INSERT INTO [Order] (OrderGuid, ' + @OrderColumns + N') SELECT NEWID(), ' + @OrderColumns
             + N' FROM [Order] WHERE Id = @SourceOrderId; SET @NewId = SCOPE_IDENTITY();';
    EXEC sp_executesql @Sql, N'@SourceOrderId int, @NewId int OUTPUT', @SourceOrderId, @NewId OUTPUT;

    -- One line per row of @Lines, each a copy of the source line with its own product, quantity and price
    DECLARE @ProductId int, @Quantity int, @UnitExcl decimal(18,4), @PriceExcl decimal(18,4), @Attributes nvarchar(max), @AttributeText nvarchar(max), @PackNameId int, @ItemId int;
    DECLARE line CURSOR LOCAL FAST_FORWARD FOR
        SELECT ProductId, Quantity, UnitExcl, PriceExcl, AttributesXml, AttributeDescription, PackNameId FROM @Lines WHERE Number = @Number;
    OPEN line;
    FETCH NEXT FROM line INTO @ProductId, @Quantity, @UnitExcl, @PriceExcl, @Attributes, @AttributeText, @PackNameId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'INSERT INTO OrderItem (OrderItemGuid, OrderId, ' + @ItemColumns + N') SELECT NEWID(), @NewId, '
                 + @ItemColumns + N' FROM OrderItem WHERE Id = @SourceItemId; SET @ItemId = SCOPE_IDENTITY();';
        EXEC sp_executesql @Sql, N'@SourceItemId int, @NewId int, @ItemId int OUTPUT', @SourceItemId, @NewId, @ItemId OUTPUT;

        UPDATE OrderItem
        SET ProductId = @ProductId, Quantity = @Quantity,
            UnitPriceExclTax = @UnitExcl, UnitPriceInclTax = ROUND(@UnitExcl * 1.1, 4),
            PriceExclTax = @PriceExcl, PriceInclTax = ROUND(@PriceExcl * 1.1, 4),
            AttributesXml = @Attributes, AttributeDescription = @AttributeText,
            DiscountAmountExclTax = 0, DiscountAmountInclTax = 0, OriginalProductCost = 0
        WHERE Id = @ItemId;

        IF @PackNameId IS NOT NULL
            INSERT INTO CORE_PN_PackNameOrderItem (OrderItemId, PackNameId, PackName, PackNameCustomerId, BranchId, BranchCode, Branch,
                                                   DepartmentId, Department, CostCenterId, CostCenter)
            SELECT @ItemId, p.Id, p.Name, NULL, b.Id, b.BranchCode, b.Name, 0, 'Workshop', 0, 'CC-01'
            FROM CORE_PN_PackName p CROSS JOIN CORE_Branch b WHERE p.Id = @PackNameId AND b.Id = @BranchId;

        FETCH NEXT FROM line INTO @ProductId, @Quantity, @UnitExcl, @PriceExcl, @Attributes, @AttributeText, @PackNameId;
    END
    CLOSE line;
    DEALLOCATE line;

    -- Totals: GST 10 % on goods and shipping
    DECLARE @SubExcl decimal(18,4) = (SELECT SUM(PriceExcl) FROM @Lines WHERE Number = @Number);
    DECLARE @AddressId int = (SELECT AddressId FROM @Addr WHERE Tag = @AddressTag);

    UPDATE [Order]
    SET CustomOrderNumber = @Number, StoreId = @StoreId, CustomerId = @OrderCustomerId,
        OrderStatusId = CASE WHEN @PaymentStatusId = 30 THEN 20 ELSE 10 END, PaymentStatusId = @PaymentStatusId,
        ShippingStatusId = 20,
        ShippingAddressId = @AddressId, PickupInStore = 0,
        ShippingMethod = CASE WHEN @ShippingExcl > 0 THEN 'T4 Courier' ELSE NULL END,
        ShippingRateComputationMethodSystemName = CASE WHEN @ShippingExcl > 0 THEN 'Shipping.FixedByWeightByTotal' ELSE NULL END,
        OrderSubtotalExclTax = @SubExcl, OrderSubtotalInclTax = ROUND(@SubExcl * 1.1, 4),
        OrderSubTotalDiscountExclTax = 0, OrderSubTotalDiscountInclTax = 0, OrderDiscount = 0,
        OrderShippingExclTax = @ShippingExcl, OrderShippingInclTax = ROUND(@ShippingExcl * 1.1, 4),
        OrderTax = ROUND((@SubExcl + @ShippingExcl) * 0.1, 4), OrderTotal = ROUND((@SubExcl + @ShippingExcl) * 1.1, 4),
        PaidDateUtc = CASE WHEN @PaymentStatusId = 30 THEN SYSUTCDATETIME() ELSE NULL END,
        CreatedOnUtc = DATEADD(day, -@DaysAgo, SYSUTCDATETIME())
    WHERE Id = @NewId;

    FETCH NEXT FROM ord INTO @Number, @StoreId, @OrderCustomerId, @PaymentStatusId, @ShippingExcl, @AddressTag, @DaysAgo;
END
CLOSE ord;
DEALLOCATE ord;

-- Packname destinations: one CORE_PN_OrderShipping row per packname (TEST-09: Alice to the branch, Bob home;
-- TEST-10: Alice and Carol to the same address, so one destination)
INSERT INTO CORE_PN_OrderShipping (OrderId, PackNameId, ShippingAddressId, PickupAddressId, PickupInStore, ShippingStatusId,
                                   ShippingMethod, ShippingRateComputationMethodSystemName, ShippingInclTax, ShippingExclTax)
SELECT o.Id, d.PackNameId, a.AddressId, NULL, 0, 20, 'T4 Courier', 'Shipping.FixedByWeightByTotal', d.ShippingExcl * 1.1, d.ShippingExcl
FROM (VALUES ('TEST-09', @Alice, 'BRANCH-09', 6.8200), ('TEST-09', @Bob, 'HOME-09', 6.8200),
             ('TEST-10', @Alice, 'BRANCH-10', 6.8200), ('TEST-10', @Carol, 'BRANCH-10', 0)) d(Number, PackNameId, AddressTag, ShippingExcl)
JOIN [Order] o ON o.CustomOrderNumber = d.Number
JOIN @Addr a ON a.Tag = d.AddressTag;

-- T19: TEST-09 paid partly by subsidy and store credit; the customer's share is what is left
DECLARE @Order09 int = (SELECT Id FROM [Order] WHERE CustomOrderNumber = 'TEST-09');
INSERT INTO CORE_PN_OrderSettlement (OrderId, SettleableAmount, SubsidyStockAmount, SubsidyShippingAmount, CreditNoteAmount, CreatedOnUtc)
SELECT Id, OrderTotal, 30.0000, 0, 5.0000, SYSUTCDATETIME() FROM [Order] WHERE Id = @Order09;
UPDATE [Order] SET OrderTotal = OrderTotal - 35.0000 WHERE Id = @Order09;

-- TEST-15 has nothing paid yet; TEST-13 and TEST-14 are pending orders
COMMIT TRANSACTION;

SELECT o.Id, o.CustomOrderNumber, o.StoreId, o.CustomerId, o.OrderStatusId, o.PaymentStatusId, o.OrderSubtotalExclTax,
       o.OrderShippingExclTax, o.OrderTotal,
       (SELECT COUNT(*) FROM OrderItem i WHERE i.OrderId = o.Id) AS Lines,
       (SELECT COUNT(*) FROM CORE_PN_OrderShipping s WHERE s.OrderId = o.Id) AS Destinations
FROM [Order] o
WHERE o.CustomOrderNumber LIKE 'TEST-%'
ORDER BY o.Id;
