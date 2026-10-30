using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Data.Mapping.Orders
{
    public class PackNameOrderItemBuilder : NopEntityBuilder<PackNameOrderItem>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(PackNameOrderItem.OrderItemId)).AsInt32().ForeignKey<OrderItem>().PrimaryKey()
                .WithColumn(nameof(PackNameOrderItem.PackNameId)).AsInt32().ForeignKey<PackName>()
                .WithColumn(nameof(PackNameOrderItem.PackName)).AsString().Nullable()
                .WithColumn(nameof(PackNameOrderItem.PackNameCustomerId)).AsInt32().Nullable()
                .WithColumn(nameof(PackNameOrderItem.BranchId)).AsInt32().NotNullable()
                .WithColumn(nameof(PackNameOrderItem.BranchCode)).AsString().Nullable()
                .WithColumn(nameof(PackNameOrderItem.Branch)).AsString().Nullable()
                .WithColumn(nameof(PackNameOrderItem.DepartmentId)).AsInt32().NotNullable()
                .WithColumn(nameof(PackNameOrderItem.Department)).AsString().Nullable()
                .WithColumn(nameof(PackNameOrderItem.CostCenterId)).AsInt32().NotNullable()
                .WithColumn(nameof(PackNameOrderItem.CostCenter)).AsString().Nullable();
        }

        #endregion
    }
}
