/*
  Integration.Odoo — demo orders for the Orders page (Odoo › Orders)

  Adds 13 orders numbered DEMO-01 … DEMO-13, each a copy of one existing order (its addresses, customer and lines),
  with the order status, payment status and Odoo sync state set so that every state of the page shows:
  Not sent, Sent (one and two Odoo orders, with and without a warning), Failed, Held, In progress (pending and
  created), To cancel in Odoo (cancelled and deleted), Cancelled in Odoo and Not sent (cancelled).

  No call is made to Odoo. The Odoo ids are 900001 and up and the Odoo references are DEMO-S900nn: they match no
  real Odoo order. Pressing a button on a demo row:
    - Sync on a "Not sent", "Failed", "Held" or "In progress" row DOES call Odoo: it creates a real sale order on the
      Odoo test instance from the copied order's lines (or fails on the made-up Odoo id). Use the clean-up below
      before testing real sends.
    - Cancel in Odoo calls Odoo with a made-up id: Odoo answers that the order does not exist.

  Run on a local development database only. Run again to recreate the demo orders (it removes the old ones first).
  Clean-up only: run the block marked CLEAN-UP on its own.

  @SourceOrderId: the order to copy (must exist and have at least one line).
*/

SET NOCOUNT ON;

DECLARE @SourceOrderId int = 1;

IF NOT EXISTS (SELECT 1 FROM [Order] WHERE Id = @SourceOrderId)
    THROW 50000, 'The source order does not exist: set @SourceOrderId to an existing order.', 1;

BEGIN TRANSACTION;

-- ── CLEAN-UP: removes every demo order and what hangs off it ───────────────────────────────────
DELETE m FROM Odoo_OrderMapping m JOIN [Order] o ON o.Id = m.NopOrderId WHERE o.CustomOrderNumber LIKE 'DEMO-%';
DELETE i FROM OrderItem i JOIN [Order] o ON o.Id = i.OrderId WHERE o.CustomOrderNumber LIKE 'DEMO-%';
DELETE n FROM OrderNote n JOIN [Order] o ON o.Id = n.OrderId WHERE o.CustomOrderNumber LIKE 'DEMO-%';
DELETE FROM [Order] WHERE CustomOrderNumber LIKE 'DEMO-%';
-- ── end of CLEAN-UP ─────────────────────────────────────────────────────────────────────────────

-- The demo orders: nop status (Pending 10, Processing 20, Complete 30, Cancelled 40), payment status (Pending 10,
-- Authorized 20, Paid 30, Refunded 40, Voided 50), deleted, days ago, and what the page should show.
DECLARE @Demo TABLE (Number varchar(10), OrderStatusId int, PaymentStatusId int, Deleted bit, DaysAgo int, Shows varchar(60));
INSERT INTO @Demo VALUES
    ('DEMO-01', 10, 10, 0, 0,  'Not sent'),
    ('DEMO-02', 20, 30, 0, 1,  'Not sent'),
    ('DEMO-03', 30, 30, 0, 2,  'Sent'),
    ('DEMO-04', 30, 30, 0, 3,  'Sent, two Odoo orders'),
    ('DEMO-05', 20, 30, 0, 4,  'Sent, totals warning'),
    ('DEMO-06', 20, 30, 0, 5,  'Failed'),
    ('DEMO-07', 10, 10, 0, 6,  'Held'),
    ('DEMO-08', 20, 20, 0, 7,  'In progress (one of two sent)'),
    ('DEMO-09', 20, 30, 0, 8,  'In progress (created, not confirmed)'),
    ('DEMO-10', 40, 50, 0, 9,  'To cancel in Odoo (cancelled)'),
    ('DEMO-11', 30, 30, 1, 10, 'To cancel in Odoo (deleted)'),
    ('DEMO-12', 40, 40, 0, 11, 'Cancelled in Odoo'),
    ('DEMO-13', 40, 50, 0, 12, 'Not sent (cancelled)');

-- Copy the source order once per demo row, every column but the id and the GUID
DECLARE @OrderColumns nvarchar(max) =
    (SELECT STRING_AGG(QUOTENAME(name), ', ') FROM sys.columns
     WHERE object_id = OBJECT_ID('[Order]') AND is_identity = 0 AND name <> 'OrderGuid');
DECLARE @ItemColumns nvarchar(max) =
    (SELECT STRING_AGG(QUOTENAME(name), ', ') FROM sys.columns
     WHERE object_id = OBJECT_ID('OrderItem') AND is_identity = 0 AND name NOT IN ('OrderItemGuid', 'OrderId'));

