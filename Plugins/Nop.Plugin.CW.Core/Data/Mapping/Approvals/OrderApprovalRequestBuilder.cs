using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class OrderApprovalRequestBuilder : NopEntityBuilder<OrderApprovalRequest>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        //An approval link is looked up by the hash of its token, so the hash is unique and indexed.
        table.WithColumn(nameof(OrderApprovalRequest.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(OrderApprovalRequest.OrderApprovalId)).AsInt32().ForeignKey<OrderApproval>(onDelete: Rule.Cascade).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.ApproverId)).AsInt32().ForeignKey<Approver>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.ApproverName)).AsString(400).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.ApproverEmail)).AsString(255).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.TokenHash)).AsString(64).NotNullable().Unique()
             .WithColumn(nameof(OrderApprovalRequest.SentOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.RevokedOnUtc)).AsDateTime2().Nullable();
    }
}
