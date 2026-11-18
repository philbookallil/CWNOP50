using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// One line of an Odoo sale order, as the send builds it.
/// </summary>
public class OdooSaleOrderLineDraft
{
    /// <summary>The Odoo product.product id.</summary>
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The unit price excl. tax; Odoo adds its own taxes.</summary>
    public decimal PriceUnit { get; set; }

    /// <summary>The line's description.</summary>
    public string Name { get; set; }

    /// <summary>Whether this is the shipping line.</summary>
    public bool IsDelivery { get; set; }
}

/// <summary>
/// One Odoo sale order, built from one destination of a nop order.
/// </summary>
public class OdooSaleOrderDraft
{
    /// <summary>The destination key (0 on a plain order).</summary>
    public int DestinationKey { get; set; }

    /// <summary>The Odoo order's origin, which finds it again on a retry.</summary>
    public string Origin { get; set; }

    /// <summary>The Odoo company: customer and invoice address.</summary>
    public int CustomerPartnerId { get; set; }

    /// <summary>The Odoo delivery address, resolved just before the order is created.</summary>
    public int ShippingPartnerId { get; set; }

    /// <summary>The nop address the destination ships to; null for a pickup.</summary>
    public int? ShippingAddressId { get; set; }

    public DateTime OrderDateUtc { get; set; }

    /// <summary>The PO number, when every line of the order has the same one.</summary>
    public string ClientOrderRef { get; set; }

    public IList<string> NoteLines { get; } = new List<string>();

    public IList<OdooSaleOrderLineDraft> Lines { get; } = new List<OdooSaleOrderLineDraft>();

    /// <summary>
    /// What nop charged for this destination before tax: its lines at their own totals, its shipping and its
    /// fees. Odoo's total is compared with it.
    /// </summary>
    public decimal NopUntaxed { get; set; }

    /// <summary>Why the order cannot be sent; null when it can.</summary>
    public string HoldReason { get; set; }
}

/// <summary>
/// Turns a draft into what Odoo's JSON-2 API takes, and composes its texts. Pure: no nop or Odoo calls.
/// </summary>
internal static class OdooOrderBuilder
{
    private static readonly Regex _lineBreak = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _tag = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// The Odoo order's origin: "nop {order number}", plus " / {destination key}" when the nop order has several
    /// destinations, so each of its Odoo orders has its own.
    /// </summary>
    public static string Origin(string customOrderNumber, int destinationKey, bool severalDestinations) =>
        severalDestinations ? $"nop {customOrderNumber} / {destinationKey}" : $"nop {customOrderNumber}";

    /// <summary>
    /// nop's HTML attribute description as plain text: one line per &lt;br /&gt;, tags dropped, entities decoded,
    /// empty lines removed.
    /// </summary>
    public static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = WebUtility.HtmlDecode(_tag.Replace(_lineBreak.Replace(html, "\n"), string.Empty));

        return string.Join("\n", text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
    }

    /// <summary>
    /// "For: … · Branch: … · Dept: … · CC: …", leaving out the empty parts; empty when all are.
    /// </summary>
    public static string PackNameInfo(string packName, string branch, string department, string costCenter)
    {
        var parts = new List<string>();

        void Add(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{label}: {value.Trim()}");
        }

        Add("For", packName);
        Add("Branch", branch);
        Add("Dept", department);
        Add("CC", costCenter);

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// A line's description, one item per text line: product name, its attributes, the packname line, the PO.
    /// </summary>
    public static string LineName(string productName, string attributeDescriptionHtml, string packNameInfo, string purchaseOrder)
    {
        var lines = new List<string> { productName?.Trim() ?? string.Empty };

        var attributes = HtmlToText(attributeDescriptionHtml);
        if (attributes.Length > 0)
            lines.Add(attributes);

        if (!string.IsNullOrWhiteSpace(packNameInfo))
            lines.Add(packNameInfo);

        if (!string.IsNullOrWhiteSpace(purchaseOrder))
            lines.Add($"PO: {purchaseOrder.Trim()}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// The order's PO number when every line has the same one; otherwise null, and each line carries its own.
    /// </summary>
    public static string CommonPurchaseOrder(IReadOnlyCollection<string> linePurchaseOrders)
    {
        if (linePurchaseOrders.Count == 0 || linePurchaseOrders.Any(string.IsNullOrWhiteSpace))
            return null;

        var distinct = linePurchaseOrders.Select(po => po.Trim()).Distinct(StringComparer.Ordinal).ToList();

        return distinct.Count == 1 ? distinct[0] : null;
    }

    /// <summary>
    /// A line's unit price excl. tax: nop's line total over its quantity, to cents (Odoo's price precision).
    /// </summary>
    public static decimal UnitPrice(decimal lineTotalExclTax, int quantity) =>
        quantity > 0 ? Math.Round(lineTotalExclTax / quantity, 2, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>
    /// The note's HTML: each text line encoded, one paragraph each; empty when there is none.
    /// </summary>
    public static string NoteHtml(IEnumerable<string> lines) =>
        string.Concat(lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => $"<p>{WebUtility.HtmlEncode(line)}</p>"));

    /// <summary>
    /// The values of <c>sale.order/create</c>: the three partners, origin, date and the lines as one2many create
    /// commands, so the order and its lines go in one call.
    /// </summary>
    public static Dictionary<string, object> CreateValues(OdooSaleOrderDraft draft)
    {
        var values = new Dictionary<string, object>
        {
            ["partner_id"] = draft.CustomerPartnerId,
            ["partner_invoice_id"] = draft.CustomerPartnerId,
            ["partner_shipping_id"] = draft.ShippingPartnerId,
            ["origin"] = draft.Origin,
            ["date_order"] = draft.OrderDateUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["order_line"] = draft.Lines.Select((line, index) => (object)new object[] { 0, 0, LineValues(line, index) }).ToList()
        };

        if (!string.IsNullOrWhiteSpace(draft.ClientOrderRef))
            values["client_order_ref"] = draft.ClientOrderRef;

        var note = NoteHtml(draft.NoteLines);
        if (note.Length > 0)
            values["note"] = note;

        return values;
    }

    private static Dictionary<string, object> LineValues(OdooSaleOrderLineDraft line, int index)
    {
        var values = new Dictionary<string, object>
        {
            ["sequence"] = (index + 1) * 10,
            ["product_id"] = line.ProductId,
            ["product_uom_qty"] = line.Quantity,
            ["price_unit"] = line.PriceUnit,
            ["name"] = line.Name
        };

        if (line.IsDelivery)
            values["is_delivery"] = true;

        return values;
    }
}