DECLARE @Number varchar(10), @OrderStatusId int, @PaymentStatusId int, @Deleted bit, @DaysAgo int, @NewId int, @Sql nvarchar(max);
DECLARE demo CURSOR LOCAL FAST_FORWARD FOR SELECT Number, OrderStatusId, PaymentStatusId, Deleted, DaysAgo FROM @Demo;
OPEN demo;
FETCH NEXT FROM demo INTO @Number, @OrderStatusId, @PaymentStatusId, @Deleted, @DaysAgo;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'INSERT INTO [Order] (OrderGuid, ' + @OrderColumns + N') SELECT NEWID(), ' + @OrderColumns
             + N' FROM [Order] WHERE Id = @SourceOrderId; SET @NewId = SCOPE_IDENTITY();';
    EXEC sp_executesql @Sql, N'@SourceOrderId int, @NewId int OUTPUT', @SourceOrderId, @NewId OUTPUT;

    UPDATE [Order]
    SET CustomOrderNumber = @Number,
        OrderStatusId = @OrderStatusId,
        PaymentStatusId = @PaymentStatusId,
        Deleted = @Deleted,
        CreatedOnUtc = DATEADD(day, -@DaysAgo, SYSUTCDATETIME())
    WHERE Id = @NewId;

    SET @Sql = N'INSERT INTO OrderItem (OrderItemGuid, OrderId, ' + @ItemColumns + N') SELECT NEWID(), @NewId, '
             + @ItemColumns + N' FROM OrderItem WHERE OrderId = @SourceOrderId;';
    EXEC sp_executesql @Sql, N'@SourceOrderId int, @NewId int', @SourceOrderId, @NewId;

    FETCH NEXT FROM demo INTO @Number, @OrderStatusId, @PaymentStatusId, @Deleted, @DaysAgo;
END
CLOSE demo;
DEALLOCATE demo;

-- The Odoo side, as the send would have recorded it. Status: Created 1, Confirmed 2, Held 3, Failed 4,
-- Cancelled 5, Pending 6. Destination key 0 is a plain order; several keys are several delivery addresses.
DECLARE @Mapping TABLE (Number varchar(10), DestinationKey int, StatusId int, OdooOrderId int NULL, OdooOrderName varchar(64) NULL, Message nvarchar(400) NULL);
INSERT INTO @Mapping VALUES
    ('DEMO-03', 0,  2, 900003, 'DEMO-S90003', NULL),
    ('DEMO-04', 11, 2, 900041, 'DEMO-S90041', NULL),
    ('DEMO-04', 12, 2, 900042, 'DEMO-S90042', NULL),
    ('DEMO-05', 0,  2, 900005, 'DEMO-S90005', 'Odoo''s total before tax 59.98 differs from nop''s 60.00.'),
    ('DEMO-06', 0,  4, 900006, NULL,          'Not enough stock for POLO (S).'),
    ('DEMO-07', 0,  3, NULL,   NULL,          'The customer is in no branch, so there is no Odoo company to send the order to.'),
    ('DEMO-08', 21, 2, 900081, 'DEMO-S90081', NULL),
    ('DEMO-08', 22, 6, NULL,   NULL,          NULL),
    ('DEMO-09', 0,  1, 900009, NULL,          NULL),
    ('DEMO-10', 0,  2, 900010, 'DEMO-S90010', NULL),
    ('DEMO-11', 0,  2, 900011, 'DEMO-S90011', NULL),
    ('DEMO-12', 0,  5, 900012, 'DEMO-S90012', NULL);

INSERT INTO Odoo_OrderMapping (NopOrderId, DestinationKey, OdooOrderId, OdooOrderName, StatusId, Message, CreatedOnUtc, UpdatedOnUtc)
SELECT o.Id, m.DestinationKey, m.OdooOrderId, m.OdooOrderName, m.StatusId, m.Message, o.CreatedOnUtc, SYSUTCDATETIME()
FROM @Mapping m
JOIN [Order] o ON o.CustomOrderNumber = m.Number;

COMMIT TRANSACTION;

-- What was created, and what the Orders page should show for each
SELECT o.Id, o.CustomOrderNumber, o.OrderStatusId, o.PaymentStatusId, o.Deleted, d.Shows,
       (SELECT COUNT(*) FROM Odoo_OrderMapping m WHERE m.NopOrderId = o.Id) AS OdooRows
FROM [Order] o
JOIN @Demo d ON d.Number = o.CustomOrderNumber
ORDER BY o.CustomOrderNumber;
