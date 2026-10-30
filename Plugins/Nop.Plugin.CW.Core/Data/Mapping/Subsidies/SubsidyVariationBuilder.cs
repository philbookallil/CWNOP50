using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyVariationBuilder : NopEntityBuilder<SubsidyVariation>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyVariation.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyVariation.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyVariation.Credit)).AsDecimal().Nullable()
             .WithColumn(nameof(SubsidyVariation.DurationStart)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariation.DurationEnd)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariation.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(SubsidyVariation.Deleted)).AsBoolean().NotNullable();
    }
}
