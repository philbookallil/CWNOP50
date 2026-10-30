using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Data.Mapping.Orders
{
    public class OrderSettlementBuilder : NopEntityBuilder<OrderSettlement>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            //One settlement per order. The unique index also serves the foreign key, so the key is declared without
            //the extra index nop's ForeignKey<T>() adds - two indexes would share one default name.
            table
                .WithColumn(nameof(OrderSettlement.Id)).AsInt32().PrimaryKey().Identity()
                .WithColumn(nameof(OrderSettlement.OrderId)).AsInt32().NotNullable().Unique()
                    .ForeignKey(NameCompatibilityManager.GetTableName(typeof(Order)), nameof(Order.Id)).OnDelete(Rule.Cascade)
                .WithColumn(nameof(OrderSettlement.SettleableAmount)).AsDecimal(18, 4).NotNullable()
                .WithColumn(nameof(OrderSettlement.SubsidyStockAmount)).AsDecimal(18, 4).NotNullable()
                .WithColumn(nameof(OrderSettlement.SubsidyShippingAmount)).AsDecimal(18, 4).NotNullable()
                .WithColumn(nameof(OrderSettlement.CreditNoteAmount)).AsDecimal(18, 4).NotNullable()
                .WithColumn(nameof(OrderSettlement.CreatedOnUtc)).AsDateTime2().NotNullable();
        }

        #endregion
    }
}