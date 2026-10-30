using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ImpersonationTokenBuilder : NopEntityBuilder<ImpersonationToken>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ImpersonationToken.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ImpersonationToken.Token)).AsString().NotNullable()
            .WithColumn(nameof(ImpersonationToken.OwnerId)).AsInt32().NotNullable()
            .WithColumn(nameof(ImpersonationToken.UserId)).AsInt32().NotNullable()
            .WithColumn(nameof(ImpersonationToken.CreatedOn)).AsDateTime2().NotNullable()
            .WithColumn(nameof(ImpersonationToken.StoreId)).AsInt32().NotNullable();
    }
}
