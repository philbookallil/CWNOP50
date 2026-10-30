using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyRestoreItemBuilder : NopEntityBuilder<SubsidyRestoreItem>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyRestoreItem.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyRestoreItem.OrderId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.OrderItemId)).AsInt32().ForeignKey<OrderItem>().NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.SubsidyId)).AsInt32().ForeignKey<Subsidy>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.ProductGroupId)).AsInt32().ForeignKey<ProductGroup>(onDelete: Rule.None).Nullable()
             .WithColumn(nameof(SubsidyRestoreItem.SubsidyOrderItemId)).AsInt32().Nullable()
             .WithColumn(nameof(SubsidyRestoreItem.ReturnCaseLineId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.ReturnRequestId)).AsInt32().Nullable()
             .WithColumn(nameof(SubsidyRestoreItem.ReturnedQuantity)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.QuantityTopup)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.CreditTopup)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.ProductGroupCredit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.ProductGroupCreditTopup)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyRestoreItem.CreatedByCustomerId)).AsInt32().Nullable()
             .WithColumn(nameof(SubsidyRestoreItem.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}