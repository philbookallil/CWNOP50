using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Reflection;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Creates the CW.Core tables and the indexes their builders do not declare
/// </summary>
/// <remarks>
/// The SQL views, functions and procedures are not created here: ICWDatabaseService.UpdateDatabaseScriptsAsync
/// creates or alters them on every install and update of the plugin.
/// </remarks>
[NopMigration(MigrationVersions.InitialSchema, "CW.Core schema", MigrationProcessType.Installation)]
public class SchemaMigration : Migration
{
    #region Fields

    /// <summary>
    /// Every CW.Core table, each listed after the tables its foreign keys point to
    /// </summary>
    internal static readonly Type[] TablesInCreateOrder =
    [
        typeof(Branch),
        typeof(BranchAddressMapping),
        typeof(CreditNote),
        typeof(CreditNoteHistory),
        typeof(CustomerBranch),
        typeof(FeeRule),
        typeof(ImpersonationToken),
        typeof(ReasonActionRelationship),
        typeof(ReturnActionRule),
        typeof(ReturnReasonRule),
        typeof(ReturnReplacementItem),
        typeof(ReturnRequestItem),
        typeof(ReturnStatusRule),
        typeof(CostCenter),
        typeof(CostCenterAddressMapping),
        typeof(Department),
        typeof(PackNameList),
        typeof(PackName),
        typeof(DepartmentManagerMapping),
        typeof(OrderSettlement),
        typeof(OrderShipping),
        typeof(PackNameCartItem),
        typeof(PackNameOrderItem),
        typeof(PackNameAddressMapping),
        typeof(PackNameListManagerMapping),
        typeof(ReturnCase),
        typeof(ReturnCaseLine),
        typeof(ReturnCharge),
        typeof(ReturnResolution),
        typeof(ProductGroup),
        typeof(ProductGroupProduct),
        typeof(Subsidy),
        typeof(SubsidyPackName),
        typeof(ProductGroupTopup),
        typeof(ProductGroupTopupHistory),
        typeof(SubsidyOrderItem),
        typeof(SubsidyOrderShipping),
        typeof(SubsidyProductGroup),
        typeof(SubsidyRestoreItem),
        typeof(SubsidyTopup),
        typeof(SubsidyTopupHistory),
        typeof(SubsidyVariation),
        typeof(SubsidyVariationHistory)
    ];

    /// <summary>
    /// nop's CreateTableIfNotExists&lt;TEntity&gt;, called for each type of the table list
    /// </summary>
    private static readonly MethodInfo _createTableIfNotExists =
        typeof(FluentMigratorExtensions).GetMethod(nameof(FluentMigratorExtensions.CreateTableIfNotExists));

    #endregion

    #region Utilities

    /// <summary>
    /// Creates a unique index over the live rows only, so soft-deleted rows may repeat the key
    /// </summary>
    /// <param name="entityType">Entity of the table</param>
    /// <param name="indexName">Index name</param>
    /// <param name="columns">Indexed columns</param>
    private void CreateLiveRowsUniqueIndex(Type entityType, string indexName, params string[] columns)
    {
        var tableName = NameCompatibilityManager.GetTableName(entityType);
        var columnList = string.Join(", ", columns.Select(column => $"[{column}]"));

        Execute.Sql($"CREATE UNIQUE NONCLUSTERED INDEX [{indexName}] ON [{tableName}] ({columnList}) WHERE [Deleted] = 0;");
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates the tables, then the indexes
    /// </summary>
    public override void Up()
    {
        foreach (var entityType in TablesInCreateOrder)
            _createTableIfNotExists.MakeGenericMethod(entityType).Invoke(null, new object[] { this });

        //A builder's ForeignKey<T>() already indexes its column as IX_<table>_<column>; these are the indexes no
        //builder declares. They are created unconditionally: Schema...Exists() runs while the migration is being
        //built, before any of its tables exist, so it cannot tell whether an index is there.
        var restoreItemTable = NameCompatibilityManager.GetTableName(typeof(SubsidyRestoreItem));
        Create.Index($"IX_{restoreItemTable}_{nameof(SubsidyRestoreItem.ReturnCaseLineId)}")
            .OnTable(restoreItemTable)
            .OnColumn(nameof(SubsidyRestoreItem.ReturnCaseLineId)).Ascending()
            .WithOptions().NonClustered();

        Create.Index("UX_CORE_RET_ReturnResolution_ReturnCaseId")
            .OnTable(NameCompatibilityManager.GetTableName(typeof(ReturnResolution)))
            .OnColumn(nameof(ReturnResolution.ReturnCaseId)).Ascending()
            .WithOptions().Unique();

        CreateLiveRowsUniqueIndex(typeof(SubsidyPackName), "UX_CORE_SUB_SubsidyPackName_SubsidyId_PackNameId",
            nameof(SubsidyPackName.SubsidyId), nameof(SubsidyPackName.PackNameId));
        CreateLiveRowsUniqueIndex(typeof(SubsidyProductGroup), "UX_CORE_SUB_SubsidyProductGroup_SubsidyId_ProductGroupId",
            nameof(SubsidyProductGroup.SubsidyId), nameof(SubsidyProductGroup.ProductGroupId));
        CreateLiveRowsUniqueIndex(typeof(SubsidyVariation), "UX_CORE_SUB_SubsidyVariation_SubsidyPackNameId",
            nameof(SubsidyVariation.SubsidyPackNameId));
    }

    /// <summary>
    /// Drops the tables, each before the tables its foreign keys point to
    /// </summary>
    public override void Down()
    {
        //Enumerable.Reverse by name: on an array, .Reverse() binds to the in-place span overload, which returns nothing.
        foreach (var entityType in Enumerable.Reverse(TablesInCreateOrder))
        {
            var tableName = NameCompatibilityManager.GetTableName(entityType);
            if (Schema.Table(tableName).Exists())
                Delete.Table(tableName);
        }
    }

    #endregion
}