using FluentMigrator;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Adds ReturnRequestItem.ApprovalQueuedEmailId to a database installed before the column was part of the schema
/// </summary>
/// <remarks>
/// An installation creates the column from the builder and nop marks this migration as applied without running it;
/// a plugin update runs it, and it adds the column only when it is missing.
/// </remarks>
[NopMigration(MigrationVersions.ReturnRequestItemApprovalQueuedEmailId, "CW.Core ReturnRequestItem.ApprovalQueuedEmailId", MigrationProcessType.Update)]
public class ReturnRequestItemApprovalQueuedEmailIdMigration : Migration
{
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(ReturnRequestItem));

        if (!Schema.Table(table).Column(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).Exists())
            Alter.Table(table).AddColumn(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).AsInt32().Nullable();
    }

    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(ReturnRequestItem));

        if (Schema.Table(table).Column(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).Exists())
            Delete.Column(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).FromTable(table);
    }
}