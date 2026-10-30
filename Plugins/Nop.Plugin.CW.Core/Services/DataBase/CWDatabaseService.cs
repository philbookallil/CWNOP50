using LinqToDB.Data;
using Nop.Core.Domain.Configuration;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Data.Mapping;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes.Views;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Services.DataBase;

public class CWDatabaseService : ICWDatabaseService
{
    #region Fields

    protected readonly INopDataProvider _dataProvider;

    #endregion

    #region Constants

    private static readonly DataParameter[] NoParameters = Array.Empty<DataParameter>();

    private static readonly string ViewSubsidyPackNameCredit = NameCompatibilityManager.GetTableName(typeof(SubsidyPackNameCredit));
    private static readonly string ViewProductGroupPackNameCredit = NameCompatibilityManager.GetTableName(typeof(ProductGroupPackNameCredit));
    private static readonly string ViewSubsidyInOrderingPeriod = NameCompatibilityManager.GetTableName(typeof(SubsidyInOrderingPeriod));
    private static readonly string ViewSubsidyVariationInOrderingPeriod = NameCompatibilityManager.GetTableName(typeof(SubsidyVariationInOrderingPeriod));
    private static readonly string ViewSubsidyPackNameCanOrder = NameCompatibilityManager.GetTableName(typeof(SubsidyPackNameCanOrder));
    private static readonly string ViewPackNameShoppingCartItem = NameCompatibilityManager.GetTableName(typeof(PackNameShoppingCartItem));
    private static readonly string ViewStoreSubsidyVariationSetting = NameCompatibilityManager.GetTableName(typeof(StoreSubsidyVariationSetting));
    private static readonly string ViewPackNameOrderItem = NameCompatibilityManager.GetTableName(typeof(PackNameFullOrderItem));

    private static readonly string ShoppingCartItemTableName = NameCompatibilityManager.GetTableName(typeof(ShoppingCartItem));
    private static readonly string PackNameCartItemTableName = NameCompatibilityManager.GetTableName(typeof(PackNameCartItem));
    private static readonly string PackNameTableName = NameCompatibilityManager.GetTableName(typeof(PackName));
    private static readonly string PackNameListTableName = NameCompatibilityManager.GetTableName(typeof(PackNameList));
    private static readonly string BranchTableName = NameCompatibilityManager.GetTableName(typeof(Branch));
    private static readonly string DepartmentTableName = NameCompatibilityManager.GetTableName(typeof(Department));
    private static readonly string StoreTableName = NameCompatibilityManager.GetTableName(typeof(Store));
    private static readonly string CostCenterTableName = NameCompatibilityManager.GetTableName(typeof(CostCenter));
    private static readonly string PackNameListManagerMappingTableName = NameCompatibilityManager.GetTableName(typeof(PackNameListManagerMapping));

