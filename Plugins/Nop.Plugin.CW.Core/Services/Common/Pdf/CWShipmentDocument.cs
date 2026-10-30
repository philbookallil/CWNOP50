using Nop.Services.Common.Pdf;
using PdfRpt.FluentInterface;
using System.Linq.Expressions;

namespace Nop.Plugin.CW.Core.Services.Common.Pdf;

/// <summary>
/// A packing slip that prints, under each product, the lines nop prints under it on the invoice: the product's
/// attributes and who the line is for.
/// </summary>
/// <remarks>
/// nop's <see cref="ShipmentDocument"/> configures its product-name column without attribute lines, so the
/// size or colour of what is being packed, and the packname it is for, never reached the slip. Only
/// <see cref="ConfigureProductColumn"/> is overridden — it is <c>protected virtual</c> and called from inside
/// <c>Generate</c> — so no rendering code is copied.
/// </remarks>
public class CWShipmentDocument : ShipmentDocument
{
    #region Utilities

    /// <summary>
    /// Whether the column shows the product name.
    /// </summary>
    protected static bool IsNameColumn(Expression<Func<ProductItem, object>> propertyExpression)
    {
        var body = propertyExpression?.Body is UnaryExpression unary ? unary.Operand : propertyExpression?.Body;

        return body is MemberExpression member && member.Member.Name == nameof(ProductItem.Name);
    }

    /// <summary>
    /// nop's column, with the attribute lines printed under the product name.
    /// </summary>
    protected override void ConfigureProductColumn(ColumnAttributesBuilder column, Expression<Func<ProductItem, object>> propertyExpression, int width = 1, bool printProductAttributes = false)
    {
        base.ConfigureProductColumn(column, propertyExpression, width, printProductAttributes || IsNameColumn(propertyExpression));
    }

    #endregion
}