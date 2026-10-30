namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// Bulk ordering domain constants
/// </summary>
public static partial class BulkOrderDefaults
{
    #region Grid

    /// <summary>
    /// Maximum number of attributes laid out on the grid. Axis 1 is the columns, 2..n the row grouping.
    /// Fixed rather than configurable: the admin screen renders exactly this many ordered slots, so a
    /// lower configured value could only contradict the UI.
    /// </summary>
    public const int MAX_AXIS_COUNT = 5;

    #endregion

    #region Product list

    /// <summary>Tiled (card) view mode. Matches 4.6's value so a bookmarked URL still works.</summary>
    public const string VIEW_MODE_TILED = "tiled";

    /// <summary>List view mode.</summary>
    public const string VIEW_MODE_LIST = "list";

    /// <summary>
    /// Most recent orders offered in the "from order" picker. A customer with years of history
    /// would otherwise get every one of them in a dropdown, loaded and rendered on every page view.
    /// </summary>
    public const int MAX_ORDER_PICKER_ITEMS = 50;

    /// <summary>
    /// How many individual page numbers the pager shows around the current page. Matches nop's own
    /// default (Pager.IndividualPagesDisplayedCount), so the control behaves the way the customer already
    /// expects from the catalogue.
    /// </summary>
    public const int PAGER_WINDOW = 5;

    #endregion
}