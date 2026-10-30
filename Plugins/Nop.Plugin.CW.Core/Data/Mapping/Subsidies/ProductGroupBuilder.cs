using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class ProductGroupBuilder : NopEntityBuilder<ProductGroup>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ProductGroup.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ProductGroup.StoreId)).AsInt32().ForeignKey<Store>().NotNullable()
             .WithColumn(nameof(ProductGroup.Name)).AsString(100).NotNullable()
             .WithColumn(nameof(ProductGroup.Description)).AsString().Nullable()
             .WithColumn(nameof(ProductGroup.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(ProductGroup.Icon)).AsString().Nullable()
             .WithColumn(nameof(ProductGroup.Colour)).AsString(7).Nullable()
             .WithColumn(nameof(ProductGroup.DisplayOrder)).AsInt32().NotNullable()
             .WithColumn(nameof(ProductGroup.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(ProductGroup.Deleted)).AsBoolean().NotNullable();
    }
}
