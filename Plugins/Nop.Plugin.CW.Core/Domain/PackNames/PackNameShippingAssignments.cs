using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.CW.Core.Domain.PackNames;

/// <summary>
/// Holds all <see cref="PackNameShippingAssignment"/> records for a customer's current checkout session,
/// keyed by PackName Id. Use the typed accessor methods rather than accessing <see cref="All"/> directly.
/// </summary>
public class PackNameShippingAssignments
{
    #region Fields

    private readonly Dictionary<int, PackNameShippingAssignment> _data;

    #endregion

    #region Ctor

    public PackNameShippingAssignments() : this(new Dictionary<int, PackNameShippingAssignment>())
    {
    }

    public PackNameShippingAssignments(Dictionary<int, PackNameShippingAssignment> data)
    {
        _data = data ?? new Dictionary<int, PackNameShippingAssignment>();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Raw backing dictionary — used for serialisation only.
    /// Prefer the typed accessor methods for all other access.
    /// </summary>
    public IReadOnlyDictionary<int, PackNameShippingAssignment> All => _data;

    #endregion

    #region Utilities

    /// <summary>Returns the existing assignment for a packname, or creates and registers an empty one.</summary>
    private PackNameShippingAssignment GetOrCreate(int packNameId)
    {
        if (!_data.TryGetValue(packNameId, out var assignment))
        {
            assignment = new PackNameShippingAssignment();
            _data[packNameId] = assignment;
        }
        return assignment;
    }

    #endregion

    #region Methods — Address

    /// <summary>Returns the selected address Id for a packname, or null if none is stored.</summary>
    public int? GetAddressId(int packNameId)
        => _data.TryGetValue(packNameId, out var a) ? a.AddressId : null;

    /// <summary>Sets the selected address Id for a packname.</summary>
    public void SetAddressId(int packNameId, int? addressId)
        => GetOrCreate(packNameId).AddressId = addressId;

    #endregion

    #region Methods — Shipping Options

    /// <summary>Returns the selected shipping option for a packname, or null if none is stored.</summary>
    public ShippingOption GetSelectedOption(int packNameId)
        => _data.TryGetValue(packNameId, out var a) ? a.SelectedShippingOption : null;

    /// <summary>Returns the offered shipping options for a packname, or an empty list if none are stored.</summary>
    public List<ShippingOption> GetOfferedOptions(int packNameId)
        => _data.TryGetValue(packNameId, out var a) ? a.OfferedShippingOptions : new List<ShippingOption>();

    /// <summary>Returns what the offered options of a packname were rated for, or null if that is not known.</summary>
    public string GetRatedFor(int packNameId)
        => _data.TryGetValue(packNameId, out var a) ? a.RatedFor : null;

    /// <summary>Replaces the shipping options for a packname (both offered list and selected method).</summary>
    /// <param name="ratedFor">What the options were rated for; null when there are none</param>
    public void SetShippingOptions(int packNameId, List<ShippingOption> offeredOptions, ShippingOption selectedOption, string ratedFor = null)
    {
        var assignment = GetOrCreate(packNameId);
        assignment.OfferedShippingOptions = offeredOptions ?? new List<ShippingOption>();
        assignment.SelectedShippingOption = selectedOption;
        assignment.RatedFor = ratedFor;
    }

    /// <summary>Updates only the selected shipping option, preserving the offered options list.</summary>
    public void SetSelectedOption(int packNameId, ShippingOption selectedOption)
        => GetOrCreate(packNameId).SelectedShippingOption = selectedOption;

    #endregion

    #region Methods — Lifecycle

    /// <summary>Removes all assignment data for a specific packname (e.g. when it leaves the cart).</summary>
    public void ClearPackName(int packNameId)
        => _data.Remove(packNameId);

    /// <summary>Returns true if no assignment data is stored for any packname.</summary>
    public bool IsEmpty => _data.Count == 0;

    #endregion
}
