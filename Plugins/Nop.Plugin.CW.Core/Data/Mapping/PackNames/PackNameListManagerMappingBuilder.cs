using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Data.Mapping.PackNames;

public class PackNameListManagerMappingBuilder : NopEntityBuilder<PackNameListManagerMapping>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(PackNameListManagerMapping.PackNameListId))
                .AsInt32().ForeignKey<PackNameList>().PrimaryKey()
            .WithColumn(nameof(PackNameListManagerMapping.PackNameId))
                .AsInt32().ForeignKey<PackName>().PrimaryKey();
    }
}