using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Mapping;

public class OdooLogBuilder : NopEntityBuilder<OdooLog>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(OdooLog.ShortMessage)).AsString(int.MaxValue).NotNullable()
            .WithColumn(nameof(OdooLog.FullMessage)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(OdooLog.EntityType)).AsString(200).Nullable()
            .WithColumn(nameof(OdooLog.OdooEntityId)).AsInt32().Nullable()
            .WithColumn(nameof(OdooLog.NopEntityId)).AsInt32().Nullable();
    }
}
