using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class OrderApprovalBuilder : NopEntityBuilder<OrderApproval>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        //One approval per order. The unique index also serves the foreign key, so the key is declared without the
        //extra index nop's ForeignKey<T>() adds, as OrderSettlementBuilder does.
        table.WithColumn(nameof(OrderApproval.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(OrderApproval.OrderId)).AsInt32().NotNullable().Unique()
                .ForeignKey(NameCompatibilityManager.GetTableName(typeof(Order)), nameof(Order.Id)).OnDelete(Rule.Cascade)
             .WithColumn(nameof(OrderApproval.StatusId)).AsInt32().NotNullable()
             .WithColumn(nameof(OrderApproval.MeasuredAmount)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(OrderApproval.RequestedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(OrderApproval.LastReminderNumber)).AsInt32().NotNullable()
             .WithColumn(nameof(OrderApproval.DecidedBy)).AsString(500).Nullable()
             .WithColumn(nameof(OrderApproval.DecidedByApproverId)).AsInt32().Nullable()
             .WithColumn(nameof(OrderApproval.DecidedByCustomerId)).AsInt32().Nullable()
             .WithColumn(nameof(OrderApproval.DecidedOnUtc)).AsDateTime2().Nullable()
             .WithColumn(nameof(OrderApproval.DecisionComment)).AsString(int.MaxValue).Nullable()
             .WithColumn(nameof(OrderApproval.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
