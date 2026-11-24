using FluentMigrator;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Gives each branch or department one approval rule, on a database installed before the index was in the schema
/// </summary>
/// <remarks>
/// An installation creates the index from <see cref="SchemaMigration"/> and nop marks this migration as applied without
/// running it; a plugin update runs it.
/// </remarks>
[NopMigration(MigrationVersions.ApprovalRuleUniqueScope, "CW.Core one approval rule per branch or department", MigrationProcessType.Update)]
public class ApprovalRuleUniqueScopeMigration : Migration
{
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(ApprovalRule));

        if (!Schema.Table(table).Exists() || Schema.Table(table).Index(SchemaMigration.ApprovalRuleUniqueIndex).Exists())
            return;

        Create.Index(SchemaMigration.ApprovalRuleUniqueIndex)
            .OnTable(table)
            .OnColumn(nameof(ApprovalRule.ScopeTypeId)).Ascending()
            .OnColumn(nameof(ApprovalRule.ScopeId)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(ApprovalRule));

        if (Schema.Table(table).Exists() && Schema.Table(table).Index(SchemaMigration.ApprovalRuleUniqueIndex).Exists())
            Delete.Index(SchemaMigration.ApprovalRuleUniqueIndex).OnTable(table);
    }
}
