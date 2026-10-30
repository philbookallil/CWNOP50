using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ReasonActionRelationshipBuilder : NopEntityBuilder<ReasonActionRelationship>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReasonActionRelationship.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReasonActionRelationship.ReasonId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReasonActionRelationship.ActionId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReasonActionRelationship.StoreId)).AsInt32().NotNullable();
    }
}
