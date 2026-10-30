using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.CreditNotes;

namespace Nop.Plugin.CW.Core.Data.Mapping.CreditNotes;

public class CreditNoteBuilder : NopEntityBuilder<CreditNote>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(CreditNote.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(CreditNote.CustomerId)).AsInt32().NotNullable()
            .WithColumn(nameof(CreditNote.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(CreditNote.Reference)).AsInt64().Nullable()
            .WithColumn(nameof(CreditNote.Name)).AsString(500).NotNullable()
            .WithColumn(nameof(CreditNote.Amount)).AsDecimal(18, 2).NotNullable()
            .WithColumn(nameof(CreditNote.ReturnReason)).AsString(500).Nullable()
            .WithColumn(nameof(CreditNote.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(CreditNote.ReturnRequestId)).AsInt32().Nullable()
            .WithColumn(nameof(CreditNote.CreatedByCustomerId)).AsInt32().Nullable()
            .WithColumn(nameof(CreditNote.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
