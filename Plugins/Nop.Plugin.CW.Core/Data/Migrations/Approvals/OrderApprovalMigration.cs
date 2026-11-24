using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Adds the order approval tables to a database installed before they were part of the schema
/// </summary>
/// <remarks>
/// An installation creates the tables from <see cref="SchemaMigration"/> and nop marks this migration as applied without
/// running it; a plugin update runs it, and it creates each table only when it is missing.
/// </remarks>
[NopMigration(MigrationVersions.OrderApproval, "CW.Core order approval tables", MigrationProcessType.Update)]
public class OrderApprovalMigration : Migration
{
    /// <summary>
    /// The approval tables, each after the tables its foreign keys point to
    /// </summary>
    private static readonly Type[] Tables =
    [
        typeof(Approver),
        typeof(ApprovalRule),
        typeof(ApprovalRuleApprover),
        typeof(OrderApproval),
        typeof(OrderApprovalRequest)
    ];

    public override void Up()
    {
        this.CreateTableIfNotExists<Approver>();
        this.CreateTableIfNotExists<ApprovalRule>();
        this.CreateTableIfNotExists<ApprovalRuleApprover>();
        this.CreateTableIfNotExists<OrderApproval>();
        this.CreateTableIfNotExists<OrderApprovalRequest>();
    }

    public override void Down()
    {
        //Enumerable.Reverse by name: on an array, .Reverse() binds to the in-place span overload, which returns nothing.
        foreach (var entityType in Enumerable.Reverse(Tables))
        {
            var table = NameCompatibilityManager.GetTableName(entityType);

            if (Schema.Table(table).Exists())
                Delete.Table(table);
        }
    }
}
