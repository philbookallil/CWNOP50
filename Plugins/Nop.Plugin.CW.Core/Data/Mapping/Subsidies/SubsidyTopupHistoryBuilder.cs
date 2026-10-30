using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyTopupHistoryBuilder : NopEntityBuilder<SubsidyTopupHistory>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyTopupHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyTopupHistory.SubsidyTopupId)).AsInt32().ForeignKey<SubsidyTopup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.Credit)).AsDecimal().NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(SubsidyTopupHistory.CreatorId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
