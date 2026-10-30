using Nop.Core.Configuration;

namespace Nop.Plugin.CW.Core.Domain.Settings;

public class MultiStoreSettings : ISettings
{
    #region Bulk Order

    /// <summary>Master switch. Backs CWFeature.BulkOrder.</summary>
    public bool BulkOrderEnable { get; set; }

    /// <summary>
    /// Ordered ProductAttribute ids expressing axis preference. Position 0 becomes the grid columns,
    /// 1..n the row grouping. A product attribute absent from this list is still eligible for a free
    /// axis slot; a listed attribute the product does not have is skipped.
    /// </summary>
    public List<int> BulkOrderAxisAttributeIds { get; set; } = new();

    /// <summary>Grid cells (rows x columns) rendered per product before an axis is demoted to pinned.</summary>
    public int BulkOrderMaxCellsPerProduct { get; set; } = 400;

    /// <summary>0 = tiled, 1 = list.</summary>
    public int BulkOrderDefaultViewMode { get; set; }

    public bool BulkOrderShowProductImages { get; set; } = true;

    public int BulkOrderProductImageSize { get; set; } = 300;

    public bool BulkOrderShowAttributeImages { get; set; }

    public int BulkOrderAttributeImageSize { get; set; } = 100;

    /// <summary>0 falls back to MediaSettings.ImageSquarePictureSize.</summary>
    public int BulkOrderAttributeImageSquaresSize { get; set; }

    public bool BulkOrderHideQtyArrows { get; set; }

    public bool BulkOrderShowAttributePrices { get; set; }

    public bool BulkOrderShowProductStock { get; set; } = true;

    public int BulkOrderDefaultPageSize { get; set; } = 15;

    public string BulkOrderPageSizeOptions { get; set; } = "15,30,50,100";

    /// <summary>Redirect a permitted customer to the bulk ordering page after login.</summary>
    public bool BulkOrderSendUserAfterLogin { get; set; }

    /// <summary>
    /// Store default for the bulk grid on the product details page. Overridable per product by the
    /// per-product GenericAttribute override.
    /// </summary>
    public bool BulkOrderEnableOnProductPage { get; set; }

    #endregion

    #region PackNames

    public bool PackNameEnable { get; set; }

    public bool UseStaffAsPackName { get; set; }

    public bool UseBranchesAsPackNameList { get; set; }

    public int PackNameListNameFrom { get; set; }

    public bool EmployeeNumberEnabled { get; set; }

    public bool PrintNameEnabled { get; set; }

    public int PrintNameProductAttributeId { get; set; }

    public bool POEntryEnabled { get; set; }

    public bool CommentsEnabled { get; set; }

    public bool GenderEnabled { get; set; }

    public bool ShowDepartmentPackNamesByBranch { get; set; }

    public bool AllowManagersToAddPackNames { get; set; }

    public bool AllowToAddCustomerToPackName { get; set; }

    public bool AllowSubsidySelection { get; set; }

    public bool AllowEditSubsidyTopup { get; set; }

    public bool AllowToAddByExcel { get; set; }

    public int AllowSubsidyCountByExcel { get; set; }

    public bool LimitAssignedBranches { get; set; }

    public bool PackNamesOwnGlobalValue { get; set; }

    public bool PackNamesOwnStartAndFinishDates { get; set; }

    public string ShippingMethod { get; set; }

    public bool AllowHomeDelivery { get; set; }

    public bool AllowCustomersToAddAddress { get; set; }

    public bool DoNotPreSelectShippingAddress { get; set; }

    public bool IncludeShippingOnSubsidy { get; set; }

    /// <summary>
    /// Send a customer whose selected packname has subsidy product groups to the subsidy catalogue after login,
    /// after selecting a packname and after being impersonated.
    /// </summary>
    public bool SubsidySendUserAfterLogin { get; set; }

    public List<int> DefaultPackNameCustomerRoleIds { get; set; } = new();

    #endregion

    #region CreditNotes && Exchanges

    public bool CreditNotesEnable { get; set; }

    public bool ExchangesEnable { get; set; }

    /// <summary>
    /// When enabled, customers may dismiss the return confirmation without printing the return note.
    /// Off by default: the printed note is expected in the box with the garments.
    /// </summary>
    public bool AllowCloseWithoutPrintingReturnNote { get; set; }

    #endregion

    #region Subsidy

    public bool SubsidyEnable { get; set; }

    #endregion

    #region Payment

    /// <summary>
    /// System names of the payment methods that take the payment in their own pages and place the order
    /// themselves, through IOrderProcessingService.PlaceOrderAsync. The packname checkout never places an order
    /// with one of them: its Checkout button validates the packname order and hands over to the method's own
    /// payment step. A method left out of the list would have its order placed unpaid.
    /// </summary>
    public List<string> OwnPlacementPaymentMethods { get; set; } = ["Payments.PayPalCommerce"];

    #endregion
}