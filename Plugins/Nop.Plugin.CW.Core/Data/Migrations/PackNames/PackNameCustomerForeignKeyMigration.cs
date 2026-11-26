using System.Data;
using FluentMigrator;
using Nop.Core.Domain.Customers;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Makes the packname → customer key clear the link when the customer's row is deleted, on a database installed when the
/// key refused the delete
/// </summary>
/// <remarks>
/// nop's "Delete guests" task deletes every guest in one statement and publishes no event, so one guest a packname still
/// points to stopped the task for all of them.
/// <para>
/// An installation creates the key from the builder and nop marks this migration as applied without running it; a plugin
/// update runs it. Replacing a key that already clears the link leaves the same key.
/// </para>
/// </remarks>
[NopMigration(MigrationVersions.PackNameCustomerForeignKey, "CW.Core packname customer key set null on delete", MigrationProcessType.Update)]
public class PackNameCustomerForeignKeyMigration : Migration
{
    #region Fields

    /// <summary>
    /// The name nop gave the key when the schema created it
    /// </summary>
    internal const string ForeignKeyName = "FK_CORE_PN_PackName_CustomerId_Customer_Id";

    #endregion

    #region Utilities

    /// <summary>
    /// Replaces the key by one with another delete rule
    /// </summary>
    /// <param name="onDelete">What happens to the packname when its customer's row is deleted</param>
    private void ReplaceForeignKey(Rule onDelete)
    {
        var table = NameCompatibilityManager.GetTableName(typeof(PackName));

        if (!Schema.Table(table).Exists())
            return;

        if (Schema.Table(table).Constraint(ForeignKeyName).Exists())
            Delete.ForeignKey(ForeignKeyName).OnTable(table);

        Create.ForeignKey(ForeignKeyName)
            .FromTable(table).ForeignColumn(nameof(PackName.CustomerId))
            .ToTable(NameCompatibilityManager.GetTableName(typeof(Customer))).PrimaryColumn(nameof(Customer.Id))
            .OnDelete(onDelete);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Replaces the key by one that clears the link
    /// </summary>
    public override void Up()
    {
        ReplaceForeignKey(Rule.SetNull);
    }

    /// <summary>
    /// Puts back the key that refuses the customer's delete
    /// </summary>
    public override void Down()
    {
        ReplaceForeignKey(Rule.None);
    }

    #endregion
}
