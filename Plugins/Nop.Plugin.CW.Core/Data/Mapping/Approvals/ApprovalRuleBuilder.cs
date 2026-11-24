using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class ApprovalRuleBuilder : NopEntityBuilder<ApprovalRule>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ApprovalRule.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ApprovalRule.StoreId)).AsInt32().NotNullable()
             .WithColumn(nameof(ApprovalRule.ScopeTypeId)).AsInt32().NotNullable()
             .WithColumn(nameof(ApprovalRule.ScopeId)).AsInt32().NotNullable()
             .WithColumn(nameof(ApprovalRule.Threshold)).AsDecimal(18, 4).Nullable()
             .WithColumn(nameof(ApprovalRule.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(ApprovalRule.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(ApprovalRule.UpdatedOnUtc)).AsDateTime2().NotNullable();
    }
}
