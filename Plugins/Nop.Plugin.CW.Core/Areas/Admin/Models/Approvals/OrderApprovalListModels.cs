using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

/// <summary>
/// The search of the orders that need or needed approval
/// </summary>
public record OrderApprovalSearchModel : BaseSearchModel
{
    [NopResourceDisplayName("Plugins.CW.Core.Admin.OrderApprovals.Search.Status")]
    public int SearchStatusId { get; set; }
    public IList<SelectListItem> AvailableStatuses { get; set; } = new List<SelectListItem>();

    [NopResourceDisplayName("Plugins.CW.Core.Admin.OrderApprovals.Search.Store")]
    public int SearchStoreId { get; set; }
    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();

    [NopResourceDisplayName("Plugins.CW.Core.Admin.OrderApprovals.Search.OrderNumber")]
    public string SearchOrderNumber { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.OrderApprovals.Search.Customer")]
    public string SearchCustomer { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.OrderApprovals.Search.Approver")]
    public int SearchApproverId { get; set; }
    public IList<SelectListItem> AvailableApprovers { get; set; } = new List<SelectListItem>();

    /// <summary>
    /// Whether the store is worth showing: there is more than one
    /// </summary>
    public bool ShowStores { get; set; }
}

/// <summary>
/// A page of the list of approvals
/// </summary>
public record OrderApprovalListModel : BasePagedListModel<OrderApprovalListItemModel>
{
    /// <summary>
    /// The approvers of every store, suggested while an address is typed; a row's dialog takes those of its store
    /// </summary>
    public IList<ApproverSuggestionModel> Suggestions { get; set; } = [];
}

/// <summary>
/// One order of the list of approvals; its id is the order's
/// </summary>
public record OrderApprovalListItemModel : BaseNopEntityModel
{
    public string OrderNumber { get; set; }

    public int StoreId { get; set; }

    public string StoreName { get; set; }

    public string Customer { get; set; }

    public string Branch { get; set; }

    public string CreatedOn { get; set; }

    public string Payable { get; set; }

    /// <summary>
    /// The subsidy the order uses, formatted; empty when none
    /// </summary>
    public string Subsidy { get; set; }

    /// <summary>
    /// The store credit the order uses, formatted; empty when none
    /// </summary>
    public string StoreCredit { get; set; }

    public string Status { get; set; }

    /// <summary>
    /// Who it waits for, with the reminders sent; who decided, when it is decided
    /// </summary>
    public string Approvers { get; set; }

    /// <summary>
    /// The addresses a waiting order waits for, as the box that changes them shows them
    /// </summary>
    public string WaitingEmails { get; set; }

    public bool IsPending { get; set; }
}

/// <summary>
/// The search of the approvers
/// </summary>
public record ApproverSearchModel : BaseSearchModel
{
    [NopResourceDisplayName("Plugins.CW.Core.Admin.Approvers.Search.Text")]
    public string SearchText { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Approvers.Search.Store")]
    public int SearchStoreId { get; set; }
    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();

    /// <summary>
    /// Whether the store is worth showing: there is more than one
    /// </summary>
    public bool ShowStores { get; set; }
}

/// <summary>
/// A page of the list of approvers
/// </summary>
public record ApproverListModel : BasePagedListModel<ApproverModel>
{
    /// <summary>
    /// The approvers of every store, suggested while an address is typed; a row's dialog takes those of its store
    /// </summary>
    public IList<ApproverSuggestionModel> Suggestions { get; set; } = [];
}

/// <summary>
/// One approver of the list
/// </summary>
public record ApproverModel : BaseNopEntityModel
{
    public string Name { get; set; }

    public string Email { get; set; }

    public int StoreId { get; set; }

    public string StoreName { get; set; }

    /// <summary>
    /// The branches and departments whose rule names the approver
    /// </summary>
    public string ApprovesFor { get; set; }

    public int Waiting { get; set; }

    public int Approved { get; set; }

    public int Declined { get; set; }

    /// <summary>
    /// Whether a rule names the approver, so it can be replaced
    /// </summary>
    public bool HasRules { get; set; }

    /// <summary>
    /// Whether no rule in force names the approver and no order waits for it
    /// </summary>
    public bool CanDelete { get; set; }
}
