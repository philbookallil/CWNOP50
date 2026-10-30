using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Data.Mapping.Orders;

public class PackNameCartItemBuilder : NopEntityBuilder<PackNameCartItem>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(PackNameCartItem.ShoppingCartItemId)).AsInt32().ForeignKey<ShoppingCartItem>().PrimaryKey()
            .WithColumn(nameof(PackNameCartItem.PackNameId)).AsInt32().ForeignKey<PackName>();
    }

    #endregion
}
