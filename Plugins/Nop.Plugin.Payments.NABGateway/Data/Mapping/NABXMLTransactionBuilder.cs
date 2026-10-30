using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Payments.NABGateway.Domain;

namespace Nop.Plugin.Payments.NABGateway.Data.Mapping;

/// <summary>
/// Mapping for the <see cref="NABXMLTransaction"/> entity.
/// </summary>
public class NABXMLTransactionBuilder : NopEntityBuilder<NABXMLTransaction>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(NABXMLTransaction.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(NABXMLTransaction.OrderGuid)).AsGuid().NotNullable()
            .WithColumn(nameof(NABXMLTransaction.CustomerId)).AsInt32().NotNullable()
            .WithColumn(nameof(NABXMLTransaction.UserEmail)).AsString(400).Nullable()
            .WithColumn(nameof(NABXMLTransaction.UserFullName)).AsString(400).Nullable()
            .WithColumn(nameof(NABXMLTransaction.Approved)).AsBoolean().WithDefaultValue(false).NotNullable()
            .WithColumn(nameof(NABXMLTransaction.MessageId)).AsString(100).Nullable()
            .WithColumn(nameof(NABXMLTransaction.TxnId)).AsString(100).Nullable()
            .WithColumn(nameof(NABXMLTransaction.AuthId)).AsString(100).Nullable()
            .WithColumn(nameof(NABXMLTransaction.ResponseCode)).AsString(50).Nullable()
            .WithColumn(nameof(NABXMLTransaction.ResponseText)).AsString(1000).Nullable()
            .WithColumn(nameof(NABXMLTransaction.SettlementDate)).AsString(50).Nullable()
            .WithColumn(nameof(NABXMLTransaction.MaskedPan)).AsString(50).Nullable()
            .WithColumn(nameof(NABXMLTransaction.CardType)).AsString(100).Nullable()
            .WithColumn(nameof(NABXMLTransaction.Amount)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(NABXMLTransaction.CurrencyCode)).AsString(10).Nullable()
            .WithColumn(nameof(NABXMLTransaction.PurchaseOrderNo)).AsString(100).Nullable()
            .WithColumn(nameof(NABXMLTransaction.RawResponseXml)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(NABXMLTransaction.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
