using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Mapping;

public class OdooOrderMappingBuilder : NopEntityBuilder<OdooOrderMapping>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(OdooOrderMapping.NopOrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooOrderMapping.DestinationKey)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooOrderMapping.OdooOrderId)).AsInt32().Nullable()
            .WithColumn(nameof(OdooOrderMapping.OdooOrderName)).AsString(64).Nullable()
            .WithColumn(nameof(OdooOrderMapping.StatusId)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooOrderMapping.Message)).AsString(int.MaxValue).Nullable();
    }
}
