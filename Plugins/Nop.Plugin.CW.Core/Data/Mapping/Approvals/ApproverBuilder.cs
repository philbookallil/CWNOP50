using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class ApproverBuilder : NopEntityBuilder<Approver>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(Approver.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(Approver.StoreId)).AsInt32().NotNullable()
             .WithColumn(nameof(Approver.Name)).AsString(400).NotNullable()
             .WithColumn(nameof(Approver.Email)).AsString(255).NotNullable()
             .WithColumn(nameof(Approver.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(Approver.Deleted)).AsBoolean().NotNullable()
             .WithColumn(nameof(Approver.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
