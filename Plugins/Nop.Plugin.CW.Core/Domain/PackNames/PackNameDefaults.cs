namespace Nop.Plugin.CW.Core.Domain.PackNames;

public static partial class PackNameDefaults
{
    #region PackName attributes

    /// <summary>
    /// Gets a name of generic attribute to store the value of 'PackNameId'
    /// </summary>
    public static string SelectedPackNameIdAttribute => "SelectedPackNameId";

    /// <summary>
    /// Gets a name of generic attribute to store the value of 'PackNameListId'
    /// </summary>
    public static string SelectedPackNameListIdAttribute => "SelectedPackNameListId";

    /// <summary>
    /// Single consolidated generic attribute that stores all packname shipping assignments (address,
    /// selected option, offered options) as JSON. Replaces the per-packname individual attributes.
    /// </summary>
    public static string PackNameShippingAssignmentsAttribute => "CW.PackName.ShippingAssignments";

    #endregion
}
