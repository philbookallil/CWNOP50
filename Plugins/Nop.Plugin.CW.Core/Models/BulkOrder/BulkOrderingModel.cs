using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.BulkOrder;

public record BulkOrderingModel : BaseNopModel
{
    #region Ctor

    public BulkOrderingModel()
    {
        Items = new List<BulkOrderProductModel>();
        AvailableFilters = new List<SelectListItem>();
        AvailableOrders = new List<SelectListItem>();
        AvailableSortOptions = new List<SelectListItem>();
        AvailablePageSizes = new List<SelectListItem>();
        AvailableViewModes = new List<SelectListItem>();
    }

    #endregion

    #region Properties

    public string FilterMode { get; set; }

    public int OrderId { get; set; }

    public int CategoryId { get; set; }

    public int OrderBy { get; set; }

    public string ViewMode { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    /// <summary>
    /// The individual page numbers to render, windowed around the current page. Empty when there is only
    /// one page. Computed in the factory rather than the view: it is arithmetic with three edge cases, and
    /// a view is the wrong place to get them wrong.
    /// </summary>
    public IList<int> PageNumbers { get; set; } = new List<int>();

    public bool ShowImages { get; set; }

    public string ImageSize { get; set; }

    public IList<BulkOrderProductModel> Items { get; set; }

    public IList<SelectListItem> AvailableFilters { get; set; }

    public IList<SelectListItem> AvailableOrders { get; set; }

    public IList<SelectListItem> AvailableSortOptions { get; set; }

    public IList<SelectListItem> AvailablePageSizes { get; set; }

    public IList<SelectListItem> AvailableViewModes { get; set; }

    #endregion
}