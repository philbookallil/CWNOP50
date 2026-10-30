using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class ProductGroupTopupBuilder : NopEntityBuilder<ProductGroupTopup>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ProductGroupTopup.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ProductGroupTopup.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(ProductGroupTopup.ProductGroupId)).AsInt32().ForeignKey<ProductGroup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(ProductGroupTopup.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(ProductGroupTopup.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(ProductGroupTopup.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
