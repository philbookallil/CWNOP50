using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class ReturnRequestExchangeModelFactory : IReturnRequestExchangeModelFactory
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly IProductService _productService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IOrderService _orderService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ITaxService _taxService;
    protected readonly IStoreService _storeService;
    protected readonly ICustomerService _customerService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly IBranchService _branchService;
    protected readonly IExchangesDisplayService _exchangesDisplayService;
    protected readonly ICreditNotesService _creditNotesService;

    #endregion

    #region Ctor

    public ReturnRequestExchangeModelFactory(IExchangeService exchangeService,
        IProductService productService,
        IReturnRequestService returnRequestService,
        IOrderService orderService,
        ILocalizationService localizationService,
        ITaxService taxService,
        IStoreService storeService,
        ICustomerService customerService,
        IShoppingCartService shoppingCartService,
        IReturnCaseService returnCaseService,
        IBranchService branchService,
        IExchangesDisplayService exchangesDisplayService,
        ICreditNotesService creditNotesService)
    {
        _exchangeService = exchangeService;
        _productService = productService;
        _returnRequestService = returnRequestService;
        _orderService = orderService;
        _localizationService = localizationService;
        _taxService = taxService;
        _storeService = storeService;
        _customerService = customerService;
        _shoppingCartService = shoppingCartService;
        _returnCaseService = returnCaseService;
        _branchService = branchService;
        _exchangesDisplayService = exchangesDisplayService;
        _creditNotesService = creditNotesService;
    }

    #endregion

    #region Utilities

    /// <returns>The branch id and name, or (0, null) when the store does not use branches</returns>
    protected async Task<(int Id, string Name)> GetBranchInfo(ReturnRequest returnRequest)
    {
        var branch = await _branchService.GetBranchByCustomerIdAsync(returnRequest.CustomerId);

        return (branch?.Id ?? 0, branch?.Name);
    }

    protected async Task PrepareRequestContextAsync(CWReturnRequestModel model, ReturnRequest returnRequest, Store store, Customer customer)
    {
        model.StoreId = returnRequest.StoreId;

        // A return always has a store, so a missing one is a data problem, not an absent value
        model.StoreName = store?.Name ?? $"(store #{returnRequest.StoreId} no longer exists)";

        var (id, name) = await GetBranchInfo(returnRequest);
        model.BranchId = id;
        model.BranchName = name;

        model.CustomerName = await _customerService.GetCustomerFullNameAsync(customer);
        //model.ErpNumber = GetERPNumber(returnRequest); //TODO: ERP integration is pending
        model.Packname = await _exchangesDisplayService.GetPacknameAsync(returnRequest.OrderItemId);

        // Get a reference to the Order Item related to the return request to obtain the Price
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        if (orderItem != null)
        {
            model.ProductPrice = ExchangesHelper.DetermineReturnedItemPrice(orderItem);
        }
    }

    protected async Task PrepareReasonAndActionListsAsync(CWReturnRequestModel model, ReturnRequestItem requestItem)
    {
        var reasons = await _returnRequestService.GetAllReturnRequestReasonsAsync();
        var actions = await _returnRequestService.GetAllReturnRequestActionsAsync();

        model.ReturnRequestReasonId = requestItem?.ReturnReasonId ?? 0;
        model.ReturnRequestActionId = requestItem?.ReturnActionId ?? 0;

        model.ReturnReasonsList = new SelectList(reasons, "Id", "Name", model.ReturnRequestReasonId);
        model.ReturnActionsList = new SelectList(actions, "Id", "Name", model.ReturnRequestActionId);
    }

    protected async Task PrepareEditabilityStateAsync(CWReturnRequestModel model, ReturnRequestModel requestData, ReturnRequest returnRequest)
    {
        model.OriginalRequestStatusId = requestData.ReturnRequestStatusId;
        model.IsEditable = !ExchangesHelper.IsFinalReturnRequestStatus((ReturnRequestStatus)requestData.ReturnRequestStatusId);

        if (string.IsNullOrEmpty(requestData.ReturnRequestStatusStr) && requestData.ReturnRequestStatusId >= 0)
        {
            model.ReturnRequestStatusStr = await _localizationService.GetLocalizedEnumAsync(returnRequest.ReturnRequestStatus);
        }
    }

    protected async Task PrepareExchangeDetailAsync(CWReturnRequestModel model, ReturnRequestModel requestData, ReturnRequestItem requestItem, ReturnRequest returnRequest, Store store, Customer customer)
    {
        // No exchange data for this return - nothing to add
        if (requestItem == null)
            return;

        model.NeedsCompletion = requestItem.NeedsCompletion;
        model.IsCompleted = requestItem.IsCompleted;

        model.ExchangeModeId = requestItem.ExchangeModeId;
        model.ExchangeModeName = ExchangesHelper.DetermineExchangeModeName(requestItem.ExchangeMode);
        model.ExchangeTypeId = requestItem.ExchangeTypeId;
        model.ExchangeTypeName = ExchangesHelper.DetermineExchangeTypeName(requestItem.ExchangeType);
        model.StaffRepackQty = requestItem.StaffRepackQty.HasValue ? requestItem.StaffRepackQty.Value : requestData.Quantity;
        model.StaffShipQty = requestItem.StaffShipQty.HasValue ? requestItem.StaffShipQty.Value : requestData.Quantity;
        model.AdditionalFeeName = requestItem.AdditionalFeeName;
        model.AdditionalFeeAmount = requestItem.AdditionalFeeAmount;

        // Pre-check the "Enable Additional Fee" toggle when a fee has already been recorded
        model.EnableAdditionalFee = !string.IsNullOrEmpty(requestItem.AdditionalFeeName)
            || (requestItem.AdditionalFeeAmount.HasValue && requestItem.AdditionalFeeAmount.Value > decimal.Zero);

        var replacementItem = await _exchangeService.GetReplacementItemAsync(requestItem.Id);
        if (replacementItem != null)
        {
            var product = await _productService.GetProductByIdAsync(replacementItem.ProductId);
            var replacementPrice = replacementItem.UnitPrice;
            if (product != null)
            {
                var (unitPrice, _, _) = await _shoppingCartService.GetUnitPriceAsync(product, customer,
                                        store, ShoppingCartType.ShoppingCart,
                                        1, replacementItem.AttributesXml, 0,
                                        null, null, true);
                (replacementPrice, _) = await _taxService.GetProductPriceAsync(product, unitPrice);
            }

            model.ReplacementProductAttrInfo = replacementItem.AttributesInfo;
            model.ReplacementProductId = replacementItem.ProductId;
            model.ReplacementProductName = replacementItem.ProductName;
            model.ReplacementProductPrice = replacementPrice;
        }

        if (requestItem.ReturnActionId > 0 && requestItem.ReturnReasonId > 0)
        {
            var feeRules = await _exchangeService.GetFeeRulesForActionAndReasonAsync(requestItem.ReturnActionId, requestItem.ReturnReasonId, returnRequest.StoreId);
            var shippingFeeConfigRule = feeRules.FirstOrDefault(r => r.FeeType == FeeType.ReShippingFee);
            var repackagingFeeConfigRule = feeRules.FirstOrDefault(r => r.FeeType == FeeType.RepackagingFee);

            if (shippingFeeConfigRule != null)
            {
                model.ShippingFeeConfig = new FeeConceptItem(shippingFeeConfigRule, ExchangesHelper.DetermineFeeTypeName(FeeType.ReShippingFee));
            }

            if (repackagingFeeConfigRule != null)
            {
                model.RepackagingFeeConfig = new FeeConceptItem(repackagingFeeConfigRule, ExchangesHelper.DetermineFeeTypeName(FeeType.RepackagingFee));
            }
        }

        //This line's own fees — frozen if the case has resolved, live from the rules if it has not.
        var feeCaseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(requestItem.ReturnRequestId);

        var feeAmounts = feeCaseLine != null
            ? (await _exchangeService.GetExchangeFeeAmountsForCaseAsync(feeCaseLine.ReturnCaseId))
                .Where(fee => fee.ReturnCaseLineId == feeCaseLine.Id).ToList()
            : await _exchangeService.CalculateExchangeFeeAmountsForItemAsync(requestItem);

        var shippingFee = feeAmounts.FirstOrDefault(fee => fee.FeeType == FeeType.ReShippingFee);
        var repackagingFee = feeAmounts.FirstOrDefault(fee => fee.FeeType == FeeType.RepackagingFee);

        var additionalFee = feeAmounts.FirstOrDefault(fee => fee.FeeType == FeeType.AdditionalFee);

        model.ShippingFeeAmount = shippingFee?.Amount ?? decimal.Zero;
        model.ShippingFeeQuantity = shippingFee?.Quantity ?? 0;
        model.RepackagingFeeAmount = repackagingFee?.Amount ?? decimal.Zero;

        //The ad-hoc fee as charged, not as typed. Until the case resolves the two are the same; after it,
        //the editable field is just an input and this is the money.
        model.AdditionalFeeChargedAmount = additionalFee?.Amount ?? decimal.Zero;

        //Only meaningful once the case has resolved: before that no note exists yet and null is what tells
        //the panel to label its figures as estimates rather than as money.
        if (!model.IsEditable)
        {
            var raisedNote = await _creditNotesService.GetCreditNoteByReturnRequestIdAsync(requestItem.ReturnRequestId);
            model.CreditNoteRaisedAmount = raisedNote?.Amount ?? decimal.Zero;
        }
    }

    protected async Task PrepareReturnCaseDetailAsync(CWReturnRequestModel model, ReturnRequestModel requestData)
    {
        // Return case data — the accept/reject decision lives on the case line, not on the NOP row
        var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(requestData.Id);

        if (caseLine != null)
        {
            model.DispositionReason = caseLine.DispositionReason;
            model.ReturnCaseId = caseLine.ReturnCaseId;

            var returnCase = await _returnCaseService.GetReturnCaseByIdAsync(caseLine.ReturnCaseId);

            // The case is what ties the sibling lines together - without it there is nothing to list
            if (returnCase == null)
                return;

            model.ReturnCaseNumber = returnCase.CaseNumber;

            var siblingLines = await _returnCaseService.GetReturnCaseLinesAsync(caseLine.ReturnCaseId);

            var caseOrderItems = (await _orderService.GetOrderItemsAsync(returnCase.OrderId)).ToDictionary(item => item.Id);

            var caseProducts = (await _productService.GetProductsByIdsAsync(
                    caseOrderItems.Values.Select(item => item.ProductId).Distinct().ToArray()))
                .ToDictionary(product => product.Id);

            foreach (var sibling in siblingLines)
            {
                caseOrderItems.TryGetValue(sibling.OrderItemId, out var siblingOrderItem);
                Product siblingProduct = null;

                if (siblingOrderItem != null)
                    caseProducts.TryGetValue(siblingOrderItem.ProductId, out siblingProduct);

                model.CaseLines.Add(new CWReturnRequestModel.CaseLineModel
                {
                    ReturnRequestId = sibling.ReturnRequestId ?? 0,
                    ProductName = siblingProduct?.Name ?? "(product unavailable)",
                    Quantity = sibling.Quantity,
                    Mode = ExchangesHelper.DetermineExchangeModeName(sibling.Mode),
                    Disposition = sibling.Disposition.ToString(),
                    IsCurrent = sibling.Id == caseLine.Id
                });
            }
        }
    }

    #endregion

    #region Methods

    public async Task PrepareExchangeRequestModelAsync(CWReturnRequestModel model, ReturnRequestModel requestData)
    {
        var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(requestData.Id);
        // Find if the Return Request has exchange request data related to it
        var requestItem = await _exchangeService.GetReturnRequestItemAsync(requestData.Id);

        var store = await _storeService.GetStoreByIdAsync(returnRequest.StoreId);
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        await PrepareRequestContextAsync(model, returnRequest, store, customer);
        await PrepareReasonAndActionListsAsync(model, requestItem);
        await PrepareEditabilityStateAsync(model, requestData, returnRequest);
        await PrepareExchangeDetailAsync(model, requestData, requestItem, returnRequest, store, customer);
        await PrepareReturnCaseDetailAsync(model, requestData);
    }

    #endregion
}
