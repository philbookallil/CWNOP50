using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Orders;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Orders
{
    public class OrderShippingBuilder : NopEntityBuilder<OrderShipping>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(OrderShipping.Id)).AsInt32().PrimaryKey().Identity()
                .WithColumn(nameof(OrderShipping.OrderId)).AsInt32().ForeignKey<Order>()
                .WithColumn(nameof(OrderShipping.PackNameId)).AsInt32().Nullable()
                .WithColumn(nameof(OrderShipping.ShippingAddressId)).AsInt32().Nullable().ForeignKey<Address>(onDelete: Rule.None)
                .WithColumn(nameof(OrderShipping.PickupAddressId)).AsInt32().Nullable().ForeignKey<Address>(onDelete: Rule.None);
        }

        #endregion
    }
}
