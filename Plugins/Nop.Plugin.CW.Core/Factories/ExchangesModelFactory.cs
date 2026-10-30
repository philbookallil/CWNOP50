using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Web.Factories;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Model Factory class intended to prepare view models related to ViewComponents
/// </summary>
public class ExchangesModelFactory : IExchangesModelFactory
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly IStoreContext _storeContext;
    protected readonly IOrderService _orderService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IWorkContext _workContext;
    protected readonly IExchangesLogic _exchangesLogic;
    protected readonly IReturnRequestOverrideModelFactory _returnRequestModelFactory;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IProductService _productService;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly IProductAttributeFormatter _productAttributeFormatter;
    protected readonly IProductModelFactory _productModelFactory;
    protected readonly IPictureService _pictureService;
    protected readonly IExchangesDisplayService _exchangesDisplayService;
    protected readonly ILocalizationService _localizationService;

    #endregion

    #region Ctor

    public ExchangesModelFactory(IExchangeService exchangeService,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IOrderService orderService,
        IReturnRequestService returnRequestService,
        IWorkContext workContext,
        IExchangesLogic exchangesLogic,
        IReturnRequestOverrideModelFactory returnRequestModelFactory,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IUrlRecordService urlRecordService,
        IProductAttributeService productAttributeService,
        IProductAttributeParser productAttributeParser,
        IProductAttributeFormatter productAttributeFormatter,
        IProductModelFactory productModelFactory,
        IPictureService pictureService,
        IExchangesDisplayService exchangesDisplayService,
        ILocalizationService localizationService)
    {
        _exchangeService = exchangeService;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _orderService = orderService;
        _returnRequestService = returnRequestService;
        _workContext = workContext;
        _exchangesLogic = exchangesLogic;
        _returnRequestModelFactory = returnRequestModelFactory;
        _priceFormatter = priceFormatter;
        _productService = productService;
        _urlRecordService = urlRecordService;
        _productAttributeService = productAttributeService;
        _productAttributeParser = productAttributeParser;
        _productAttributeFormatter = productAttributeFormatter;
        _productModelFactory = productModelFactory;
        _pictureService = pictureService;
        _exchangesDisplayService = exchangesDisplayService;
        _localizationService = localizationService;
    }

    #endregion

    #region Utilities

    protected async Task PopulateFeesOnModelAsync(IDictionary<FeeType, decimal> exchangeFees, ExchangesFeesModel model, IList<AdditionalFeeItem> additionalFeeItems = null)
    {
        foreach (var feeEntry in exchangeFees)
        {
            var feeType = feeEntry.Key;
            var feeAmount = feeEntry.Value;
            var feeModelItem = model.Fees.FirstOrDefault(f => f.FeeType == feeType);
            // Fee item already exists, only update amount
            if (feeModelItem != null)
            {
                feeModelItem.FeeAmount += feeAmount;
            }
            else // Fee item not found, create it
            {
                var feeConceptName = await _exchangesDisplayService.GetFeeTypeNameAsync(feeType);
                model.Fees.Add(new FeeDisplayModel
                {
                    FeeType = feeType,
                    FeeConcept = feeConceptName,
                    FeeAmount = feeAmount
                });
            }
        }

        if (additionalFeeItems != null)
        {
            foreach (var item in additionalFeeItems)
            {
                model.AdditionalFees.Add(new AdditionalFeeItemModel(item));
            }
        }
    }

    protected async Task PopulateReturnRequestReasonActionAsync(ReturnRequestGatherInfoModel model, int returnRequestActionId, int returnRequestReasonId)
    {
        var returnRequestAction = await _returnRequestService.GetReturnRequestActionByIdAsync(returnRequestActionId);
        if (returnRequestAction != null)
        {
            model.ReturnRequestActionId = returnRequestAction.Id;
            model.ReturnRequestActionName = await _localizationService.GetLocalizedAsync(returnRequestAction, action => action.Name);
        }

        var returnRequestReason = await _returnRequestService.GetReturnRequestReasonByIdAsync(returnRequestReasonId);
        if (returnRequestReason != null)
        {
            model.ReturnRequestReasonId = returnRequestReason.Id;
            model.ReturnRequestReasonName = await _localizationService.GetLocalizedAsync(returnRequestReason, reason => reason.Name);
        }
    }

    /// <summary>
    /// Lists the fee rules that may apply to this reason and action, formatted for display.
    /// Per-rule only - the total depends on quantities that are not final at this step.
    /// </summary>
    protected virtual async Task PopulateEstimatedFeesAsync(ReturnRequestGatherInfoModel model, int storeId)
    {
        var feeRules = await _exchangeService.GetFeeRulesForActionAndReasonAsync(
            model.ReturnRequestActionId, model.ReturnRequestReasonId, storeId);

        foreach (var rule in feeRules)
        {
            var amount = await _exchangesDisplayService.FormatPriceAsync(rule.Amount);

            model.EstimatedFees.Add(new ReturnRequestGatherInfoModel.EstimatedFeeModel
            {
                Concept = await _exchangesDisplayService.GetFeeTypeNameAsync(rule.FeeType),
                AmountDisplay = rule.Operator == FeeRuleOperator.PerUnitsQuantity && rule.UnitsQuantity > 0
                    ? (rule.UnitsQuantity == 1
                        ? string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Fees.PerItem"), amount)
                        : string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Fees.PerItems"), amount, rule.UnitsQuantity))
                    : amount
            });
        }
    }

    /// <summary>
    /// Builds the replacement product a customer chose, from their submitted line
    /// </summary>
    /// <param name="line">The submitted line; must carry a replacement product id</param>
    /// <returns>The replacement product model; null when the product is gone (the server checks then refuse the line)</returns>
    protected virtual async Task<ReturnRequestGatherInfoModel.ReplacementProductModel> PrepareReplacementProductModelAsync(ReturnWizardLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var product = await _productService.GetProductByIdAsync(line.ReplacementProductId.Value);
        if (product == null || product.Deleted)
            return null;

        var model = new ReturnRequestGatherInfoModel.ReplacementProductModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            ProductSeName = await _urlRecordService.GetSeNameAsync(product)
        };

        var attributesXml = string.Empty;

        foreach (var mapping in await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id))
        {
            if (!line.ReplacementAttributes.TryGetValue(mapping.Id, out var attributeValueId))
                continue;

            var values = await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id);
            var attributeValue = values.FirstOrDefault(v => v.Id == attributeValueId);

            if (attributeValue == null)
                continue;

            model.Attributes.Add(new ReturnRequestGatherInfoModel.ReplacementProductAttrModel
            {
                AttributeId = mapping.Id,
                AttributeValueId = attributeValue.Id
            });

            attributesXml = _productAttributeParser.AddProductAttribute(attributesXml, mapping, attributeValue.Id.ToString());
        }

        model.PictureUrl = await _exchangesDisplayService.GetProductThumbUrlAsync(product.Id, attributesXml, CWCoreDefaults.ReturnItemThumbSize);

        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();
        var replacementCart = _exchangesLogic.BuildSingleProductItemCart(store, product.Id, customer, attributesXml);
        var replacementPrice = await _exchangesLogic.CalculateCartItemsTotalAmountAsync(replacementCart);

        model.UnitPrice = replacementPrice ?? decimal.Zero;
        model.UnitPriceDisplay = await _exchangesDisplayService.FormatPriceAsync(model.UnitPrice);

        if (!string.IsNullOrEmpty(attributesXml))
        {
            model.AttributesInfo = await _productAttributeFormatter.FormatAttributesAsync(product, attributesXml);
            model.AttributesXml = attributesXml;
        }

        return model;
    }

    protected async Task<FeeConceptItem> BuildFeeChargeItemAsync(FeeRule rule)
    {
        return new FeeConceptItem(rule, await _exchangesDisplayService.GetFeeTypeNameAsync(rule.FeeType));
    }

    #endregion

    #region Methods

    #region Fees

    public async Task<ExchangesFeesModel> PrepareExchangesFeesModelFromOrderAsync(Order order)
    {
        var model = new ExchangesFeesModel();
        var additionalFees = new List<AdditionalFeeItem>();
        var exchangeFeesInOrder = await _exchangeService.GetExchangeFeesAmountsAsync(order, additionalFees);
        if (exchangeFeesInOrder != null)
        {
            await PopulateFeesOnModelAsync(exchangeFeesInOrder, model, additionalFees);
        }
        return model;
    }

    public async Task<ExchangesFeesModel> PrepareExchangesFeesModelFromCartAsync(Customer customer, Store store)
    {
        if (customer != null && store != null)
        {
            var model = new ExchangesFeesModel();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            var additionalFees = new List<AdditionalFeeItem>();
            var exchangeFeesInCart = await _exchangeService.GetExchangeFeesAmountsAsync(cart, additionalFees);

            if (exchangeFeesInCart != null)
            {
                await PopulateFeesOnModelAsync(exchangeFeesInCart, model, additionalFees);
            }

            return model;
        }
        return null;
    }

    #endregion

    #region Gather step 

    /// <summary>
    /// Assembles the gather step model. Quantities, group id and case id are resolved by the caller,
    /// so this works both for a first visit and for redisplaying the step after a validation failure.
    /// </summary>
    /// <param name="order">The order being returned against</param>
    /// <param name="returnRequestModel">Reason, action, comments and uploaded file</param>
    /// <param name="submission">Submission data</param>
    /// <param name="returnRequestGroupId">Identifies this attempt; preserved across a retry</param>
    /// <param name="returnCaseId">The draft return case, already ensured by the caller</param>
    public virtual async Task<ReturnRequestGatherInfoModel> PrepareGatherInfoModelAsync(
        Order order,
        SubmitReturnRequestModel returnRequestModel,
        ReturnWizardSubmission submission,
        Guid returnRequestGroupId,
        int returnCaseId)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(returnRequestModel);
        ArgumentNullException.ThrowIfNull(submission);

        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new ReturnRequestGatherInfoModel
        {
            Comments = returnRequestModel.Comments,
            EnabledExchangeModes = await _exchangeService.DetermineEnabledReturnModesAsync(
                store.Id, returnRequestModel.ReturnRequestReasonId, returnRequestModel.ReturnRequestActionId),
            OrderId = order.Id,
            CustomOrderNumber = order.CustomOrderNumber,
            UploadedFileGuid = returnRequestModel.UploadedFileGuid,
            ReturnRequestGroupId = returnRequestGroupId,
            ReturnCaseId = returnCaseId,
            Mode = submission.Mode
        };

        await PopulateReturnRequestReasonActionAsync(model, returnRequestModel.ReturnRequestActionId, returnRequestModel.ReturnRequestReasonId);

        await PopulateEstimatedFeesAsync(model, store.Id);

        foreach (var orderItem in await _orderService.GetOrderItemsAsync(order.Id, isNotReturnable: false))
        {
            var line = submission.LineFor(orderItem.Id);
            if (line == null || line.Quantity <= 0)
                continue;

            // A credit note returns the item as-is, so the type is not the customer's to choose
            var exchangeTypeId = submission.Mode == ExchangeReturnMode.CreditNote
                ? (int)ExchangeType.Exact
                : (int?)line.ExchangeType;

            var itemModel = await BuildReturnedItemModelAsync(order, orderItem, line.Quantity, exchangeTypeId);

            // Restores the chosen replacement when redisplaying after a validation failure; a credit note has none
            if (submission.Mode != ExchangeReturnMode.CreditNote && line.ReplacementProductId.HasValue)
                itemModel.ReplacementProduct = await PrepareReplacementProductModelAsync(line);

            model.ReturnedItems.Add(itemModel);
        }

        return model;
    }

    public async Task<ReturnRequestGatherInfoModel.ReturnedOrderItemModel> BuildReturnedItemModelAsync(Order order, OrderItem returnedOrderItem, int returnedQuantity, int? exchangeTypeId = null)
    {
        var exchangeTypes = new List<SelectListItem>
        {
            new() { Value = "0", Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.SelectPlaceholder") }
        };

        foreach (var exchangeType in Enum.GetValues<ExchangeType>())
            exchangeTypes.Add(new SelectListItem { Value = ((int)exchangeType).ToString(), Text = await _exchangesDisplayService.GetExchangeTypeNameAsync(exchangeType) });

        var exchangedItemPrice = ExchangesHelper.DetermineReturnedItemPrice(returnedOrderItem);

        var item = new ReturnRequestGatherInfoModel.ReturnedOrderItemModel
        {
            AvailableExchangeTypes = new SelectList(exchangeTypes, nameof(SelectListItem.Value), nameof(SelectListItem.Text), (exchangeTypeId ?? 0).ToString()),
            //model = await _returnRequestModelFactory.PrepareSubmitReturnRequestModelAsync(model, order);
            Item = await _returnRequestModelFactory.PrepareSubmitReturnRequestOrderItemModelAsync(order, returnedOrderItem, returnedOrderItem.Quantity),
            ReturnedQuantity = returnedQuantity,
            UnitPrice = exchangedItemPrice
        };

        item.UnitPriceDisplay = await _exchangesDisplayService.FormatPriceAsync(item.UnitPrice);
        item.ExchangeTypeId = exchangeTypeId ?? 0;

        return item;
    }

    /// <summary>
    /// Rebuilds the gather model from a submission. The returned items are not model-bound, so every
    /// action that receives a wizard post has to reconstruct them.
    /// </summary>
    /// <param name="gatheredInfo">The model-bound hidden fields carried through the wizard</param>
    /// <param name="submission">The parsed post</param>
    /// <returns>The rebuilt model, or null when the order no longer exists</returns>
    public async Task<ReturnRequestGatherInfoModel> RebuildGatheredInfoAsync(ReturnRequestGatherInfoModel gatheredInfo, ReturnWizardSubmission submission)
    {
        var order = await _orderService.GetOrderByIdAsync(gatheredInfo.OrderId);
        if (order == null)
            return null;

        var returnRequestModel = new SubmitReturnRequestModel
        {
            Comments = gatheredInfo.Comments,
            ReturnRequestReasonId = gatheredInfo.ReturnRequestReasonId,
            ReturnRequestActionId = gatheredInfo.ReturnRequestActionId,
            UploadedFileGuid = gatheredInfo.UploadedFileGuid
        };

        return await PrepareGatherInfoModelAsync(
            order,
            returnRequestModel,
            submission,
            // Same attempt throughout - a new group id would orphan the draft case
            gatheredInfo.ReturnRequestGroupId,
            gatheredInfo.ReturnCaseId);
    }

    #endregion

    #region Replacement picker

    public virtual async Task<ReplacementProductListModel> PrepareReplacementProductListModelAsync(int storeId, int orderItemId, int excludeProductId, string keywords, int pageIndex, int pageSize)
    {
        var products = await _productService.SearchProductsAsync(
            pageIndex: pageIndex,
            pageSize: pageSize,
            storeId: storeId,
            keywords: string.IsNullOrWhiteSpace(keywords) ? null : keywords.Trim(),
            searchSku: true,
            visibleIndividuallyOnly: true,
            orderBy: ProductSortingEnum.NameAsc,
            overridePublished: true);

        var model = new ReplacementProductListModel
        {
            OrderItemId = orderItemId,
            ExcludeProductId = excludeProductId,
            Keywords = keywords,
            PageIndex = products.PageIndex,
            TotalPages = products.TotalPages,
            TotalCount = products.TotalCount
        };

        // The returned product is kept in the results and rendered as unavailable
        // preparePriceModel: needed - the customer is comparing prices
        // preparePictureModel: needed - this is a visual choice
        // prepareSpecificationAttributes: not needed, and it is the expensive one
        var overviews = await _productModelFactory.PrepareProductOverviewModelsAsync(
            products.ToList(),
            preparePriceModel: true,
            preparePictureModel: true,
            prepareSpecificationAttributes: false);

        model.Products = overviews.ToList();

        return model;
    }

    public virtual async Task<ReplacementProductOptionsModel> PrepareReplacementProductOptionsModelAsync(Product product, int orderItemId)
    {
        ArgumentNullException.ThrowIfNull(product);

        var productModel = await _productModelFactory.PrepareProductDetailsModelAsync(product);

        var model = new ReplacementProductOptionsModel
        {
            OrderItemId = orderItemId,
            Product = productModel,
            DefaultPictureUrl = productModel.PictureModels.FirstOrDefault()?.ImageUrl
        };

        // PictureId already arrived with the attribute data - resolving the URLs costs no extra query
        foreach (var attribute in productModel.ProductAttributes)
            foreach (var value in attribute.Values.Where(v => v.PictureId > 0))
                model.AttributeValuePictures[value.Id] =
                    await _pictureService.GetPictureUrlAsync(value.PictureId, CWCoreDefaults.ReplacementPreviewSize);

        // What this product needs that the picker has no field for. Non-empty stops the customer
        // choosing it and tells them why. Deciding it here, once, keeps the view, the disabled
        // Choose button and the refusal message from drifting apart.
        model.UncollectableRequiredOptions = ProductsHelper.FindUncollectableRequiredAttributes(productModel);

        // Neither of these has a field in the picker, and nop requires both at add-to-cart
        if (productModel.IsRental)
            model.UncollectableRequiredOptions.Add(
                await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Picker.RentalDates"));

        if (productModel.GiftCard.IsGiftCard)
            model.UncollectableRequiredOptions.Add(
                await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Picker.GiftCardDetails"));

        return model;
    }

    /// <summary>
    /// Marks the attribute values the customer already has as pre-selected, so the picker opens
    /// showing their current combination rather than an empty form.
    /// </summary>
    public async Task PreselectCurrentAttributesAsync(ProductDetailsModel product, int? returnRequestId, int orderItemId)
    {
        var attributesXml = string.Empty;

        if (returnRequestId.HasValue && returnRequestId.Value > 0)
        {
            var requestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequestId.Value);
            if (requestItem != null)
                attributesXml = (await _exchangeService.GetReplacementItemAsync(requestItem.Id))?.AttributesXml;
        }
        else if (orderItemId > 0)
        {
            attributesXml = (await _orderService.GetOrderItemByIdAsync(orderItemId))?.AttributesXml;
        }

        if (string.IsNullOrEmpty(attributesXml))
            return;

        var selected = await _exchangesDisplayService.GetSelectedAttributeMapAsync(attributesXml);
        if (selected.Count == 0)
            return;

        foreach (var attribute in product.ProductAttributes)
        {
            if (!selected.TryGetValue(attribute.Id.ToString(), out var selectedValueId))
                continue;

            foreach (var value in attribute.Values)
                value.IsPreSelected = value.Id.ToString() == selectedValueId;
        }
    }

    #endregion

    #region Summary

    public async Task PrepareSummaryFeesAsync(ReturnRequestSummaryModel model)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var feeRules = await _exchangeService.GetFeeRulesForActionAndReasonAsync(model.GatheredInfo.ReturnRequestActionId,
                            model.GatheredInfo.ReturnRequestReasonId, store.Id);
        foreach (var feeRule in feeRules)
        {
            model.EstimatedFees.Add(await BuildFeeChargeItemAsync(feeRule));
        }
    }

    #endregion

    #endregion
}
