using System.Globalization;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Orders;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <inheritdoc />
/// <remarks>
/// Each destination of an order (CW.Core's packname destinations; a plain order has one) becomes its own Odoo
/// sale order, recorded in <see cref="OdooOrderMapping"/>. The Odoo order is created with its lines in one call,
/// its id saved at once, then confirmed: an order whose confirmation failed is confirmed, not created again, on
/// the next send. Before creating, the order is looked up by its origin, which catches a create whose answer was
/// lost.
/// </remarks>
public class OrderSyncService : IOrderSyncService
{
    #region Fields

    protected readonly IAddressService _addressService;
    protected readonly IBranchService _branchService;
    protected readonly ICountryService _countryService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IOdooApiService _odooApiService;
    protected readonly IOdooLogService _odooLog;
    protected readonly IOrderService _orderService;
    protected readonly IPackNameService _packNameService;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly IProductService _productService;
    protected readonly IRepository<OdooOrderMapping> _orderMappingRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly OdooSettings _settings;

    #endregion

    #region Ctor

    public OrderSyncService(
        IAddressService addressService,
        IBranchService branchService,
        ICountryService countryService,
        ICWOrderService cwOrderService,
        IExchangeService exchangeService,
        IGenericAttributeService genericAttributeService,
        IOdooApiService odooApiService,
        IOdooLogService odooLog,
        IOrderService orderService,
        IPackNameService packNameService,
        IProductAttributeParser productAttributeParser,
        IProductService productService,
        IRepository<OdooOrderMapping> orderMappingRepository,
        IRepository<Order> orderRepository,
        IStateProvinceService stateProvinceService,
        OdooSettings settings)
    {
        _addressService = addressService;
        _branchService = branchService;
        _countryService = countryService;
        _cwOrderService = cwOrderService;
        _exchangeService = exchangeService;
        _genericAttributeService = genericAttributeService;
        _odooApiService = odooApiService;
        _odooLog = odooLog;
        _orderService = orderService;
        _packNameService = packNameService;
        _productAttributeParser = productAttributeParser;
        _productService = productService;
        _orderMappingRepository = orderMappingRepository;
        _orderRepository = orderRepository;
        _stateProvinceService = stateProvinceService;
        _settings = settings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// What one run looks up once and reuses for every order of the batch.
    /// </summary>
    protected sealed class SendRun
    {
        public int? DeliveryProductId { get; init; }
        public int? FeeProductId { get; init; }
        public Dictionary<int, (int? PartnerId, string Reason)> CompanyByCustomerId { get; } = new();
        public Dictionary<string, int> PartnerByAddress { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int?> CountryIdByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int?> StateIdByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A destination of a nop order: where its items go, and what its shipping costs.
    /// </summary>
    protected sealed class Destination
    {
        public int Key { get; init; }
        public int? ShippingAddressId { get; init; }
        public bool PickupInStore { get; init; }
        public string ShippingMethod { get; init; }
        public decimal ShippingExclTax { get; init; }
        public IList<int> OrderItemIds { get; init; } = new List<int>();
    }

    #region Lookups

    /// <summary>
    /// The Odoo id in an <c>ODOO_{id}</c> link, or null.
    /// </summary>
    protected static int? ParseOdooLink(string link) =>
        link != null
        && link.StartsWith(OdooConstants.OdooLinkPrefix, StringComparison.Ordinal)
        && int.TryParse(link[OdooConstants.OdooLinkPrefix.Length..], out var id) ? id : null;

    /// <summary>
    /// The product.product id of an Odoo product's internal reference, logged when it is set but not found.
    /// </summary>
    protected async Task<int?> FindProductByCodeAsync(int logEventId, string code, string purpose)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var id = await _odooApiService.GetProductVariantIdByCodeAsync(code.Trim());
        if (id is null)
            await _odooLog.WarningAsync($"No active Odoo product has the internal reference '{code}' set as the {purpose} product.",
                logEventId, entityType: "Order");

        return id;
    }

    /// <summary>
    /// The Odoo company of a customer: the Odoo id of the top branch above the customer's branch.
    /// </summary>
    protected async Task<(int? PartnerId, string Reason)> ResolveCompanyAsync(SendRun run, int customerId)
    {
        if (run.CompanyByCustomerId.TryGetValue(customerId, out var known))
            return known;

        (int? PartnerId, string Reason) result;
        var branch = await _branchService.GetBranchByCustomerIdAsync(customerId);

        if (branch == null)
            result = (null, "The customer is in no branch, so there is no Odoo company to send the order to.");
        else
        {
            var headOffice = branch;

            // A parent is never a descendant (both branch saves check); the bound only guards hand-edited data.
            for (var depth = 0; headOffice.ParentBranchId is int parentId && depth < 50; depth++)
            {
                var parent = await _branchService.GetBranchByIdAsync(parentId);
                if (parent == null)
                    break;

                headOffice = parent;
            }

            result = int.TryParse(headOffice.CardCode, out var partnerId) && partnerId > 0
                ? (partnerId, null)
                : (null, $"Head office '{headOffice.Name}' is not linked to an Odoo company: its card code is not an Odoo id.");
        }

        run.CompanyByCustomerId[customerId] = result;

        return result;
    }

    /// <summary>
    /// The Odoo variant an order item is for: its combination's link, else the product's single variant.
    /// Null when the product did not come from Odoo or the variant is unknown.
    /// </summary>
    protected async Task<int?> ResolveVariantIdAsync(Product product, string attributesXml)
    {
        if (ParseOdooLink(product.ManufacturerPartNumber) is null)
            return null;

        if (!string.IsNullOrEmpty(attributesXml))
        {
            var combination = await _productAttributeParser.FindProductAttributeCombinationAsync(product, attributesXml);
            if (ParseOdooLink(combination?.ManufacturerPartNumber) is int variantId)
                return variantId;
        }

        var singleVariantId = await _genericAttributeService.GetAttributeAsync<int>(product, OdooConstants.ProductVariantIdAttribute);

        return singleVariantId > 0 ? singleVariantId : null;
    }

    /// <summary>
    /// The Odoo delivery address of a draft: the company for a pickup; otherwise the company or one of its
    /// contacts at the same street, postcode and city, or a new delivery contact under the company.
    /// </summary>
    protected async Task<int> ResolveShippingPartnerAsync(SendRun run, int logEventId, Order order, OdooSaleOrderDraft draft)
    {
        if (draft.ShippingAddressId is not int addressId || await _addressService.GetAddressByIdAsync(addressId) is not Address address)
            return draft.CustomerPartnerId;

        var key = $"{draft.CustomerPartnerId}|{address.Address1}|{address.ZipPostalCode}|{address.City}";
        if (run.PartnerByAddress.TryGetValue(key, out var known))
            return known;

        var partnerId = await _odooApiService.FindDeliveryPartnerAsync(draft.CustomerPartnerId, address.Address1, address.ZipPostalCode, address.City);

        if (partnerId is null)
        {
            var name = string.Join(" ", new[] { address.FirstName, address.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
            var values = new Dictionary<string, object>
            {
                ["parent_id"] = draft.CustomerPartnerId,
                ["type"] = "delivery",
                ["name"] = name.Length > 0 ? name : !string.IsNullOrWhiteSpace(address.Company) ? address.Company : address.Address1 ?? "Delivery address"
            };

            void Add(string field, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    values[field] = value.Trim();
            }

            Add("street", address.Address1);
            Add("street2", address.Address2);
            Add("city", address.City);
            Add("zip", address.ZipPostalCode);
            Add("phone", address.PhoneNumber);
            Add("email", address.Email);

            if (address.CountryId is int countryId && await _countryService.GetCountryByIdAsync(countryId) is { } country
                && !string.IsNullOrWhiteSpace(country.TwoLetterIsoCode))
            {
                if (!run.CountryIdByCode.TryGetValue(country.TwoLetterIsoCode, out var odooCountryId))
                    run.CountryIdByCode[country.TwoLetterIsoCode] = odooCountryId = await _odooApiService.GetCountryIdByCodeAsync(country.TwoLetterIsoCode);

                if (odooCountryId is int odooCountry)
                {
                    values["country_id"] = odooCountry;

                    if (address.StateProvinceId is int stateId && await _stateProvinceService.GetStateProvinceByIdAsync(stateId) is { } state
                        && !string.IsNullOrWhiteSpace(state.Abbreviation))
                    {
                        var stateKey = $"{odooCountry}|{state.Abbreviation}";
                        if (!run.StateIdByCode.TryGetValue(stateKey, out var odooStateId))
                            run.StateIdByCode[stateKey] = odooStateId = await _odooApiService.GetStateIdByCodeAsync(odooCountry, state.Abbreviation);

                        if (odooStateId is int odooState)
                            values["state_id"] = odooState;
                    }
                }
            }

            partnerId = await _odooApiService.CreatePartnerAsync(values);
            await _odooLog.InformationAsync($"Order {order.CustomOrderNumber}: created Odoo delivery contact {partnerId} ({values["name"]}) under company {draft.CustomerPartnerId}.",
                logEventId, entityType: "Partner", odooEntityId: partnerId, nopEntityId: address.Id);
        }

        run.PartnerByAddress[key] = partnerId.Value;

        return partnerId.Value;
    }

    #endregion

    #region Building

    /// <summary>
    /// The destinations of an order: CW.Core's on a packname order, else one from the order itself.
    /// </summary>
    protected async Task<IList<Destination>> GetDestinationsAsync(Order order, IList<OrderItem> items)
    {
        if (await _cwOrderService.IsPackNameOrderAsync(order.Id))
        {
            var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);
            if (destinations.Count > 0)
            {
                var shippings = await _cwOrderService.GetOrderShippingAsync(order.Id);

                return destinations.Select(destination => new Destination
                {
                    Key = destination.Key,
                    ShippingAddressId = destination.PickupInStore ? null : destination.ShippingAddressId,
                    PickupInStore = destination.PickupInStore,
                    ShippingMethod = destination.ShippingMethod,
                    ShippingExclTax = shippings
                        .Where(shipping => shipping.PackNameId is int packNameId && destination.PackNameIds.Contains(packNameId))
                        .Sum(shipping => shipping.ShippingExclTax),
                    OrderItemIds = destination.OrderItemIds
                }).ToList();
            }
        }

        return new List<Destination>
        {
            new()
            {
                Key = 0,
                ShippingAddressId = order.PickupInStore ? null : order.ShippingAddressId,
                PickupInStore = order.PickupInStore,
                ShippingMethod = order.ShippingMethod,
                ShippingExclTax = order.OrderShippingExclTax,
                OrderItemIds = items.Select(item => item.Id).ToList()
            }
        };
    }

    /// <summary>
    /// The display name of an exchange fee type.
    /// </summary>
    protected static string FeeName(FeeType feeType) => feeType switch
    {
        FeeType.ReShippingFee => "Re-shipping fee",
        FeeType.RepackagingFee => "Repackaging fee",
        FeeType.AdditionalFee => "Additional fee",
        _ => feeType.ToString()
    };

    /// <summary>
    /// The notes of the whole nop order, carried by its first Odoo order.
    /// </summary>
    protected static void AddOrderNotes(OdooSaleOrderDraft draft, Order order, int destinationCount, bool isExchange, OrderSettlement settlement)
    {
        static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

        // The nop date travels in the note: Odoo may put its own date on the order when it confirms it
        var placed = order.CreatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        draft.NoteLines.Add(destinationCount > 1
            ? $"nop order {order.CustomOrderNumber}, placed {placed} UTC, sent as {destinationCount} Odoo orders, one per delivery address. These notes cover the whole nop order."
            : $"nop order {order.CustomOrderNumber}, placed {placed} UTC.");

        if (isExchange)
            draft.NoteLines.Add("Exchange replacement order.");

        if (settlement != null)
        {
            if (settlement.SubsidyStockAmount > 0)
                draft.NoteLines.Add($"Subsidy paid {Money(settlement.SubsidyStockAmount)} of the goods (incl. tax).");
            if (settlement.SubsidyShippingAmount > 0)
                draft.NoteLines.Add($"Subsidy paid {Money(settlement.SubsidyShippingAmount)} of the shipping (incl. tax).");
            if (settlement.CreditNoteAmount > 0)
                draft.NoteLines.Add($"Store credit paid {Money(settlement.CreditNoteAmount)} (incl. tax).");

            draft.NoteLines.Add($"The customer's share in nop: {Money(order.OrderTotal)} (incl. tax).");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
            draft.NoteLines.Add(string.IsNullOrWhiteSpace(order.PaymentMethodSystemName)
                ? "Paid in nop."
                : $"Paid in nop ({order.PaymentMethodSystemName}).");
    }

    /// <summary>
    /// One draft per destination of the order. A draft that cannot be sent carries the reason; partners in
    /// Odoo are not resolved here, since resolving one may create a contact.
    /// </summary>
    protected async Task<IList<OdooSaleOrderDraft>> BuildDraftsAsync(SendRun run, Order order)
    {
        var items = await _orderService.GetOrderItemsAsync(order.Id);
        var itemsById = items.ToDictionary(item => item.Id);
        var destinations = await GetDestinationsAsync(order, items);
        var (companyId, companyReason) = await ResolveCompanyAsync(run, order.CustomerId);

        // A line in no destination would be left out of every Odoo order
        var uncovered = items.Count(item => !destinations.Any(destination => destination.OrderItemIds.Contains(item.Id)));
        var uncoveredReason = uncovered > 0
            ? $"{uncovered} order line(s) belong to no delivery destination, so the order would be sent incomplete."
            : null;

        // Who each line is for, as it was at placement, and the packnames' PO numbers as they are now
        var packNameItems = new Dictionary<int, PackNameOrderItem>();
        foreach (var item in items)
            if (await _cwOrderService.GetPackNameOrderItemAsync(item.Id) is { } packNameItem)
                packNameItems[item.Id] = packNameItem;

        var packNameIds = packNameItems.Values.Select(packNameItem => packNameItem.PackNameId).Distinct().ToArray();
        var purchaseOrderByPackNameId = packNameIds.Length == 0
            ? new Dictionary<int, string>()
            : (await _packNameService.GetPackNamesByIdsAsync(packNameIds)).ToDictionary(packName => packName.Id, packName => packName.POEntry);

        var isExchange = (await _exchangeService.GetReturnRequestItemsForOrderItemsAsync(items.Select(item => item.Id).ToArray())).Any();
        var fees = isExchange
            ? await _exchangeService.GetExchangeFeesAmountsAsync(order)
            : new Dictionary<FeeType, decimal>();
        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);

        var drafts = new List<OdooSaleOrderDraft>();

        foreach (var destination in destinations)
        {
            var draft = new OdooSaleOrderDraft
            {
                DestinationKey = destination.Key,
                Origin = OdooOrderBuilder.Origin(order.CustomOrderNumber, destination.Key, destinations.Count > 1),
                OrderDateUtc = order.CreatedOnUtc,
                ShippingAddressId = destination.ShippingAddressId
            };
            var isFirst = drafts.Count == 0;
            drafts.Add(draft);

            if (companyId is not int company)
            {
                draft.HoldReason = companyReason;
                continue;
            }

            draft.CustomerPartnerId = company;
            var reasons = new List<string>();
            if (uncoveredReason != null)
                reasons.Add(uncoveredReason);

            // Product lines
            var lines = new List<(OrderItem Item, Product Product, int VariantId, string PackNameInfo, string PurchaseOrder)>();
            foreach (var itemId in destination.OrderItemIds)
            {
                if (!itemsById.TryGetValue(itemId, out var item))
                    continue;

                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product == null || await ResolveVariantIdAsync(product, item.AttributesXml) is not int variantId)
                {
                    reasons.Add($"'{product?.Name ?? $"Product {item.ProductId}"}' is not linked to an Odoo product variant.");
                    continue;
                }

                packNameItems.TryGetValue(item.Id, out var packNameItem);
                var purchaseOrder = packNameItem != null && purchaseOrderByPackNameId.TryGetValue(packNameItem.PackNameId, out var po) ? po : null;
                var info = packNameItem == null
                    ? null
                    : OdooOrderBuilder.PackNameInfo(packNameItem.PackName, packNameItem.Branch, packNameItem.Department, packNameItem.CostCenter);

                lines.Add((item, product, variantId, info, purchaseOrder));
            }

            draft.ClientOrderRef = OdooOrderBuilder.CommonPurchaseOrder(lines.Select(line => line.PurchaseOrder).ToList());

            foreach (var line in lines)
                draft.Lines.Add(new OdooSaleOrderLineDraft
                {
                    ProductId = line.VariantId,
                    Quantity = line.Item.Quantity,
                    PriceUnit = OdooOrderBuilder.UnitPrice(line.Item.PriceExclTax, line.Item.Quantity),
                    Name = OdooOrderBuilder.LineName(line.Product.Name, line.Item.AttributeDescription, line.PackNameInfo,
                        draft.ClientOrderRef == null ? line.PurchaseOrder : null)
                });

            draft.NopUntaxed = lines.Sum(line => line.Item.PriceExclTax);

            // Shipping line
            if (!destination.PickupInStore && (destination.ShippingExclTax > 0 || !string.IsNullOrWhiteSpace(destination.ShippingMethod)))
            {
                if (run.DeliveryProductId is int deliveryProductId)
                {
                    draft.Lines.Add(new OdooSaleOrderLineDraft
                    {
                        ProductId = deliveryProductId,
                        Quantity = 1,
                        PriceUnit = Math.Round(destination.ShippingExclTax, 2, MidpointRounding.AwayFromZero),
                        Name = string.IsNullOrWhiteSpace(destination.ShippingMethod) ? "Shipping" : destination.ShippingMethod,
                        IsDelivery = true
                    });
                    draft.NopUntaxed += destination.ShippingExclTax;
                }
                else if (destination.ShippingExclTax > 0)
                    reasons.Add("Shipping is charged, but no Odoo delivery product is set on the Odoo Settings page.");
            }

            // The fees and the notes of the whole order go on its first Odoo order
            if (isFirst)
            {
                foreach (var (feeType, amount) in fees.Where(fee => fee.Value > 0))
                {
                    if (run.FeeProductId is int feeProductId)
                    {
                        draft.Lines.Add(new OdooSaleOrderLineDraft
                        {
                            ProductId = feeProductId,
                            Quantity = 1,
                            PriceUnit = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
                            Name = FeeName(feeType)
                        });
                        draft.NopUntaxed += amount;
                    }
                    else
                        reasons.Add("The order has exchange fees, but no Odoo exchange fee product is set on the Odoo Settings page.");
                }

                AddOrderNotes(draft, order, destinations.Count, isExchange, settlement);
            }
            else
                draft.NoteLines.Add($"Part of nop order {order.CustomOrderNumber}; its notes are on the Odoo order with origin '{drafts[0].Origin}'.");

            if (draft.Lines.Count == 0)
                reasons.Add("Nothing to send: no line of this destination is linked to Odoo.");

            if (reasons.Count > 0)
                draft.HoldReason = string.Join(" ", reasons.Distinct());
        }

        return drafts;
    }

    #endregion

    #region Sending

    /// <summary>
    /// Saves a mapping row, inserting it the first time.
    /// </summary>
    protected async Task SaveMappingAsync(OdooOrderMapping mapping)
    {
        mapping.UpdatedOnUtc = DateTime.UtcNow;

        if (mapping.Id == 0)
            await _orderMappingRepository.InsertAsync(mapping, false);
        else
            await _orderMappingRepository.UpdateAsync(mapping, false);
    }

    /// <summary>
    /// Creates (or finds) and confirms the Odoo order of one draft.
    /// </summary>
    /// <returns>Created when it created the Odoo order, Updated when the order already existed.</returns>
    /// <remarks>Private: <see cref="SyncOutcome"/> is internal to the plugin.</remarks>
    private async Task<SyncOutcome> SendDraftAsync(SendRun run, int logEventId, Order order, OdooSaleOrderDraft draft, OdooOrderMapping mapping)
    {
        var created = false;

        // Created by an earlier send whose answer was lost
        if (mapping.OdooOrderId is null && await _odooApiService.FindSaleOrderByOriginAsync(draft.Origin) is OdooSaleOrderResponse existing)
        {
            mapping.OdooOrderId = existing.Id;
            await _odooLog.InformationAsync($"Order {order.CustomOrderNumber}: Odoo already has {existing.Name} with origin '{draft.Origin}'; it is used.",
                logEventId, entityType: "Order", odooEntityId: existing.Id, nopEntityId: order.Id);
        }

        if (mapping.OdooOrderId is null)
        {
            draft.ShippingPartnerId = await ResolveShippingPartnerAsync(run, logEventId, order, draft);
            mapping.OdooOrderId = await _odooApiService.CreateSaleOrderAsync(OdooOrderBuilder.CreateValues(draft));
            mapping.Status = OdooOrderStatus.Created;
            mapping.Message = null;

            // Saved before confirming: a failed confirmation must not lead to a second order
            await SaveMappingAsync(mapping);
            created = true;
        }

        var saleOrder = await _odooApiService.GetSaleOrderAsync(mapping.OdooOrderId.Value)
            ?? throw new OdooApiException($"Odoo sale order {mapping.OdooOrderId} no longer exists.");

        if (saleOrder.State == "cancel")
            throw new OdooApiException($"Odoo sale order {saleOrder.Name} is cancelled in Odoo.");

        if (saleOrder.State is "draft" or "sent")
        {
            await _odooApiService.ConfirmSaleOrderAsync(saleOrder.Id);

            // An answer without an error is not proof that Odoo confirmed: read the state back
            saleOrder = await _odooApiService.GetSaleOrderAsync(saleOrder.Id)
                ?? throw new OdooApiException($"Odoo sale order {mapping.OdooOrderId} no longer exists.");
            if (saleOrder.State != "sale")
                throw new OdooApiException($"Odoo did not confirm {saleOrder.Name}: its state is '{saleOrder.State}'.");
        }

        // Compared with what nop charged, not with the rounded prices sent. A draft held now but created by
        // an earlier send has no complete nop total to compare.
        var warning = draft.HoldReason == null && Math.Abs(saleOrder.AmountUntaxed - draft.NopUntaxed) > 0.01m
            ? $"Odoo's total before tax {saleOrder.AmountUntaxed.ToString("0.00", CultureInfo.InvariantCulture)} differs from nop's {draft.NopUntaxed.ToString("0.00", CultureInfo.InvariantCulture)}."
            : null;

        mapping.Status = OdooOrderStatus.Confirmed;
        mapping.OdooOrderName = saleOrder.Name;
        mapping.Message = warning;
        await SaveMappingAsync(mapping);

        if (warning != null)
            await _odooLog.WarningAsync($"Order {order.CustomOrderNumber} → {saleOrder.Name}: {warning}",
                logEventId, entityType: "Order", odooEntityId: saleOrder.Id, nopEntityId: order.Id);

        await _odooLog.InformationAsync($"Order {order.CustomOrderNumber} → Odoo {saleOrder.Name} ({(created ? "created" : "existing")}, confirmed).",
            logEventId, entityType: "Order", odooEntityId: saleOrder.Id, nopEntityId: order.Id);

        return created ? SyncOutcome.Created : SyncOutcome.Updated;
    }

    /// <summary>
    /// Sends every destination of an order that is not yet confirmed.
    /// </summary>
    private async Task<SyncOutcome> SendOrderAsync(SendRun run, int logEventId, Order order)
    {
        if (order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
        {
            await _odooLog.InformationAsync($"Order {order.CustomOrderNumber} is deleted or cancelled in nop; not sent.",
                logEventId, entityType: "Order", nopEntityId: order.Id);
            return SyncOutcome.Skipped;
        }

        var mappings = (await _orderMappingRepository.Table.Where(mapping => mapping.NopOrderId == order.Id).ToListAsync())
            .ToDictionary(mapping => mapping.DestinationKey);

        var anyCreated = false;
        var anyConfirmed = false;

        var drafts = await BuildDraftsAsync(run, order);

        // A row for every destination before any is sent: if the run stops part-way, the destinations it did
        // not reach keep the order listed to send
        foreach (var draft in drafts.Where(draft => !mappings.ContainsKey(draft.DestinationKey)).ToList())
        {
            var pending = new OdooOrderMapping
            {
                NopOrderId = order.Id,
                DestinationKey = draft.DestinationKey,
                CreatedOnUtc = DateTime.UtcNow,
                Status = OdooOrderStatus.Pending
            };
            await SaveMappingAsync(pending);
            mappings[draft.DestinationKey] = pending;
        }

        foreach (var draft in drafts)
        {
            var mapping = mappings[draft.DestinationKey];

            if (mapping.Status is OdooOrderStatus.Confirmed or OdooOrderStatus.Cancelled)
                continue;

            // A draft held now but created by an earlier send is confirmed all the same
            if (draft.HoldReason != null && mapping.OdooOrderId is null)
            {
                mapping.Status = OdooOrderStatus.Held;
                mapping.Message = draft.HoldReason;
                await SaveMappingAsync(mapping);
                await _odooLog.WarningAsync($"Order {order.CustomOrderNumber} held: {draft.HoldReason}",
                    logEventId, entityType: "Order", nopEntityId: order.Id);
                continue;
            }

            try
            {
                var outcome = await SendDraftAsync(run, logEventId, order, draft, mapping);
                anyCreated |= outcome == SyncOutcome.Created;
                anyConfirmed |= outcome == SyncOutcome.Updated;
            }
            catch (Exception ex)
            {
                mapping.Status = OdooOrderStatus.Failed;
                mapping.Message = ex.Message;
                await SaveMappingAsync(mapping);
                await _odooLog.ErrorAsync($"Order {order.CustomOrderNumber}: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Order", odooEntityId: mapping.OdooOrderId, nopEntityId: order.Id);
            }
        }

        return anyCreated ? SyncOutcome.Created : anyConfirmed ? SyncOutcome.Updated : SyncOutcome.Skipped;
    }

    #endregion

    #region Listing

    /// <summary>
    /// The orders the Orders page lists with these filters: every order not deleted, and a deleted one while an
    /// Odoo order of it is still open (it can only be cancelled in Odoo from here).
    /// </summary>
    protected IQueryable<Order> FilterOrders(int storeId, DateTime? createdFromUtc, DateTime? createdToUtc,
        OdooOrderSyncFilter syncFilter, int paymentStatusId)
    {
        var pending = (int)OdooOrderStatus.Pending;
        var created = (int)OdooOrderStatus.Created;
        var confirmed = (int)OdooOrderStatus.Confirmed;
        var held = (int)OdooOrderStatus.Held;
        var failed = (int)OdooOrderStatus.Failed;
        var orderCancelled = (int)OrderStatus.Cancelled;

        var mappings = _orderMappingRepository.Table;
        var query = _orderRepository.Table.Where(order => !order.Deleted
            || mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.OdooOrderId != null
                && (mapping.StatusId == created || mapping.StatusId == confirmed || mapping.StatusId == failed)));

        if (storeId > 0)
            query = query.Where(order => order.StoreId == storeId);
        if (createdFromUtc.HasValue)
            query = query.Where(order => order.CreatedOnUtc >= createdFromUtc.Value);
        if (createdToUtc.HasValue)
            query = query.Where(order => order.CreatedOnUtc < createdToUtc.Value);
        if (paymentStatusId > 0)
            query = query.Where(order => order.PaymentStatusId == paymentStatusId);

        // Not sent, sent and needs attention are about live orders: a cancelled or deleted order reads as
        // "to cancel in Odoo", "cancelled in Odoo" or "not sent (cancelled)", and only All lists the last two
        return syncFilter switch
        {
            OdooOrderSyncFilter.NotSent => query.Where(order => !order.Deleted && order.OrderStatusId != orderCancelled
                && !mappings.Any(mapping => mapping.NopOrderId == order.Id)),
            OdooOrderSyncFilter.Sent => query.Where(order => !order.Deleted && order.OrderStatusId != orderCancelled
                && mappings.Any(mapping => mapping.NopOrderId == order.Id)
                && !mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.StatusId != confirmed)),
            OdooOrderSyncFilter.NeedsAttention => query.Where(order => !order.Deleted && order.OrderStatusId != orderCancelled
                && mappings.Any(mapping => mapping.NopOrderId == order.Id
                    && (mapping.StatusId == pending || mapping.StatusId == created || mapping.StatusId == held || mapping.StatusId == failed))),
            // A deleted order can no longer be cancelled in nop, so it counts as cancelled here
            OdooOrderSyncFilter.ToCancel => query.Where(order => (order.Deleted || order.OrderStatusId == orderCancelled)
                && mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.OdooOrderId != null
                    && (mapping.StatusId == created || mapping.StatusId == confirmed || mapping.StatusId == failed))),
            _ => query
        };
    }

