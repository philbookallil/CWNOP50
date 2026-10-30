using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyVariationHistoryBuilder : NopEntityBuilder<SubsidyVariationHistory>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyVariationHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyVariationHistory.SubsidyVariationId)).AsInt32().ForeignKey<SubsidyVariation>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyVariationHistory.Credit)).AsDecimal().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.DurationStart)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.DurationEnd)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.CreatorId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyVariationHistory.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
