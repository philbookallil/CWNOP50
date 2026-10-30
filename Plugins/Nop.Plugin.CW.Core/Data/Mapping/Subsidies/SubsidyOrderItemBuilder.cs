using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyOrderItemBuilder : NopEntityBuilder<SubsidyOrderItem>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyOrderItem.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyOrderItem.OrderId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.OrderItemId)).AsInt32().ForeignKey<OrderItem>().NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.SubsidyId)).AsInt32().ForeignKey<Subsidy>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.ProductGroupId)).AsInt32().ForeignKey<ProductGroup>(onDelete: Rule.None).Nullable()
             .WithColumn(nameof(SubsidyOrderItem.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.CreditTopup)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.QuantityTopup)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.ProductGroupCredit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyOrderItem.ProductGroupCreditTopup)).AsDecimal(18, 4).NotNullable();
    }
}