    #endregion

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IPagedList<Order>> SearchOrdersAsync(int storeId = 0, DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        OdooOrderSyncFilter syncFilter = OdooOrderSyncFilter.All, int paymentStatusId = 0,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        return await FilterOrders(storeId, createdFromUtc, createdToUtc, syncFilter, paymentStatusId)
            .OrderByDescending(order => order.CreatedOnUtc)
            .ToPagedListAsync(pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<IList<int>> GetSendableOrderIdsAsync(int storeId = 0, DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        OdooOrderSyncFilter syncFilter = OdooOrderSyncFilter.All, int paymentStatusId = 0)
    {
        var confirmed = (int)OdooOrderStatus.Confirmed;
        var cancelled = (int)OdooOrderStatus.Cancelled;
        var orderCancelled = (int)OrderStatus.Cancelled;
        var mappings = _orderMappingRepository.Table;

        return await FilterOrders(storeId, createdFromUtc, createdToUtc, syncFilter, paymentStatusId)
            .Where(order => !order.Deleted && order.OrderStatusId != orderCancelled
                && (!mappings.Any(mapping => mapping.NopOrderId == order.Id)
                    || mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.StatusId != confirmed && mapping.StatusId != cancelled)))
            .OrderBy(order => order.CreatedOnUtc)
            .Select(order => order.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IList<OdooOrderMapping>> GetMappingsAsync(IList<int> orderIds)
    {
        if (orderIds == null || orderIds.Count == 0)
            return new List<OdooOrderMapping>();

        return await _orderMappingRepository.Table.Where(mapping => orderIds.Contains(mapping.NopOrderId)).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<OrderSyncResult> SendOrdersAsync(int logEventId, IReadOnlyList<int> orderIds)
    {
        var run = new SendRun
        {
            DeliveryProductId = await FindProductByCodeAsync(logEventId, _settings.DeliveryProductCode, "delivery"),
            FeeProductId = await FindProductByCodeAsync(logEventId, _settings.ExchangeFeeProductCode, "exchange fee")
        };
        var tally = new SyncTally();

        foreach (var orderId in (orderIds ?? Array.Empty<int>()).Distinct())
        {
            var outcome = SyncOutcome.Skipped;
            try
            {
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                    await _odooLog.WarningAsync($"Order {orderId} does not exist.", logEventId, entityType: "Order", nopEntityId: orderId);
                else
                    outcome = await SendOrderAsync(run, logEventId, order);
            }
            catch (Exception ex)
            {
                await _odooLog.ErrorAsync($"Order {orderId} could not be sent: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Order", nopEntityId: orderId);
            }
            finally
            {
                tally.Add(outcome);
            }
        }

        return new OrderSyncResult(tally.Created, tally.Updated, tally.Skipped);
    }

    /// <inheritdoc />
    public async Task<OrderSyncResult> CancelOrdersAsync(int logEventId, IReadOnlyList<int> orderIds)
    {
        var open = new[] { (int)OdooOrderStatus.Created, (int)OdooOrderStatus.Confirmed, (int)OdooOrderStatus.Failed };
        var tally = new SyncTally();

        foreach (var orderId in (orderIds ?? Array.Empty<int>()).Distinct())
        {
            var outcome = SyncOutcome.Skipped;
            try
            {
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null || (order.OrderStatus != OrderStatus.Cancelled && !order.Deleted))
                {
                    await _odooLog.WarningAsync($"Order {order?.CustomOrderNumber ?? orderId.ToString()} is neither cancelled nor deleted in nop; its Odoo orders are left alone.",
                        logEventId, entityType: "Order", nopEntityId: orderId);
                    continue;
                }

                var mappings = await _orderMappingRepository.Table
                    .Where(mapping => mapping.NopOrderId == orderId && mapping.OdooOrderId != null && open.Contains(mapping.StatusId))
                    .ToListAsync();

                foreach (var mapping in mappings)
                {
                    try
                    {
                        await _odooApiService.CancelSaleOrderAsync(mapping.OdooOrderId.Value);

                        // Odoo can answer with a confirmation step instead of cancelling: read the state back.
                        // An order that no longer exists in Odoo needs no cancelling.
                        var after = await _odooApiService.GetSaleOrderAsync(mapping.OdooOrderId.Value);
                        if (after != null && after.State != "cancel")
                            throw new OdooApiException($"its state is still '{after.State}'.");

                        mapping.Status = OdooOrderStatus.Cancelled;
                        mapping.Message = null;
                        await SaveMappingAsync(mapping);
                        await _odooLog.InformationAsync($"Order {order.CustomOrderNumber}: Odoo {mapping.OdooOrderName ?? mapping.OdooOrderId.ToString()} cancelled.",
                            logEventId, entityType: "Order", odooEntityId: mapping.OdooOrderId, nopEntityId: orderId);
                        outcome = SyncOutcome.Updated;
                    }
                    catch (Exception ex)
                    {
                        mapping.Message = $"Odoo refused the cancellation: {ex.Message}";
                        await SaveMappingAsync(mapping);
                        await _odooLog.ErrorAsync($"Order {order.CustomOrderNumber}: {mapping.Message}",
                            logEventId, fullMessage: ex.ToString(), entityType: "Order", odooEntityId: mapping.OdooOrderId, nopEntityId: orderId);
                    }
                }
            }
            catch (Exception ex)
            {
                await _odooLog.ErrorAsync($"Order {orderId} could not be cancelled in Odoo: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Order", nopEntityId: orderId);
            }
            finally
            {
                tally.Add(outcome);
            }
        }

        return new OrderSyncResult(tally.Created, tally.Updated, tally.Skipped);
    }

    #endregion
}
