using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Catalog;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class ProductGroupProductBuilder : NopEntityBuilder<ProductGroupProduct>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ProductGroupProduct.ProductGroupId)).AsInt32().ForeignKey<ProductGroup>().PrimaryKey()
             .WithColumn(nameof(ProductGroupProduct.ProductId)).AsInt32().ForeignKey<Product>().PrimaryKey();
    }
}
