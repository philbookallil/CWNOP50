using Microsoft.AspNetCore.Http;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

/// <summary>
/// A customer's wizard submission, parsed out of the posted form. The single place that knows the
/// wizard's field names - nothing else should read the form directly.
/// </summary>
public record ReturnWizardSubmission
{
    #region Constants

    private const string MODE_FIELD = "exchange_mode";
    private const string QUANTITY_PREFIX = "returned_item_qty_";
    private const string EXCHANGE_TYPE_PREFIX = "exchange_mode_";
    private const string REPLACEMENT_PREFIX = "replacement_id_";
    private const string ATTRIBUTE_PREFIX = "attr_id_";

    /// <summary>Quantity field used by the nop return request page that feeds the wizard</summary>
    private const string RETURN_PAGE_QUANTITY_PREFIX = "quantity";

    #endregion

    #region Properties

    /// <summary>Null when not supplied or not a recognised mode</summary>
    public ExchangeReturnMode? Mode { get; init; }

    public IList<ReturnWizardLine> Lines { get; init; } = new List<ReturnWizardLine>();

    #endregion

    #region Utilities

    // attr_id_{orderItemId}_{productId}_{attributeId}
    private static void ReadAttribute(string key, string value, Func<int, ReturnWizardLine> lineFor)
    {
        var parts = key[ATTRIBUTE_PREFIX.Length..].Split('_');

        if (parts.Length != 3
            || !int.TryParse(parts[0], out var orderItemId)
            || !int.TryParse(parts[2], out var attributeId)
            || !int.TryParse(value, out var attributeValueId)
            || attributeValueId <= 0)
            return;

        lineFor(orderItemId).ReplacementAttributes[attributeId] = attributeValueId;
    }

    private static bool TryReadId(string key, string prefix, out int id)
    {
        id = 0;

        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key.AsSpan(prefix.Length), out id);
    }

    private static int ToInt(string value) => int.TryParse(value, out var parsed) ? parsed : 0;

    private static int? ToNullableInt(string value)
        => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    private static TEnum? ToEnum<TEnum>(string value) where TEnum : struct, Enum
        => int.TryParse(value, out var parsed) && Enum.IsDefined(typeof(TEnum), parsed)
            ? (TEnum)(object)parsed
            : null;

    #endregion

    #region Methods

    /// <summary>Parses a post from the wizard's gather step</summary>
    public static ReturnWizardSubmission FromGatherForm(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var lines = new Dictionary<int, ReturnWizardLine>();

        ReturnWizardLine LineFor(int orderItemId)
        {
            if (!lines.TryGetValue(orderItemId, out var line))
                lines[orderItemId] = line = new ReturnWizardLine { OrderItemId = orderItemId };

            return line;
        }

        foreach (var key in form.Keys)
        {
            // Checked before the EXCHANGE_TYPE_PREFIX branch - "exchange_mode" is a prefix of
            // "exchange_mode_{id}", so order matters here
            if (key.Equals(MODE_FIELD, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryReadId(key, QUANTITY_PREFIX, out var qtyItemId))
                LineFor(qtyItemId).Quantity = ToInt(form[key]);
            else if (TryReadId(key, EXCHANGE_TYPE_PREFIX, out var typeItemId))
                LineFor(typeItemId).ExchangeType = ToEnum<ExchangeType>(form[key]);
            else if (TryReadId(key, REPLACEMENT_PREFIX, out var replItemId))
                LineFor(replItemId).ReplacementProductId = ToNullableInt(form[key]);
            else if (key.StartsWith(ATTRIBUTE_PREFIX, StringComparison.OrdinalIgnoreCase))
                ReadAttribute(key, form[key], LineFor);
        }

        return new ReturnWizardSubmission
        {
            Mode = ToEnum<ExchangeReturnMode>(form[MODE_FIELD]),
            Lines = lines.Values.OrderBy(l => l.OrderItemId).ToList()
        };
    }

    /// <summary>Parses the initial post from the nop return request page, which carries quantities only</summary>
    public static ReturnWizardSubmission FromReturnPageForm(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var lines = new List<ReturnWizardLine>();

        foreach (var key in form.Keys)
            if (TryReadId(key, RETURN_PAGE_QUANTITY_PREFIX, out var orderItemId))
            {
                var quantity = ToInt(form[key]);
                if (quantity > 0)
                    lines.Add(new ReturnWizardLine { OrderItemId = orderItemId, Quantity = quantity });
            }

        return new ReturnWizardSubmission { Lines = lines };
    }

    /// <summary>The line for an order item, or null when the customer is not returning it</summary>
    public ReturnWizardLine LineFor(int orderItemId)
        => Lines.FirstOrDefault(l => l.OrderItemId == orderItemId);

    #endregion
}

/// <summary>One returned order item and what the customer wants done with it</summary>
public record ReturnWizardLine
{
    public int OrderItemId { get; init; }

    public int Quantity { get; set; }

    /// <summary>Null when the customer has not chosen yet</summary>
    public ExchangeType? ExchangeType { get; set; }

    /// <summary>Null for an exact exchange or a credit note</summary>
    public int? ReplacementProductId { get; set; }

    /// <summary>Attribute id -> chosen value id</summary>
    public IDictionary<int, int> ReplacementAttributes { get; init; } = new Dictionary<int, int>();

    public bool NeedsReplacement
        => ExchangeType == Domain.Exchanges.ExchangeType.SameStyle
        || ExchangeType == Domain.Exchanges.ExchangeType.DifferentStyle;
}