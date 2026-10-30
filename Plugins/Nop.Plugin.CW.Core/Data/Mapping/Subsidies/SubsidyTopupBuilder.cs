using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyTopupBuilder : NopEntityBuilder<SubsidyTopup>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyTopup.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyTopup.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyTopup.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyTopup.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
