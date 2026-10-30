using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Returns;

namespace Nop.Plugin.CW.Core.Data.Mapping.Returns;

public class ReturnResolutionBuilder : NopEntityBuilder<ReturnResolution>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnResolution.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnResolution.ReturnCaseId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnResolution.ResolvedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(ReturnResolution.ResolvedByCustomerId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnResolution.BalanceDue)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(ReturnResolution.StatusId)).AsInt32().NotNullable();
    }
}