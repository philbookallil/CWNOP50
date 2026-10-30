using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyPackNameBuilder : NopEntityBuilder<SubsidyPackName>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyPackName.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyPackName.PackNameId)).AsInt32().ForeignKey<PackName>().NotNullable()
             .WithColumn(nameof(SubsidyPackName.SubsidyId)).AsInt32().ForeignKey<Domain.Subsidies.Subsidy>().NotNullable()
             .WithColumn(nameof(SubsidyPackName.CreatorId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyPackName.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(SubsidyPackName.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(SubsidyPackName.Deleted)).AsBoolean().NotNullable();
    }
}
