using Nop.Plugin.CW.Core.Domain.BulkOrder;

namespace Nop.Plugin.CW.Core.Models.BulkOrder;

public record BulkOrderCommand
{
    public string FilterMode { get; set; }

    public int OrderId { get; set; }

    public int CategoryId { get; set; }

    public int? OrderBy { get; set; }

    public string ViewMode { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>
    /// Pin selections for the products on this page, as "productId:mappingId:valueId" triples separated
    /// by commas. Empty on a first load - every pin then takes its default.
    /// </summary>
    public string Pins { get; set; }

    /// <summary>
    /// Parses <see cref="Pins"/> into productId -> (mappingId -> valueId). Unparseable triples are
    /// dropped rather than throwing: this arrives on the query string.
    /// </summary>
    public IDictionary<int, IDictionary<int, IList<int>>> GetPinSelections()
    {
        var result = new Dictionary<int, IDictionary<int, IList<int>>>();

        foreach (var triple in (Pins ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = triple.Split(':');

            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var productId) ||
                !int.TryParse(parts[1], out var mappingId) ||
                !int.TryParse(parts[2], out var valueId))
                continue;

            if (!result.TryGetValue(productId, out var forProduct))
                result[productId] = forProduct = new Dictionary<int, IList<int>>();

            if (!forProduct.TryGetValue(mappingId, out var values))
                forProduct[mappingId] = values = new List<int>();

            //valueId 0 is the "cleared" sentinel: the entry exists but holds nothing, which the builder
            //must treat as an explicit empty selection rather than as "no opinion, use the default"
            if (valueId > 0)
                values.Add(valueId);
        }

        return result;
    }

    /// <summary>
    /// Parses FilterMode into the domain enum. Unrecognised or missing values fall back to
    /// AllProducts rather than producing an empty list — see spec trap 3.
    /// </summary>
    public BulkOrderFilterType GetFilterType()
        => FilterMode.ToBulkOrderFilterType();

    /// <summary>Parses OrderBy, defaulting to Position. Never throws on a null or unknown value.</summary>
    public BulkOrderSorting GetSorting()
        => OrderBy.HasValue && Enum.IsDefined(typeof(BulkOrderSorting), OrderBy.Value)
            ? (BulkOrderSorting)OrderBy.Value
            : BulkOrderSorting.Position;
}