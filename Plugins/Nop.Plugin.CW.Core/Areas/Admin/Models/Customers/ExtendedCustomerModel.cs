using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Web.Areas.Admin.Models.Customers;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;

public record ExtendedCustomerModel : CustomerModel
{
    #region Ctor

    public ExtendedCustomerModel()
    {
        AvailableBranches = new List<SelectListItem>();
        PackNameModel = new PackNameModel();
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId")]
    public int BranchId { get; set; }

    public IList<SelectListItem> AvailableBranches { get; set; }

    public string Branch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Customers.Customer.Fields.RegisteredInBranch")]
    public string RegisteredInBranch { get; set; }

    public bool HasPackName { get; set; }

    public PackNameModel PackNameModel { get; set; }

    #region Credit notes visibility

    /// <summary>
    /// Gets or sets a value indicating whether the credit notes card is shown. True when credit
    /// notes are enabled for the customer's store, or the customer already has credit note records.
    /// A credit note is a liability, so it stays visible after the feature is switched off.
    /// </summary>
    public bool DisplayCreditNotes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether credit notes are enabled for the customer's store.
    /// False while <see cref="DisplayCreditNotes"/> is true means: show existing records read-only.
    /// </summary>
    public bool CreditNotesEnabled { get; set; }

    #endregion

    #endregion
}