using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class ApprovalRuleApproverBuilder : NopEntityBuilder<ApprovalRuleApprover>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ApprovalRuleApprover.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ApprovalRuleApprover.ApprovalRuleId)).AsInt32().ForeignKey<ApprovalRule>(onDelete: Rule.Cascade).NotNullable()
             .WithColumn(nameof(ApprovalRuleApprover.ApproverId)).AsInt32().ForeignKey<Approver>(onDelete: Rule.None).NotNullable();
    }
}
