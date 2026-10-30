using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyProductGroupBuilder : NopEntityBuilder<SubsidyProductGroup>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyProductGroup.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyProductGroup.SubsidyId)).AsInt32().ForeignKey<Subsidy>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.ProductGroupId)).AsInt32().ForeignKey<ProductGroup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.DisplayOrder)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.CreatorId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(SubsidyProductGroup.Deleted)).AsBoolean().NotNullable();
    }
}
