using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyReminderBuilder : NopEntityBuilder<SubsidyReminder>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyReminder.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyReminder.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyReminder.PeriodEnd)).AsDate().NotNullable()
             .WithColumn(nameof(SubsidyReminder.LastFollowUpNumber)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyReminder.LastFollowUpOnUtc)).AsDateTime2().NotNullable();
    }
}