    private static readonly string SubsidyTableName = NameCompatibilityManager.GetTableName(typeof(Subsidy));
    private static readonly string SubsidyPackNameTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyPackName));
    private static readonly string SubsidyVariationTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyVariation));
    private static readonly string SubsidyVariationHistoryTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyVariationHistory));
    private static readonly string SubsidyTopupTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyTopup));
    private static readonly string SubsidyTopupHistoryTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyTopupHistory));
    private static readonly string SubsidyProductGroupTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyProductGroup));
    private static readonly string ProductGroupTableName = NameCompatibilityManager.GetTableName(typeof(ProductGroup));
    private static readonly string ProductGroupTopupTableName = NameCompatibilityManager.GetTableName(typeof(ProductGroupTopup));
    private static readonly string ProductGroupTopupHistoryTableName = NameCompatibilityManager.GetTableName(typeof(ProductGroupTopupHistory));
    private static readonly string SubsidyOrderItemTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyOrderItem));
    private static readonly string SubsidyOrderShippingTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyOrderShipping));
    private static readonly string SubsidyRestoreItemTableName = NameCompatibilityManager.GetTableName(typeof(SubsidyRestoreItem));

    private static readonly string ViewShipmentAddress = NameCompatibilityManager.GetTableName(typeof(ShipmentAddress));
    private static readonly string ShipmentTableName = NameCompatibilityManager.GetTableName(typeof(Shipment));
    private static readonly string ShipmentItemTableName = NameCompatibilityManager.GetTableName(typeof(ShipmentItem));
    private static readonly string OrderShippingTableName = NameCompatibilityManager.GetTableName(typeof(OrderShipping));

    private static readonly string CreditNoteTableName = NameCompatibilityManager.GetTableName(typeof(CreditNote));
    private static readonly string CreditNoteHistoryTableName = NameCompatibilityManager.GetTableName(typeof(CreditNoteHistory));
    private static readonly string ViewCreditNoteRemain = NameCompatibilityManager.GetTableName(typeof(CreditNoteBalance));
    private static readonly string ViewCreditNoteHistoryRemain = NameCompatibilityManager.GetTableName(typeof(CreditNoteHistoryBalance));

    private static readonly string CustomerTableName = NameCompatibilityManager.GetTableName(typeof(Customer));
    private static readonly string OrderTableName = $"[{NameCompatibilityManager.GetTableName(typeof(Order))}]";
    private static readonly string OrderItemTableName = NameCompatibilityManager.GetTableName(typeof(OrderItem));
    private static readonly string PackNameOrderItemTableName = NameCompatibilityManager.GetTableName(typeof(PackNameOrderItem));
    private static readonly string SettingTableName = NameCompatibilityManager.GetTableName(typeof(Setting));

    #endregion

    #region Scripts

    private static readonly string SubsidyNetConsumptionSource =
        $@"(
						SELECT OrderId, OrderItemId, SubsidyId, ProductGroupId,
						       Credit, CreditTopup, Quantity, QuantityTopup,
						       ProductGroupCredit, ProductGroupCreditTopup
						FROM {SubsidyOrderItemTableName}
						UNION ALL
						SELECT OrderId, OrderItemId, SubsidyId, ProductGroupId,
						       -Credit, -CreditTopup, -Quantity, -QuantityTopup,
						       -ProductGroupCredit, -ProductGroupCreditTopup
						FROM {SubsidyRestoreItemTableName}
					)";

    private readonly string CreateSPGetPackNameListIdsByManagerSql =
        $@"CREATE OR ALTER PROCEDURE  [dbo].[{CWCoreDefaults.GetPackNameListIdsByManagerSPName}]
    			@ManagerId INT,
    			@RequireActiveList BIT = 1
            AS
            BEGIN
                SET NOCOUNT ON;

                -- @RequireActiveList = 1 (default): VISIBILITY - which lists may this manager order from.
                -- @RequireActiveList = 0: OWNERSHIP - which lists' packnames are this manager's to
                -- administer. Deactivating a list takes it out of the selector; it does not revoke the
                -- manager's right to edit or top up the packnames in it. Before
                -- that flag existed, every path filtered Active = 1 and a deactivated list locked its
                -- manager out of the very records they would fix.
                -- The four paths are deliberately broader than 4.6, deliberately.

                WITH BranchHierarchy AS (
                    SELECT pnl_root.BranchId AS Id,
                           pnl_root.BranchId AS RootBranchId,
                           pnl_root.StoreId  AS StoreId
                    FROM {PackNameTableName} pn_root
                    INNER JOIN {PackNameListTableName} pnl_root ON pn_root.PackNameListId = pnl_root.Id
                    WHERE  pn_root.Id = @ManagerId

                    UNION ALL

                    SELECT b.Id,
                           bh.RootBranchId,
                           bh.StoreId
                    FROM   {BranchTableName} b
                    INNER JOIN BranchHierarchy bh ON b.ParentBranchId = bh.Id
                    -- b.StoreId = bh.StoreId is NOT optional. The recursion carries the ROOT's store
                    -- forward, so without this a branch whose ParentBranchId points into another
                    -- store joins the hierarchy wearing the root's StoreId, and path 3 grants a
                    -- store-A manager that store-B branch's lists. SaveBranch's insert maps
                    -- ParentBranchId straight from the post, so the edge is reachable
                    -- (LimitAssignedBranches decides reach INSIDE a store, never across).
                    WHERE  b.Deleted = 0
                      AND  b.StoreId = bh.StoreId
                )
                -- Path 1: Own branch — all lists regardless of flags
                SELECT DISTINCT pnl.Id
                FROM  {PackNameListTableName} pnl
                INNER JOIN BranchHierarchy bh ON pnl.BranchId = bh.Id
                WHERE  pnl.Deleted  = 0 AND (@RequireActiveList = 0 OR pnl.Active = 1)
                  AND  pnl.BranchId = bh.RootBranchId

                UNION

                -- Path 2: AllManagers — any list store-wide with this flag.
                -- b.Deleted = 0 is NOT optional: paths 1 and 3 inherit it from the CTE's recursive
                -- member and path 4 never joins Branch, so without it this is the only path that can
                -- surface a list whose branch has been deleted.
                SELECT DISTINCT pnl.Id
                FROM {PackNameListTableName} pnl
                INNER JOIN {BranchTableName} b ON pnl.BranchId = b.Id
                WHERE  pnl.Deleted     = 0 AND (@RequireActiveList = 0 OR pnl.Active = 1)
                  AND  b.Deleted       = 0
                  AND  pnl.AllManagers = 1
                  AND  b.StoreId       = (SELECT TOP 1 StoreId FROM BranchHierarchy)

                UNION

                -- Path 3: ParentManagers — descendant branches only
                SELECT DISTINCT pnl.Id
                FROM {PackNameListTableName} pnl
                INNER JOIN BranchHierarchy bh ON pnl.BranchId = bh.Id
                WHERE  pnl.Deleted          = 0 AND (@RequireActiveList = 0 OR pnl.Active = 1)
                  AND  pnl.ParentManagers   = 1
                  AND  pnl.BranchId        != bh.RootBranchId

                UNION

                -- Path 4: Direct assignment.
                -- NOTE: this path has no store predicate of its own - it cannot, the mapping is the
                -- grant. The store boundary for it is enforced where the mapping is CREATED
                -- where the mapping is created; a row that should not exist still grants scope here.
                SELECT DISTINCT pnl.Id
                FROM {PackNameListTableName} pnl
                INNER JOIN {PackNameListManagerMappingTableName} m ON pnl.Id = m.PackNameListId
                WHERE  pnl.Deleted       = 0 AND (@RequireActiveList = 0 OR pnl.Active = 1)
                  AND  m.PackNameId      = @ManagerId

            END";

    //SP for supporting subsidy history
    private static readonly string CreateSPGetSubsidyHistorySql =
        $@"CREATE OR ALTER PROCEDURE  [dbo].[{CWCoreDefaults.GetSubsidyHistorySPName}]
			@PackNameId INT,
			@SubsidyId  INT,
			@PageIndex INT,
			@PageSize INT
		
		AS
			BEGIN
				SET NOCOUNT ON;
 
				--ONE SCALAR FOR THE WHOLE RESULT SET, NOT A JOIN. AssignmentRemoved depends only on the
				--two parameters, so joining SubsidyPackName to carry it multiplied every history row by
				--the number of assignment rows: a packname removed from a subsidy and re-added has two,
				--one deleted and one live, and every row came back twice - once flagged Removed and once
				--not (the filtered unique index allows that pair deliberately; see the spg comment in the
				--allocation branch below, which is the same shape one level down).
				--EXISTS, not the newest row: ""removed"" means there is NO live assignment. Re-adding
				--restores it, which is the answer the grid should give.
				DECLARE @AssignmentRemoved BIT =
					CASE WHEN EXISTS (SELECT 1 FROM {SubsidyPackNameTableName}
									  WHERE PackNameId = @PackNameId AND SubsidyId = @SubsidyId AND Deleted = 0)
						 THEN 0 ELSE 1 END;

				--CreatedOn IS TRUE UTC ON EVERY ARM, AS A datetime2. The ledger columns were datetimeoffset
				--until the CWTimestampsDateTime2 migration and the Order column is datetime2; in a UNION the
				--datetimeoffset type wins and widens every arm, and the reader then materialised the offset
				--value as the WEB SERVER'S wall clock - two hours late on a UTC+2 host. AT TIME ZONE 'UTC'
				--then the cast gives UTC as a datetime2 whichever of the two types the column is, so the
				--procedure is right both before and after that migration has run.
				SELECT
					ROW_NUMBER() OVER(ORDER BY h.CreatedOn DESC) AS Id,
					COUNT(*) OVER() AS TotalCount,
					h.PackNameId,
					h.SubsidyId,
					h.OrderId,
					h.CreatedOn,
					h.Subsidy,
					h.Credit,
					h.CreditTopup,
					h.Quantity,
					h.ProductGroupCredit,
					h.QuantityTopup,
					h.ProductGroupCreditTopup,
					h.ProductGroup,
					h.Reason,
					h.CreatorId,
					h.CreatorName,
					h.HistoryTypeId,
					h.AssignmentRemoved
				FROM
				(
					SELECT
						sp.PackNameId as PackNameId,
						sp.SubsidyId as SubsidyId,
						0 as OrderId,
						CAST(sp.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
						s.[Name] as Subsidy,
						s.[Credit] as Credit,
						0 as CreditTopup,
						0 as Quantity,
						0 as ProductGroupCredit,
						0 as QuantityTopup,
						0 as ProductGroupCreditTopup,
						'' as ProductGroup,
						'' as Reason,
						sp.CreatorId,
						CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
						1 as HistoryTypeId,
						@AssignmentRemoved as AssignmentRemoved
					FROM {SubsidyPackNameTableName} sp
					INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id
					LEFT JOIN {CustomerTableName} c ON sp.CreatorId = c.Id
					WHERE sp.PackNameId = @PackNameId AND sp.SubsidyId = @SubsidyId
 
					UNION ALL
			
					--A VARIATION ROW IS HISTORY WHATEVER IT VARIES. This arm used to require the store's
					--PackNamesOwnGlobalValue, so a store where variations carry only dates saw no variation
					--history at all. The creator is the VARIATION HISTORY row's - the admin who varied it -
					--on both the id and the name; the assignment's creator is the row above.
					SELECT
						sp.PackNameId as PackNameId,
						sp.SubsidyId as SubsidyId,
						0 as OrderId,
						CAST(svarh.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
						s.[Name] as Subsidy,
						COALESCE(svarh.[Credit], s.[Credit]) as Credit,
						0 as CreditTopup,
						0 as Quantity,
						0 as ProductGroupCredit,
						0 as QuantityTopup,
						0 as ProductGroupCreditTopup,
						'' as ProductGroup,
						svarh.Reason as Reason,
						svarh.CreatorId,
						CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
						7 as HistoryTypeId,
						@AssignmentRemoved as AssignmentRemoved
					FROM {SubsidyPackNameTableName} sp
					INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id
					INNER JOIN {SubsidyVariationTableName} svar ON sp.Id = svar.SubsidyPackNameId AND svar.Deleted = 0
					INNER JOIN {SubsidyVariationHistoryTableName} svarh ON svar.Id = svarh.SubsidyVariationId
					LEFT JOIN {CustomerTableName} c ON svarh.CreatorId = c.Id
					WHERE sp.PackNameId = @PackNameId AND sp.SubsidyId = @SubsidyId
			
					UNION ALL
			
					SELECT
						sp.PackNameId as PackNameId,
						sp.SubsidyId as SubsidyId,
						0 as OrderId,
						CAST(stoph.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
						s.[Name] as Subsidy,
						0 as Credit,
						stoph.Credit as CreditTopup,
						0 as Quantity,
						0 as ProductGroupCredit,
						0 as QuantityTopup,
						0 as ProductGroupCreditTopup,
						'' as ProductGroup,
						stoph.Reason as Reason,
						stoph.CreatorId,
						CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
						2 as HistoryTypeId,
						@AssignmentRemoved as AssignmentRemoved
					FROM {SubsidyPackNameTableName} sp
					INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id
					INNER JOIN {SubsidyTopupTableName} stop ON sp.Id = stop.SubsidyPackNameId
					INNER JOIN {SubsidyTopupHistoryTableName} stoph ON stop.Id = stoph.SubsidyTopupId
					LEFT JOIN {CustomerTableName} c ON stoph.CreatorId = c.Id
					WHERE sp.PackNameId = @PackNameId AND sp.SubsidyId = @SubsidyId
 
					UNION ALL
			
					SELECT
						@PackNameId as PackNameId,
						@SubsidyId as SubsidyId,
						0 as OrderId,
						CAST(spg.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
						s.[Name] as Subsidy,
						0 as Credit,
						0 as CreditTopup,
						spg.[Quantity] as Quantity,
						spg.[Credit] as ProductGroupCredit,
						0 as QuantityTopup,
						0 as ProductGroupCreditTopup,
						ISNULL(pg.[Name],'') as ProductGroup,
						'' as Reason,
						spg.CreatorId,
						CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
						3 as HistoryTypeId,
						@AssignmentRemoved as AssignmentRemoved
					--sp IS NOT IN THE FROM. It only scoped this branch to the packname, and an allocation
					--belongs to the SUBSIDY, not to the assignment - so with two assignment rows
					--(remove-then-re-add) every allocation was listed once per row. EXISTS in the WHERE
					--scopes without multiplying, and is deliberately NOT filtered on Deleted: a packname
					--whose assignment was removed still shows its history, which is the whole point of
					--marking rows instead of hiding them.
					--spg.Deleted = 0 is in the WHERE for the same reason the credit views have it: a
					--soft-deleted allocation alongside a live one for the same group - what
					--remove-then-re-add leaves behind, and what the filtered unique index deliberately
					--allows - emitted this line twice.
					FROM {SubsidyProductGroupTableName} spg
					INNER JOIN {SubsidyTableName} s ON s.Id = @SubsidyId
					INNER JOIN {ProductGroupTableName} pg ON spg.ProductGroupId = pg.Id AND pg.Deleted = 0
					LEFT JOIN {CustomerTableName} c ON spg.CreatorId = c.Id
					WHERE spg.SubsidyId = @SubsidyId AND spg.Deleted = 0
					  AND EXISTS (SELECT 1 FROM {SubsidyPackNameTableName} sp
								  WHERE sp.PackNameId = @PackNameId AND sp.SubsidyId = @SubsidyId)
			
					UNION ALL
			
					SELECT
						sp.PackNameId as PackNameId,
						sp.SubsidyId as SubsidyId,
						0 as OrderId,
						CAST(pgtoph.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
						s.[Name] as Subsidy,
						0 as Credit,
						0 as CreditTopup,
						0 as Quantity,
						0 as ProductGroupCredit,
						pgtoph.[Quantity] as QuantityTopup,
						pgtoph.[Credit] as ProductGroupCreditTopup,
						ISNULL(pg.[Name],'') as ProductGroup,
						pgtoph.[Reason] as Reason,
						pgtoph.CreatorId,
						CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
						4 as HistoryTypeId,
						@AssignmentRemoved as AssignmentRemoved
					FROM {SubsidyPackNameTableName} sp
					INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id
					--See the allocation branch above: without spg.Deleted = 0 a remove-then-re-add of one
					--allocation made EVERY top-up history row for that group appear twice.
					INNER JOIN {SubsidyProductGroupTableName} spg ON sp.SubsidyId = spg.SubsidyId AND spg.Deleted = 0
					INNER JOIN {ProductGroupTableName} pg ON spg.ProductGroupId = pg.Id AND pg.Deleted = 0
					INNER JOIN {ProductGroupTopupTableName} pgtop ON sp.Id = pgtop.SubsidyPackNameId and spg.ProductGroupId = pgtop.ProductGroupId
					INNER JOIN {ProductGroupTopupHistoryTableName} pgtoph ON pgtop.Id = pgtoph.ProductGroupTopupId
					LEFT JOIN {CustomerTableName} c ON pgtoph.CreatorId = c.Id
					WHERE sp.PackNameId = @PackNameId AND sp.SubsidyId = @SubsidyId
			
					UNION ALL
			
					SELECT
						pnoi.PackNameId as PackNameId,
						soi.SubsidyId as SubsidyId,
						o.[Id] as OrderId,
						CAST(o.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
						s.[Name] as Subsidy,
						soi.[Credit] as Credit,
						soi.[CreditTopup] as CreditTopup,
						soi.[Quantity] as Quantity,
						soi.[ProductGroupCredit] as ProductGroupCredit,
						soi.[QuantityTopup] as QuantityTopup,
						soi.[ProductGroupCreditTopup] as ProductGroupCreditTopup,
						ISNULL(pg.[Name],'') as ProductGroup,
						'' as Reason,
						o.CustomerId as CreatorId,
						CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
						CASE WHEN soi.ProductGroupId IS NULL THEN 5 ELSE 6 END as HistoryTypeId,
                        @AssignmentRemoved as AssignmentRemoved
					FROM {SubsidyOrderItemTableName} soi
					INNER JOIN {SubsidyTableName} s ON soi.SubsidyId = s.Id
					INNER JOIN {OrderTableName} o ON soi.OrderId = o.Id
					INNER JOIN {PackNameOrderItemTableName} pnoi ON soi.OrderItemId = pnoi.OrderItemId
					LEFT JOIN {ProductGroupTableName} pg ON soi.ProductGroupId = pg.Id
					LEFT JOIN {CustomerTableName} c ON o.CustomerId = c.Id 
					WHERE o.OrderStatusId != 40 AND o.Deleted = 0 AND pnoi.PackNameId = @PackNameId AND soi.SubsidyId = @SubsidyId
 
                    UNION ALL
 
                    SELECT  
                        sos.PackNameId as PackNameId,
                        sos.SubsidyId as SubsidyId,
                        o.[Id] as OrderId,
                        CAST(o.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
                        s.[Name] as Subsidy,
                        sos.[Credit] as Credit,
                        sos.[CreditTopup] as CreditTopup,
                        0 as Quantity,
                        0 as ProductGroupCredit,
                        0 as QuantityTopup,
                        0 as ProductGroupCreditTopup,
                        '' as ProductGroup,
                        '' as Reason,
                        o.CustomerId as CreatorId,
                        CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
                        10 as HistoryTypeId,
                        @AssignmentRemoved as AssignmentRemoved
                    FROM {SubsidyOrderShippingTableName} sos 
                    INNER JOIN {SubsidyTableName} s ON sos.SubsidyId = s.Id
                    INNER JOIN {OrderTableName} o ON sos.OrderId = o.Id
                    LEFT JOIN {CustomerTableName} c ON o.CustomerId = c.Id
					WHERE o.OrderStatusId != 40 AND o.Deleted = 0 AND sos.PackNameId = @PackNameId AND sos.SubsidyId = @SubsidyId
 
                    UNION ALL
 
                    SELECT
                        pnoi.PackNameId as PackNameId,
                        sri.SubsidyId as SubsidyId,
                        sri.OrderId as OrderId,
                        CAST(sri.[CreatedOnUtc] AT TIME ZONE 'UTC' AS datetime2) as CreatedOn,
                        s.[Name] as Subsidy,
                        sri.[Credit] as Credit,
                        sri.[CreditTopup] as CreditTopup,
                        sri.[Quantity] as Quantity,
                        sri.[ProductGroupCredit] as ProductGroupCredit,
                        sri.[QuantityTopup] as QuantityTopup,
                        sri.[ProductGroupCreditTopup] as ProductGroupCreditTopup,
                        ISNULL(pg.[Name],'') as ProductGroup,
                        '' as Reason,
                        ISNULL(sri.CreatedByCustomerId, 0) as CreatorId,
                        CASE WHEN c.Id IS NULL THEN '' ELSE CONCAT(c.FirstName, ' ', c.LastName) END AS CreatorName,
                        12 as HistoryTypeId,
                        @AssignmentRemoved as AssignmentRemoved
                    FROM {SubsidyRestoreItemTableName} sri
                    INNER JOIN {SubsidyTableName} s ON sri.SubsidyId = s.Id
                    INNER JOIN {OrderTableName} o ON sri.OrderId = o.Id
                    INNER JOIN {PackNameOrderItemTableName} pnoi ON sri.OrderItemId = pnoi.OrderItemId
                    LEFT JOIN {ProductGroupTableName} pg ON sri.ProductGroupId = pg.Id
                    LEFT JOIN {CustomerTableName} c ON sri.CreatedByCustomerId = c.Id
                    WHERE o.OrderStatusId != 40 AND o.Deleted = 0 AND pnoi.PackNameId = @PackNameId AND sri.SubsidyId = @SubsidyId
				) as h
				ORDER BY h.CreatedOn DESC OFFSET @PageIndex * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
				
			END";

    //The occurrence of a subsidy's ordering period that is current as of a given date.
    //An inline TVF, so it expands into the caller's plan instead of blocking parallelism the way a scalar
    //UDF does on a server older than SQL Server 2019. The views CROSS APPLY it and test the dates
    //themselves: a period is open when the first occurrence has begun and the date falls inside the current
    //one, and an order counts when its store-local date falls inside that same occurrence. Both dates are
    //NOT NULL on the subsidy, and a variation's dates are COALESCEd with them before they get here.
    //Feb-29 anniversaries are clamped with EOMONTH; DATEFROMPARTS would RAISE
    //"Cannot construct data type date" and take the whole credit view down in a non-leap year.
    private static readonly string CreateFunctionSubsidyCurrentPeriodSql =
        $@"CREATE OR ALTER FUNCTION [dbo].[subsidy_current_period]
			(
				@AnnualRecurrence bit,
				@DurationStart    date,
				@DurationEnd      date,
				@AsOf             date
			)
			RETURNS TABLE
			AS
			RETURN
			(
				WITH firsts AS
				(
					SELECT
						Wraps          = CASE WHEN YEAR(@DurationStart) < YEAR(@DurationEnd) THEN 1 ELSE 0 END,
						StartPrevFirst = DATEFROMPARTS(YEAR(@AsOf) - 1, MONTH(@DurationStart), 1),
						StartThisFirst = DATEFROMPARTS(YEAR(@AsOf),     MONTH(@DurationStart), 1),
						EndThisFirst   = DATEFROMPARTS(YEAR(@AsOf),     MONTH(@DurationEnd),   1),
						EndNextFirst   = DATEFROMPARTS(YEAR(@AsOf) + 1, MONTH(@DurationEnd),   1)
				),
				anniversaries AS
				(
					SELECT
						f.Wraps,
						StartPrevYear = CASE WHEN DAY(@DurationStart) > DAY(EOMONTH(f.StartPrevFirst))
											 THEN EOMONTH(f.StartPrevFirst)
											 ELSE DATEADD(day, DAY(@DurationStart) - 1, f.StartPrevFirst) END,
						StartThisYear = CASE WHEN DAY(@DurationStart) > DAY(EOMONTH(f.StartThisFirst))
											 THEN EOMONTH(f.StartThisFirst)
											 ELSE DATEADD(day, DAY(@DurationStart) - 1, f.StartThisFirst) END,
						EndThisYear   = CASE WHEN DAY(@DurationEnd) > DAY(EOMONTH(f.EndThisFirst))
											 THEN EOMONTH(f.EndThisFirst)
											 ELSE DATEADD(day, DAY(@DurationEnd) - 1, f.EndThisFirst) END,
						EndNextYear   = CASE WHEN DAY(@DurationEnd) > DAY(EOMONTH(f.EndNextFirst))
											 THEN EOMONTH(f.EndNextFirst)
											 ELSE DATEADD(day, DAY(@DurationEnd) - 1, f.EndNextFirst) END
					FROM firsts f
				)
				SELECT
					PeriodStart =
						CASE
							WHEN @AnnualRecurrence = 0    THEN @DurationStart
							WHEN a.Wraps = 0              THEN a.StartThisYear
							WHEN @AsOf >= a.StartThisYear THEN a.StartThisYear
							ELSE a.StartPrevYear
						END,
					PeriodEnd =
						CASE
							WHEN @AnnualRecurrence = 0    THEN @DurationEnd
							WHEN a.Wraps = 0              THEN a.EndThisYear
							WHEN @AsOf >= a.StartThisYear THEN a.EndNextYear
							ELSE a.EndThisYear
						END
				FROM anniversaries a
			);";

    //The two scalar wrappers the views used before they applied subsidy_current_period themselves, and the
    //scalar form of the topup period end. Dropped so nothing can call them again.
    private static readonly string DropScalarPeriodFunctionsSql =
        $@"DROP FUNCTION IF EXISTS [dbo].[subsidy_order_is_in_current_period];
			DROP FUNCTION IF EXISTS [dbo].[subsidy_is_in_ordering_period];
			DROP FUNCTION IF EXISTS [dbo].[subsidy_get_topup_period_end];";

    //The close of the ordering period a topup created at @CreatedOnLocal belongs to: 23:59:59 store time
    //on the closing day.
    //NOT the same question as subsidy_current_period: this one rolls FORWARD. A topup added in November
    //against a July-September annual subsidy belongs to NEXT year's close, because it is a top-up for the
    //coming season. subsidy_current_period would answer "this year's July-September". Both are needed.
    //A non-annual subsidy has one period for ever, so every topup maps to its one close and still counts
    //whether it was created before or after the season. An inline TVF for the same reason as
    //subsidy_current_period; EOMONTH clamps 29 February to the 28th in a non-leap year.
    private static readonly string CreateFunctionSubsidyTopupPeriodEndSql =
        $@"CREATE OR ALTER FUNCTION [dbo].[subsidy_topup_period_end]
			(
				@CreatedOnLocal   datetime2,
				@DurationEnd      date,
				@AnnualRecurrence bit
			)
			RETURNS TABLE
			AS
			RETURN
			(
				WITH firsts AS
				(
					SELECT
						ThisFirst = DATEFROMPARTS(YEAR(@CreatedOnLocal),     MONTH(@DurationEnd), 1),
						NextFirst = DATEFROMPARTS(YEAR(@CreatedOnLocal) + 1, MONTH(@DurationEnd), 1)
				),
				closes AS
				(
					SELECT
						ThisClose = DATEADD(second, -1, DATEADD(day, 1, CAST(
										CASE WHEN DAY(@DurationEnd) > DAY(EOMONTH(f.ThisFirst))
											 THEN EOMONTH(f.ThisFirst)
											 ELSE DATEADD(day, DAY(@DurationEnd) - 1, f.ThisFirst) END AS datetime2))),
						NextClose = DATEADD(second, -1, DATEADD(day, 1, CAST(
										CASE WHEN DAY(@DurationEnd) > DAY(EOMONTH(f.NextFirst))
											 THEN EOMONTH(f.NextFirst)
											 ELSE DATEADD(day, DAY(@DurationEnd) - 1, f.NextFirst) END AS datetime2)))
					FROM firsts f
				)
				SELECT
					PeriodEnd =
						CASE
							WHEN @AnnualRecurrence = 0          THEN DATEADD(second, -1, DATEADD(day, 1, CAST(@DurationEnd AS datetime2)))
							WHEN c.ThisClose >= @CreatedOnLocal THEN c.ThisClose
							ELSE c.NextClose
						END
				FROM closes c
			);";

    //View for supporting subsidies credits
    //eff is the assignment's effective dates (the variation's where the store lets packnames own their dates),
    //per the occurrence current today and topupEnd the close of that occurrence, which is what a topup is
    //bucketed on. Each assignment row reads its own topups and its own consumption.
    private static readonly string CreateViewSubsidyPackNameCreditsSql =
        $@"CREATE OR ALTER VIEW {ViewSubsidyPackNameCredit}
			AS
				SELECT
					sp.[Id] as Id,
					sp.[PackNameId],
					sp.[SubsidyId],
					s.[Name] as SubsidyName,
					CASE WHEN vset.PackNamesOwnGlobalValue = 1 THEN COALESCE(svar.Credit, s.Credit) ELSE s.Credit END as Credit,
					COALESCE(stop.Credit, 0) as CreditTopup,
					COALESCE(used.UsedCredit, 0) + COALESCE(usedShipping.UsedCredit, 0) as UsedCredit,
					COALESCE(used.UsedCreditTopup, 0) + COALESCE(usedShipping.UsedCreditTopup, 0) as UsedCreditTopup,
					eff.DurationStart,
					eff.DurationEnd,
					s.AnnualRecurrence,
					s.OrderOutsidePeriod,
					CASE WHEN s.OnlyAllocatedProducts = 1 AND (s.IncludeManagers = 1 OR pn.Manager = 0) THEN 1 ELSE 0 END AS OnlyAllocatedProducts,
					CASE WHEN s.RestrictToAllocatedQty = 1 AND (s.IncludeManagers = 1 OR pn.Manager = 0) THEN 1 ELSE 0 END AS RestrictToAllocatedQty
				FROM {SubsidyPackNameTableName} sp
				INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id AND s.Deleted = 0 AND s.Active = 1
				INNER JOIN {PackNameTableName} pn ON sp.PackNameId = pn.Id
				INNER JOIN {ViewStoreSubsidyVariationSetting} vset ON s.StoreId = vset.StoreId
				LEFT JOIN {SubsidyVariationTableName} svar ON sp.Id = svar.SubsidyPackNameId AND svar.Deleted = 0
				CROSS APPLY
				(
					SELECT
						DurationStart = CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationStart, s.DurationStart) ELSE s.DurationStart END,
						DurationEnd   = CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationEnd, s.DurationEnd) ELSE s.DurationEnd END
				) eff
				CROSS APPLY [dbo].[subsidy_current_period](s.AnnualRecurrence, eff.DurationStart, eff.DurationEnd, vset.LocalToday) per
				CROSS APPLY [dbo].[subsidy_topup_period_end](CAST(SYSDATETIMEOFFSET() AT TIME ZONE vset.TimeZoneId AS datetime2), eff.DurationEnd, s.AnnualRecurrence) topupEnd
				OUTER APPLY
				(
					--SUM, not a plain join. A second topup row in the same period used to MULTIPLY this view's
					--rows, so the allocation read the assignment twice and both available and used credit were
					--double-counted. Folding them here makes a duplicate harmless: rows of
					--50 and 30 give one row of 80, exactly as a single row of 80 would.
					SELECT Credit = SUM(stopin.Credit)
					FROM {SubsidyTopupTableName} stopin
					CROSS APPLY [dbo].[subsidy_topup_period_end](CAST(stopin.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS datetime2), eff.DurationEnd, s.AnnualRecurrence) stopEnd
					WHERE stopin.SubsidyPackNameId = sp.Id
					  AND stopEnd.PeriodEnd = topupEnd.PeriodEnd
				) stop
				OUTER APPLY
				(
					SELECT
						SUM(soi.Credit) as UsedCredit,
						SUM(soi.CreditTopup) as UsedCreditTopup
					FROM {SubsidyNetConsumptionSource} soi
					INNER JOIN {OrderTableName} o ON soi.OrderId = o.Id
					INNER JOIN {PackNameOrderItemTableName} pnoi ON soi.OrderItemId = pnoi.OrderItemId
					WHERE soi.SubsidyId = sp.SubsidyId AND pnoi.PackNameId = sp.PackNameId
					  AND o.OrderStatusId != 40 AND o.Deleted = 0
					  AND CAST(o.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS date) >= per.PeriodStart
					  AND CAST(o.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS date) <= per.PeriodEnd
				) used
				OUTER APPLY
				(
					SELECT
						SUM(sos.Credit) as UsedCredit,
						SUM(sos.CreditTopup) as UsedCreditTopup
					FROM {SubsidyOrderShippingTableName} sos
					INNER JOIN {OrderTableName} o ON sos.OrderId = o.Id
					WHERE sos.SubsidyId = sp.SubsidyId AND sos.PackNameId = sp.PackNameId
					  AND o.OrderStatusId != 40 AND o.Deleted = 0
					  AND CAST(o.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS date) >= per.PeriodStart
					  AND CAST(o.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS date) <= per.PeriodEnd
				) usedShipping
				--In its ordering period: the first occurrence has begun and today is inside the current one.
				WHERE sp.Deleted = 0 AND sp.Active = 1
				  AND vset.LocalToday >= eff.DurationStart
				  AND vset.LocalToday >= per.PeriodStart
				  AND vset.LocalToday <= per.PeriodEnd";

    //View for supporting product group credits
    //Same shape as the subsidy credit view: eff, per and topupEnd are the assignment's effective dates, its
    //current occurrence and that occurrence's close.
    private static readonly string CreateViewProductGroupPackNameCreditsSql =
        $@"CREATE OR ALTER VIEW {ViewProductGroupPackNameCredit}
			AS
				SELECT
					sp.[Id] as Id,
					sp.[PackNameId],
					sp.[SubsidyId],
					spg.[ProductGroupId],
					pg.[Name] as ProductGroupName,
					spg.DisplayOrder as DisplayOrder,
					spg.[Credit],
					spg.[Quantity],
					COALESCE(pgtop.[Credit], 0) as CreditTopup,
					COALESCE(pgtop.[Quantity], 0) as QuantityTopup,
					COALESCE(used.UsedCredit, 0) as UsedCredit,
					COALESCE(used.UsedCreditTopup, 0) as UsedCreditTopup,
					COALESCE(used.UsedQuantity, 0) as UsedQuantity,
					COALESCE(used.UsedQuantityTopup, 0) as UsedQuantityTopup
				FROM {SubsidyPackNameTableName} sp
				INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id AND s.Deleted = 0 AND s.Active = 1
				INNER JOIN {SubsidyProductGroupTableName} spg ON sp.SubsidyId = spg.SubsidyId
				INNER JOIN {ProductGroupTableName} pg ON spg.ProductGroupId = pg.Id
				INNER JOIN {ViewStoreSubsidyVariationSetting} vset ON s.StoreId = vset.StoreId
				LEFT JOIN {SubsidyVariationTableName} svar ON sp.Id = svar.SubsidyPackNameId AND svar.Deleted = 0
				CROSS APPLY
				(
					SELECT
						DurationStart = CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationStart, s.DurationStart) ELSE s.DurationStart END,
						DurationEnd   = CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationEnd, s.DurationEnd) ELSE s.DurationEnd END
				) eff
				CROSS APPLY [dbo].[subsidy_current_period](s.AnnualRecurrence, eff.DurationStart, eff.DurationEnd, vset.LocalToday) per
				CROSS APPLY [dbo].[subsidy_topup_period_end](CAST(SYSDATETIMEOFFSET() AT TIME ZONE vset.TimeZoneId AS datetime2), eff.DurationEnd, s.AnnualRecurrence) topupEnd
				OUTER APPLY
				(
					--SUM for the same reason as the subsidy topups: a duplicate row multiplied this view and
					--double-counted both the allocation and the usage.
					SELECT Credit = SUM(pgtopin.Credit), Quantity = SUM(pgtopin.Quantity)
					FROM {ProductGroupTopupTableName} pgtopin
					CROSS APPLY [dbo].[subsidy_topup_period_end](CAST(pgtopin.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS datetime2), eff.DurationEnd, s.AnnualRecurrence) pgtopEnd
					WHERE pgtopin.SubsidyPackNameId = sp.Id
					  AND pgtopin.ProductGroupId = pg.Id
					  AND pgtopEnd.PeriodEnd = topupEnd.PeriodEnd
				) pgtop
				OUTER APPLY
				(
					SELECT
						SUM(soi.ProductGroupCredit) as UsedCredit,
						SUM(soi.ProductGroupCreditTopup) as UsedCreditTopup,
						SUM(soi.Quantity) as UsedQuantity,
						SUM(soi.QuantityTopup) as UsedQuantityTopup
					FROM {SubsidyNetConsumptionSource} soi
					INNER JOIN {OrderTableName} o ON soi.OrderId = o.Id
					INNER JOIN {PackNameOrderItemTableName} pnoi ON soi.OrderItemId = pnoi.OrderItemId
					WHERE soi.SubsidyId = sp.SubsidyId AND soi.ProductGroupId = pg.Id AND pnoi.PackNameId = sp.PackNameId
					  AND o.OrderStatusId != 40 AND o.Deleted = 0
					  AND CAST(o.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS date) >= per.PeriodStart
					  AND CAST(o.CreatedOnUtc AT TIME ZONE 'UTC' AT TIME ZONE vset.TimeZoneId AS date) <= per.PeriodEnd
				) used
				--In its ordering period: the first occurrence has begun and today is inside the current one.
				WHERE sp.Deleted = 0 AND sp.Active = 1 AND spg.Deleted = 0 AND pg.Deleted = 0 AND pg.Active = 1
				  AND vset.LocalToday >= eff.DurationStart
				  AND vset.LocalToday >= per.PeriodStart
				  AND vset.LocalToday <= per.PeriodEnd";

    //View for supporting subsidies in ordering period flag
    private static readonly string CreateViewSubsidyInOrderingPeriodSql =
        $@"CREATE OR ALTER VIEW {ViewSubsidyInOrderingPeriod}
			AS
				SELECT
					s.[Id],
					s.[Name],
					s.OrderOutsidePeriod,
					CAST(CASE WHEN vset.LocalToday >= s.DurationStart
							   AND vset.LocalToday >= per.PeriodStart
							   AND vset.LocalToday <= per.PeriodEnd THEN 1 ELSE 0 END AS bit) as InOrderingPeriod
				FROM {SubsidyTableName} s
				--A subsidy of a store with subsidies switched off is never in ordering: it has no effect at all.
				INNER JOIN {ViewStoreSubsidyVariationSetting} vset ON s.StoreId = vset.StoreId AND vset.SubsidyEnabled = 1
				CROSS APPLY [dbo].[subsidy_current_period](s.AnnualRecurrence, s.DurationStart, s.DurationEnd, vset.LocalToday) per
				WHERE s.Deleted = 0 AND s.Active  = 1";

    //View for supporting subsidy variation in ordering period flag for packNames
    private static readonly string CreateViewSubsidyVariationInOrderingPeriodSql =
        $@"CREATE OR ALTER VIEW {ViewSubsidyVariationInOrderingPeriod}
			AS
				SELECT
					sp.[Id],
					s.[Id] as SubsidyId,
					s.[Name],
					s.OrderOutsidePeriod,
					CAST(CASE WHEN vset.LocalToday >= eff.DurationStart
							   AND vset.LocalToday >= per.PeriodStart
							   AND vset.LocalToday <= per.PeriodEnd THEN 1 ELSE 0 END AS bit) as InOrderingPeriod
				FROM {SubsidyTableName} s
				INNER JOIN {SubsidyPackNameTableName} sp ON s.Id = sp.SubsidyId
				INNER JOIN {SubsidyVariationTableName} svar ON sp.Id = svar.SubsidyPackNameId
				--A subsidy of a store with subsidies switched off is never in ordering: it has no effect at all.
				INNER JOIN {ViewStoreSubsidyVariationSetting} vset ON s.StoreId = vset.StoreId AND vset.PackNamesOwnStartAndFinishDates = 1 AND vset.SubsidyEnabled = 1
				CROSS APPLY
				(
					SELECT
						DurationStart = COALESCE(svar.DurationStart, s.DurationStart),
						DurationEnd   = COALESCE(svar.DurationEnd, s.DurationEnd)
				) eff
				CROSS APPLY [dbo].[subsidy_current_period](s.AnnualRecurrence, eff.DurationStart, eff.DurationEnd, vset.LocalToday) per
				WHERE s.Deleted = 0 AND s.Active = 1 AND sp.Deleted = 0 AND sp.Active = 1 AND svar.Deleted = 0";

    //View for supporting subsidies can order flag
    private static readonly string CreateViewSubsidyPackNameCanOrderSql =
        $@"CREATE OR ALTER VIEW {ViewSubsidyPackNameCanOrder}
			AS
				SELECT
					pn.Id,
					ISNULL(c.CanOrder, 1) as CanOrder,
					ISNULL(c.OnlyAllocatedProducts, 0) AS OnlyAllocatedProducts
				FROM {PackNameTableName} pn
				LEFT JOIN
				(
					SELECT
						sp.[PackNameId],
						CASE WHEN SUM(CASE WHEN s.OnlyAllocatedProducts = 1 AND (s.IncludeManagers = 1 OR pn.Manager = 0) THEN 0 ELSE 1 END) = 0 THEN 1 ELSE 0 END AS OnlyAllocatedProducts,
						CASE WHEN SUM(CASE WHEN ISNULL(canorder.Id, 0)> 0 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS CanOrder
					FROM {SubsidyPackNameTableName} sp
					INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id
					--Only a subsidy of a store with subsidies switched on can block ordering.
					INNER JOIN {ViewStoreSubsidyVariationSetting} svset ON s.StoreId = svset.StoreId AND svset.SubsidyEnabled = 1
					INNER JOIN {PackNameTableName} pn ON sp.PackNameId = pn.Id
					LEFT JOIN
					(
						SELECT sp.[Id] as Id
						FROM {SubsidyPackNameTableName} sp
						INNER JOIN {SubsidyTableName} s ON sp.SubsidyId = s.Id
						INNER JOIN {PackNameTableName} p ON sp.PackNameId = p.Id
						INNER JOIN {ViewStoreSubsidyVariationSetting} vset ON s.StoreId = vset.StoreId
						LEFT JOIN {SubsidyVariationTableName} svar ON sp.Id = svar.SubsidyPackNameId AND svar.Deleted = 0
						CROSS APPLY
						(
							SELECT
								DurationStart = CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationStart, s.DurationStart) ELSE s.DurationStart END,
								DurationEnd   = CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationEnd, s.DurationEnd) ELSE s.DurationEnd END
						) eff
						CROSS APPLY [dbo].[subsidy_current_period](s.AnnualRecurrence, eff.DurationStart, eff.DurationEnd, vset.LocalToday) per
						WHERE sp.Deleted = 0 AND sp.Active = 1 AND s.Deleted  = 0 AND s.Active = 1 AND 
							(
								(
									vset.LocalToday >= eff.DurationStart
									AND vset.LocalToday >= per.PeriodStart
									AND vset.LocalToday <= per.PeriodEnd
								)
								OR
								(
									s.OrderOutsidePeriod = 1
									AND 
									(
										s.RestrictToAllocatedQty = 0
										OR 
										(
											s.IncludeManagers = 0
											AND p.Manager = 1
										)
									)
								)
							) 
					) canorder ON sp.Id = canorder.Id
					WHERE sp.Deleted = 0 AND sp.Active = 1 AND s.Deleted = 0 AND s.Active   = 1
					GROUP BY sp.PackNameId 
				) c ON pn.Id = c.PackNameId
				WHERE pn.Deleted = 0";

    //View for supporting Shopping Cart Item for PackNames
    private static readonly string CreateViewPackNameShoppingCartItemSql =
        $@"CREATE OR ALTER VIEW {ViewPackNameShoppingCartItem}
			AS
				SELECT
					sci.Id,
					sci.CustomerId,
					sci.ProductId,
					sci.StoreId,
					sci.ShoppingCartTypeId,
					sci.AttributesXml,
					sci.CustomerEnteredPrice,
					sci.Quantity,
					sci.RentalStartDateUtc,
					sci.RentalEndDateUtc,
					sci.CustomWishlistId,
					sci.CreatedOnUtc,
					sci.UpdatedOnUtc,
					pnci.PackNameId,
					pn.Name,
					pn.[Group],
					pn.CustomerId as PackNameCustomerId,
					pn.Colour,
                    b.BranchCode,
                    b.Name as Branch,
					b.Id as BranchId,
					pnl.Id as PackNameListId,
					s.Name as StoreName,
					dep.Name as Department,
					ISNULL(dep.Id, 0) as DepartmentId,
					cc.Name as CostCenter,
					ISNULL(cc.Id, 0) as CostCenterId
				FROM {ShoppingCartItemTableName} sci
				INNER JOIN {PackNameCartItemTableName} pnci ON sci.Id = pnci.ShoppingCartItemId
				INNER JOIN {PackNameTableName} pn ON pnci.PackNameId = pn.Id
				INNER JOIN {ViewSubsidyPackNameCanOrder} pno ON pn.Id = pno.Id
				INNER JOIN {PackNameListTableName} pnl ON pn.PackNameListId = pnl.Id
				INNER JOIN {BranchTableName} b ON pnl.BranchId = b.Id AND b.Deleted = 0
				LEFT JOIN {DepartmentTableName} dep ON pn.DepartmentId = dep.Id
				LEFT JOIN {CostCenterTableName} cc ON pn.CostCenterId = cc.Id
				INNER JOIN {StoreTableName} s ON sci.StoreId = s.Id
				WHERE pn.Deleted = 0 AND pn.Active = 1 AND pnl.Deleted = 0 AND s.Deleted = 0 AND pno.CanOrder = 1";

    //View to be able to get the setting for subsidy variations so we can do joins easily to validate the setting is enable
    private static readonly string CreateViewStoreSubsidyVariationSettingSql =
        $@"CREATE OR ALTER VIEW {ViewStoreSubsidyVariationSetting}
			AS
				SELECT
					ROW_NUMBER() OVER (ORDER BY (SELECT 1)) Id, --The Id value doesn't matter for this view (it's only used to satisfy BaseEntity.Id)
					g.StoreId,
					g.PackNamesOwnGlobalValue,
					g.PackNamesOwnStartAndFinishDates,
					g.TimeZoneId,
					--1 when subsidies are switched on for the store, as CWFeatureSet reads it: SubsidyEnable AND
					--PackNameEnable, the store's own value first and then the instance value, off when neither is set.
					--A store with subsidies off is never blocked by subsidy periods.
					g.SubsidyEnabled,
					--Every subsidy period in this store is judged against THIS date. A season that ends on
					--30 September ends when the 30th ends in the store's own zone - not the database
					--server's, and not UTC's.
					CAST(SYSDATETIMEOFFSET() AT TIME ZONE g.TimeZoneId AS date) as LocalToday
				FROM
				(
					SELECT
						sett.Id as StoreId,
						MIN(sett.PackNamesOwnGlobalValue) as PackNamesOwnGlobalValue,
						MIN(sett.PackNamesOwnStartAndFinishDates) as PackNamesOwnStartAndFinishDates,
						MIN(sett.TimeZoneId) as TimeZoneId,
						MIN(sett.SubsidyEnabled) as SubsidyEnabled
					FROM
					(
						SELECT
							s.Id,
							CASE WHEN COALESCE(gv.Value, instancegv.Value) = 'True' THEN 1 ELSE 0 END as PackNamesOwnGlobalValue,
							CASE WHEN COALESCE(dates.Value, instancedates.Value) = 'True' THEN 1 ELSE 0 END as PackNamesOwnStartAndFinishDates,
							--nop's own store time zone: this store's setting, then the instance setting.
							--The literal is a LAST RESORT for an install with no store time zone set anywhere.
							--It is deliberately fixed and NOT CURRENT_TIMEZONE_ID(): that needs SQL Server 2019
							--while the rest of this plugin runs on 2016 SP1, and the DATABASE server's zone is not
							--the WEB server's either, so it would not match nop's own C# fallback any better.
							--Subsidy_P1_Precheck.sql check 6 detects an install that would reach this.
							COALESCE(NULLIF(LTRIM(RTRIM(COALESCE(tz.Value, instancetz.Value))), ''), N'UTC') as TimeZoneId,
							CASE WHEN COALESCE(se.Value, instancese.Value) = 'True' AND COALESCE(pe.Value, instancepe.Value) = 'True' THEN 1 ELSE 0 END as SubsidyEnabled
						FROM {StoreTableName} s
						LEFT JOIN
						(
							SELECT
								Value,
								StoreId
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.packnamesownglobalvalue' 
						) gv ON s.Id = gv.StoreId
						LEFT JOIN
						(
							SELECT
								Value,
								StoreId
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.packnamesownstartandfinishdates' 
						) dates ON s.Id = dates.StoreId
						LEFT JOIN
						(
							SELECT
								Value,
								StoreId
							FROM {SettingTableName}
							WHERE Name = 'datetimesettings.defaultstoretimezoneid' 
						) tz ON s.Id = tz.StoreId
						LEFT JOIN
						(
							SELECT Value
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.packnamesownglobalvalue' AND StoreId = 0 
						) instancegv ON 1 = 1
						LEFT JOIN
						(
							SELECT Value
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.packnamesownstartandfinishdates' AND StoreId = 0 
						) instancedates ON 1 = 1 
						LEFT JOIN
						(
							SELECT Value
							FROM {SettingTableName}
							WHERE Name = 'datetimesettings.defaultstoretimezoneid' AND StoreId = 0 
						) instancetz ON 1 = 1 
						LEFT JOIN
						(
							SELECT
								Value,
								StoreId
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.subsidyenable'
						) se ON s.Id = se.StoreId
						LEFT JOIN
						(
							SELECT
								Value,
								StoreId
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.packnameenable'
						) pe ON s.Id = pe.StoreId
						LEFT JOIN
						(
							SELECT Value
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.subsidyenable' AND StoreId = 0
						) instancese ON 1 = 1
						LEFT JOIN
						(
							SELECT Value
							FROM {SettingTableName}
							WHERE Name = 'multistoresettings.packnameenable' AND StoreId = 0
						) instancepe ON 1 = 1
					) sett
					GROUP BY sett.Id
				) g";

    //View for supporting Order Item for PackNames
    private static readonly string CreateViewPackNameOrderItemSql =
        $@"CREATE OR ALTER VIEW {ViewPackNameOrderItem}
                AS
                    SELECT
                        oi.Id,
                        oi.OrderId,
                        oi.ProductId,
                        oi.OrderItemGuid,
                        oi.Quantity,
                        oi.UnitPriceInclTax,
                        oi.UnitPriceExclTax,
                        oi.PriceInclTax,
                        oi.PriceExclTax,
                        oi.DiscountAmountInclTax,
                        oi.DiscountAmountExclTax,
                        oi.OriginalProductCost,
                        oi.AttributeDescription,
                        oi.AttributesXml,
                        oi.DownloadCount,
                        oi.IsDownloadActivated,
                        oi.LicenseDownloadId,
                        oi.ItemWeight,
                        oi.RentalStartDateUtc,
                        oi.RentalEndDateUtc,
                        pnoi.PackNameId,
                        pnoi.PackName,
                        pnoi.PackNameCustomerId,
                        pnoi.BranchId,
                        pnoi.BranchCode,
                        pnoi.Branch,
                        pnoi.DepartmentId,
                        pnoi.Department,
                        pnoi.CostCenterId,
                        pnoi.CostCenter
                    FROM {OrderItemTableName} oi
                    INNER JOIN {PackNameOrderItemTableName} pnoi ON oi.Id = pnoi.OrderItemId";

    //View for supporting live-computed Credit Note balances (Amount minus non-cancelled-order consumption)
    private static readonly string CreateViewCreditNoteRemainSql =
        $@"CREATE OR ALTER VIEW {ViewCreditNoteRemain}
			AS
				SELECT
					cn.[Id],
					cn.[CustomerId],
					cn.[OrderId],
					cn.[CreatedOnUtc],
					cn.[Reference],
					cn.[Name],
					cn.[Amount],
					cn.[Amount] - ISNULL(SUM(CASE WHEN o.OrderStatusId != 40 THEN h.[Amount] ELSE 0 END), 0) as [Remain],
					cn.[ReturnReason],
					cn.[StoreId],
					cn.[ReturnRequestId],
					cn.[CreatedByCustomerId]
				FROM {CreditNoteTableName} cn
				LEFT JOIN {CreditNoteHistoryTableName} h ON h.CreditNoteId = cn.Id
				LEFT JOIN {OrderTableName} o ON o.Id = h.OrderId
				GROUP BY
					cn.[Id], cn.[CustomerId], cn.[OrderId], cn.[CreatedOnUtc], cn.[Reference], cn.[Name],
					cn.[Amount], cn.[ReturnReason], cn.[StoreId], cn.[ReturnRequestId], cn.[CreatedByCustomerId]";

    //View for supporting live-computed, per-event running Credit Note history balances.
    //The running total skips any history row whose order is cancelled (OrderStatusId = 40), the
    //same exclusion CreateViewCreditNoteRemainSql applies to the header balance — otherwise a
    //cancelled order's consumption would still show as "spent" in every later row's running Remain,
    //even though the header balance has already excluded it.
    private static readonly string CreateViewCreditNoteHistoryRemainSql =
        $@"CREATE OR ALTER VIEW {ViewCreditNoteHistoryRemain}
			AS
				SELECT
					h.[Id],
					h.[CreditNoteId],
					h.[CustomerId],
					h.[OrderId],
					h.[CreatedOnUtc],
					h.[Amount],
					cn.[Amount] - SUM(CASE WHEN o.OrderStatusId != 40 THEN h.[Amount] ELSE 0 END) OVER (PARTITION BY h.[CreditNoteId] ORDER BY h.[CreatedOnUtc], h.[Id] ROWS UNBOUNDED PRECEDING) as [Remain]
				FROM {CreditNoteHistoryTableName} h
				INNER JOIN {CreditNoteTableName} cn ON cn.Id = h.CreditNoteId
				LEFT JOIN {OrderTableName} o ON o.Id = h.OrderId";

    //View resolving the address a shipment actually went to — see ShipmentAddress
    private static readonly string CreateViewShipmentAddressSql =
        $@"CREATE OR ALTER VIEW {ViewShipmentAddress}
			AS
				SELECT
					s.[Id] as Id,
					s.[Id] as ShipmentId,
					s.[OrderId],
					COALESCE(
						(
							SELECT TOP 1
								CASE WHEN os.[PickupInStore] = 1 THEN os.[PickupAddressId] ELSE os.[ShippingAddressId] END
							FROM {OrderShippingTableName} os
							INNER JOIN {PackNameOrderItemTableName} pnoi ON pnoi.PackNameId = os.PackNameId
							INNER JOIN {ShipmentItemTableName} si ON si.OrderItemId = pnoi.OrderItemId
							WHERE os.OrderId = s.[OrderId] AND si.ShipmentId = s.[Id]
						),
						CASE WHEN o.[PickupInStore] = 1 THEN o.[PickupAddressId] ELSE o.[ShippingAddressId] END
					) as AddressId
				FROM {ShipmentTableName} s
				INNER JOIN {OrderTableName} o ON o.Id = s.[OrderId]";

    #endregion

    #region Ctor

    public CWDatabaseService(INopDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    #endregion

    #region Utilities

    protected async Task ExecuteCreateDatabaseScriptsAsync()
    {
        await _dataProvider.ExecuteNonQueryAsync(CreateSPGetPackNameListIdsByManagerSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewStoreSubsidyVariationSettingSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateSPGetSubsidyHistorySql, NoParameters);
        //The two period functions precede the views that apply them: deferred name resolution does not cover
        //functions.
        await _dataProvider.ExecuteNonQueryAsync(CreateFunctionSubsidyCurrentPeriodSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateFunctionSubsidyTopupPeriodEndSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewSubsidyPackNameCreditsSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewProductGroupPackNameCreditsSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewSubsidyInOrderingPeriodSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewSubsidyVariationInOrderingPeriodSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewSubsidyPackNameCanOrderSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewPackNameShoppingCartItemSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewPackNameOrderItemSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewShipmentAddressSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewCreditNoteRemainSql, NoParameters);
        await _dataProvider.ExecuteNonQueryAsync(CreateViewCreditNoteHistoryRemainSql, NoParameters);
        //Last, once no view refers to them any more.
        await _dataProvider.ExecuteNonQueryAsync(DropScalarPeriodFunctionsSql, NoParameters);
    }

    #endregion

    #region Methods

    public async Task UpdateDatabaseScriptsAsync()
    {
        await ExecuteCreateDatabaseScriptsAsync();
    }

    #endregion
}
