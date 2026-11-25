using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the admin lists of the order approvals: the orders, and the approvers
/// </summary>
/// <remarks>
/// Neither list is gated on the Approvals switch: an order already held still has to be decided. Amounts are in the
/// primary store currency, as the orders keep them.
/// </remarks>
public class OrderApprovalListModelFactory : IOrderApprovalListModelFactory
{
    #region Fields

    protected readonly CurrencySettings _currencySettings;
    protected readonly IApproverService _approverService;
    protected readonly IBranchService _branchService;
    protected readonly ICurrencyService _currencyService;
    protected readonly ICustomerService _customerService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IOrderApprovalReportService _orderApprovalReportService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IStoreService _storeService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalListModelFactory(CurrencySettings currencySettings,
        IApproverService approverService,
        IBranchService branchService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        ICWOrderService cwOrderService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IMultiStoreService multiStoreService,
        IOrderApprovalReportService orderApprovalReportService,
        IOrderApprovalService orderApprovalService,
        IPriceFormatter priceFormatter,
        IStoreService storeService,
        IWorkContext workContext)
    {
        _currencySettings = currencySettings;
        _approverService = approverService;
        _branchService = branchService;
        _currencyService = currencyService;
        _customerService = customerService;
        _cwOrderService = cwOrderService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _multiStoreService = multiStoreService;
        _orderApprovalReportService = orderApprovalReportService;
        _orderApprovalService = orderApprovalService;
        _priceFormatter = priceFormatter;
        _storeService = storeService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// An amount of the primary store currency, without the tax suffix
    /// </summary>
    protected virtual async Task<string> FormatAsync(decimal amount, Currency currency)
        => await _priceFormatter.FormatPriceAsync(amount, true, currency, (await _workContext.GetWorkingLanguageAsync()).Id, true, false);

    /// <summary>
    /// An amount of the primary store currency; empty when there is none
    /// </summary>
    protected virtual async Task<string> FormatOrEmptyAsync(decimal amount, Currency currency)
        => amount == decimal.Zero ? string.Empty : await FormatAsync(amount, currency);

    /// <summary>
    /// The choice of a store, "All" first
    /// </summary>
    protected virtual async Task<IList<SelectListItem>> PrepareStoresAsync()
    {
        var items = new List<SelectListItem> { new(await _localizationService.GetResourceAsync("Admin.Common.All"), "0") };
        items.AddRange((await _storeService.GetAllStoresAsync()).Select(store => new SelectListItem(store.Name, store.Id.ToString())));

        return items;
    }

    /// <summary>
    /// Who an order waits for, with the reminders sent; who decided, when it is decided
    /// </summary>
    protected virtual async Task<string> GetApproversTextAsync(OrderApproval approval)
    {
        if (approval.Status != OrderApprovalStatus.Pending)
            return approval.DecidedBy;

        var names = await _orderApprovalService.GetWaitingApproverNamesAsync(approval.Id);

        var text = names.Count == 0
            ? await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.OrderApprovals.Nobody")
            : string.Join(", ", names);

        return approval.LastReminderNumber switch
        {
            0 => text,
            1 => $"{text} ({await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.OrderApprovals.Reminders.One")})",
            var number => $"{text} ({string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.OrderApprovals.Reminders.Many"), number)})"
        };
    }

    /// <summary>
    /// The branch or department of a rule, with the amount it starts at or that it is off; null when it is gone
    /// </summary>
    protected virtual async Task<string> GetRuleTextAsync(ApprovalRule rule, Currency currency)
    {
        string name = null;

        if (rule.ScopeType == ApprovalScopeType.Branch)
        {
            var branch = await _branchService.GetBranchByIdAsync(rule.ScopeId);
            name = branch == null || branch.Deleted ? null : branch.Name;
        }
        else if (rule.ScopeType == ApprovalScopeType.Department)
        {
            var department = await _multiStoreService.GetDepartmentByIdAsync(rule.ScopeId);
            name = department == null || department.Deleted ? null : department.Name;
        }

        if (string.IsNullOrEmpty(name))
            return null;

        if (!rule.Active)
            return string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Approvers.ApprovesFor.Off"), name);

        return rule.Threshold > decimal.Zero
            ? string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Approvers.ApprovesFor.Over"), name,
                await FormatAsync(rule.Threshold.Value, currency))
            : name;
    }

    /// <summary>
    /// One order of the list
    /// </summary>
    protected virtual async Task<OrderApprovalListItemModel> PrepareOrderApprovalListItemModelAsync(OrderApprovalLineDTO line, Currency currency,
        IDictionary<int, string> storeNames)
    {
        var customer = await _customerService.GetCustomerByIdAsync(line.Order.CustomerId);
        var customerName = customer == null ? string.Empty : await _customerService.GetCustomerFullNameAsync(customer);
        var settlement = await _cwOrderService.GetOrderSettlementAsync(line.Order.Id);
        var isPending = line.Approval.Status == OrderApprovalStatus.Pending;

        return new OrderApprovalListItemModel
        {
            Id = line.Order.Id,
            OrderNumber = line.Order.CustomOrderNumber,
            StoreId = line.Order.StoreId,
            StoreName = storeNames.TryGetValue(line.Order.StoreId, out var storeName) ? storeName : string.Empty,
            Customer = string.IsNullOrEmpty(customerName) ? customer?.Email : customerName,
            Branch = (await _branchService.GetBranchByCustomerIdAsync(line.Order.CustomerId))?.Name,
            CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(line.Order.CreatedOnUtc, DateTimeKind.Utc)).ToString("g"),
            Payable = await FormatAsync(line.Order.OrderTotal, currency),
            Subsidy = await FormatOrEmptyAsync((settlement?.SubsidyStockAmount ?? decimal.Zero) + (settlement?.SubsidyShippingAmount ?? decimal.Zero), currency),
            StoreCredit = await FormatOrEmptyAsync(settlement?.CreditNoteAmount ?? decimal.Zero, currency),
            Status = await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{line.Approval.Status}"),
            Approvers = await GetApproversTextAsync(line.Approval),
            WaitingEmails = isPending ? OrderApprovalEmailHelper.Format(await _orderApprovalService.GetWaitingEmailsAsync(line.Approval.Id)) : string.Empty,
            IsPending = isPending
        };
    }

    #endregion

    #region Orders

    public virtual async Task<OrderApprovalSearchModel> PrepareOrderApprovalSearchModelAsync(OrderApprovalSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.SearchStatusId = (int)OrderApprovalStatus.Pending;
        searchModel.AvailableStatuses.Add(new SelectListItem(await _localizationService.GetResourceAsync("Admin.Common.All"), "0"));

        foreach (var status in Enum.GetValues<OrderApprovalStatus>())
        {
            searchModel.AvailableStatuses.Add(new SelectListItem(
                await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{status}"), ((int)status).ToString()));
        }

        searchModel.AvailableStores = await PrepareStoresAsync();
        searchModel.ShowStores = searchModel.AvailableStores.Count > 2;

        searchModel.AvailableApprovers.Add(new SelectListItem(await _localizationService.GetResourceAsync("Admin.Common.All"), "0"));

        foreach (var approver in await _approverService.SearchApproversAsync())
        {
            searchModel.AvailableApprovers.Add(new SelectListItem(
                OrderApprovalEmailHelper.NameAndEmail(approver.Name, approver.Email), approver.Id.ToString()));
        }

        searchModel.SetGridPageSize();

        return searchModel;
    }

    public virtual async Task<OrderApprovalListModel> PrepareOrderApprovalListModelAsync(OrderApprovalSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        OrderApprovalStatus? status = Enum.IsDefined((OrderApprovalStatus)searchModel.SearchStatusId)
            ? (OrderApprovalStatus)searchModel.SearchStatusId
            : null;

        var lines = await _orderApprovalReportService.SearchApprovalsAsync(status, searchModel.SearchStoreId, searchModel.SearchOrderNumber,
            searchModel.SearchCustomer, searchModel.SearchApproverId, searchModel.Page - 1, searchModel.PageSize);

        var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        var storeNames = (await _storeService.GetAllStoresAsync()).ToDictionary(store => store.Id, store => store.Name);

        var suggestions = ApproverSuggestionModel.From(await _approverService.SearchApproversAsync());

        return await new OrderApprovalListModel { Suggestions = suggestions }.PrepareToGridAsync(searchModel, lines, () =>
        {
            return lines.SelectAwait(async line => await PrepareOrderApprovalListItemModelAsync(line, currency, storeNames));
        });
    }

    #endregion

    #region Approvers

    public virtual async Task<ApproverSearchModel> PrepareApproverSearchModelAsync(ApproverSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.AvailableStores = await PrepareStoresAsync();
        searchModel.ShowStores = searchModel.AvailableStores.Count > 2;
        searchModel.SetGridPageSize();

        return searchModel;
    }

    public virtual async Task<ApproverListModel> PrepareApproverListModelAsync(ApproverSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var approvers = await _approverService.SearchApproversAsync(searchModel.SearchStoreId, searchModel.SearchText,
            searchModel.Page - 1, searchModel.PageSize);

        var approverIds = approvers.Select(approver => approver.Id).ToArray();
        var rules = (await _approverService.GetRulesAsync(approverIds)).ToLookup(item => item.ApproverId, item => item.Rule);
        var counts = await _approverService.GetCountsAsync(approverIds);

        var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        var storeNames = (await _storeService.GetAllStoresAsync()).ToDictionary(store => store.Id, store => store.Name);

        var suggestions = ApproverSuggestionModel.From(await _approverService.SearchApproversAsync());

        return await new ApproverListModel { Suggestions = suggestions }.PrepareToGridAsync(searchModel, approvers, () =>
        {
            return approvers.SelectAwait(async approver =>
            {
                var approvesFor = new List<string>();

                foreach (var rule in rules[approver.Id])
                {
                    var text = await GetRuleTextAsync(rule, currency);

                    if (text != null)
                        approvesFor.Add(text);
                }

                return new ApproverModel
                {
                    Id = approver.Id,
                    Name = approver.Name,
                    Email = approver.Email,
                    StoreId = approver.StoreId,
                    StoreName = storeNames.TryGetValue(approver.StoreId, out var storeName) ? storeName : string.Empty,
                    ApprovesFor = string.Join(", ", approvesFor),
                    Waiting = counts[approver.Id].Waiting,
                    Approved = counts[approver.Id].Approved,
                    Declined = counts[approver.Id].Declined,
                    HasRules = approvesFor.Count > 0,
                    CanDelete = counts[approver.Id].Waiting == 0 && !rules[approver.Id].Any(rule => rule.Active)
                };
            });
        });
    }

    #endregion
}
