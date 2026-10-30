using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Shipping;

namespace Nop.Plugin.CW.Core.Services.Orders;

public class CWOrderService : ICWOrderService
{
    #region Fields

    protected readonly IRepository<PackNameFullOrderItem> _packNameFullOrderItemRepository;
    protected readonly IRepository<PackNameOrderItem> _packNameOrderItemRepository;
    protected readonly IRepository<OrderShipping> _orderShippingRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IAddressService _addressService;
    protected readonly ICountryService _countryService;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IShipmentService _shipmentService;
    protected readonly IRepository<OrderSettlement> _orderSettlementRepository;

    #endregion

    #region Ctor

    public CWOrderService(IRepository<PackNameFullOrderItem> packNameFullOrderItemRepository,
        IRepository<PackNameOrderItem> packNameOrderItemRepository,
        IRepository<OrderShipping> orderShippingRepository,
        IRepository<Order> orderRepository,
        IStaticCacheManager staticCacheManager,
        IAddressService addressService,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        ILocalizationService localizationService,
        IShipmentService shipmentService,
        IRepository<OrderSettlement> orderSettlementRepository)
    {
        _packNameFullOrderItemRepository = packNameFullOrderItemRepository;
        _packNameOrderItemRepository = packNameOrderItemRepository;
        _orderShippingRepository = orderShippingRepository;
        _orderRepository = orderRepository;
        _staticCacheManager = staticCacheManager;
        _addressService = addressService;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _localizationService = localizationService;
        _shipmentService = shipmentService;
        _orderSettlementRepository = orderSettlementRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Builds the "City, State, PostCode" display string from an address.
    /// </summary>
    protected static string BuildCityStateZip(Address address, IReadOnlyDictionary<int, string> stateMap)
    {
        var parts = new List<string>(3);

        if (!string.IsNullOrEmpty(address.City))
            parts.Add(address.City);

        if (address.StateProvinceId.HasValue && stateMap.TryGetValue(address.StateProvinceId.Value, out var stateName))
            parts.Add(stateName);

        if (!string.IsNullOrEmpty(address.ZipPostalCode))
            parts.Add(address.ZipPostalCode);

        return string.Join(", ", parts);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a list of PackNameFullOrderItem by order id
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public virtual async Task<IList<PackNameFullOrderItem>> GetPackNameFullOrderItemsAsync(int orderId)
    {
        if (orderId == 0)
            return new List<PackNameFullOrderItem>();

        var query = _packNameFullOrderItemRepository.Table.Where(on => on.OrderId == orderId);

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets a PackNameOrderItem by order item id
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <returns></returns>
    public virtual async Task<PackNameOrderItem> GetPackNameOrderItemAsync(int orderItemId)
    {
        if (orderItemId == 0)
            return null;

        var query = _packNameOrderItemRepository.Table.Where(on => on.OrderItemId == orderItemId);

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Inserts a packname order item
    /// </summary>
    /// <param name="orderItem">Order item</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertPackNameOrderItemAsync(PackNameOrderItem orderItem)
    {
        await _packNameOrderItemRepository.InsertAsync(orderItem);
    }

    public virtual async Task<IList<OrderShipping>> GetOrderShippingAsync(int orderId)
    {
        if (orderId == 0)
            return new List<OrderShipping>();

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(OrderServicesDefaults.OrderShippingCacheKey, orderId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = _orderShippingRepository.Table.Where(on => on.OrderId == orderId);
            return await query.ToListAsync();
        });
    }

    /// <summary>
    /// Inserts an order shipping
    /// </summary>
    /// <param name="orderShipping">Order shipping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertOrderShippingAsync(OrderShipping orderShipping)
    {
        await _orderShippingRepository.InsertAsync(orderShipping);
    }

    /// <summary>
    /// Updates an order shipping
    /// </summary>
    /// <param name="orderShipping">Order shipping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdateOrderShippingAsync(OrderShipping orderShipping)
    {
        await _orderShippingRepository.UpdateAsync(orderShipping);
    }

    public virtual async Task<OrderSettlement> GetOrderSettlementAsync(int orderId)
    {
        if (orderId == 0)
            return null;

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(OrderServicesDefaults.OrderSettlementCacheKey, orderId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
            await _orderSettlementRepository.Table.FirstOrDefaultAsync(settlement => settlement.OrderId == orderId));
    }

    /// <summary>
    /// Inserts an order settlement
    /// </summary>
    /// <param name="orderSettlement">Order settlement</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertOrderSettlementAsync(OrderSettlement orderSettlement)
    {
        ArgumentNullException.ThrowIfNull(orderSettlement);

        await _orderSettlementRepository.InsertAsync(orderSettlement);
    }

    public virtual async Task<bool> IsPackNameOrderAsync(int orderId)
    {
        if (orderId == 0)
            return false;

        //Reuses the per-order cache GetPackNameOrderGroupsAsync already fills, so this is cheap enough
        //to call from a global filter on every admin order POST.
        return (await GetPackNameOrderGroupsAsync(orderId)).Count > 0;
    }

    public virtual async Task<bool> IsPackNameOrderAddressAsync(int addressId)
    {
        if (addressId == 0)
            return false;

        //One of the per-packname destinations. OrderShipping rows are written only for packname
        //orders, so the row's existence is the whole answer here.
        if (await _orderShippingRepository.Table
            .AnyAsync(os => os.ShippingAddressId == addressId || os.PickupAddressId == addressId))
            return true;

        //...and the packname order's OWN addresses, which are on the Order row and in no OrderShipping
        //row. Without this a packname order's BILLING address was editable by posting a NON-packname
        //order's id with this address id: ResolveOrderId answered with the innocent order, this method
        //answered "not a destination", and nop edited the address it was handed - it never ties
        //model.Address.Id to model.OrderId (OrderController:1739). Live D10 rewrote order 131's
        //billing address through order 130 before this half existed.
        return await (from order in _orderRepository.Table
                      join orderItem in _packNameFullOrderItemRepository.Table
                          on order.Id equals orderItem.OrderId
                      where order.BillingAddressId == addressId
                          || order.ShippingAddressId == addressId
                          || order.PickupAddressId == addressId
                      select order.Id)
            .AnyAsync();
    }

    /// <summary>
    /// Returns order items grouped by packname, with shipping, address, and pre-formatted
    /// address display fields loaded. Returns an empty list when the order has no packname items.
    /// </summary>
    public virtual async Task<IList<PackNameOrderGroupDTO>> GetPackNameOrderGroupsAsync(int orderId)
    {
        if (orderId == 0)
            return new List<PackNameOrderGroupDTO>();

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(OrderServicesDefaults.PackNameOrderGroupsCacheKey, orderId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var allItems = await GetPackNameFullOrderItemsAsync(orderId);
            if (allItems.Count == 0)
                return new List<PackNameOrderGroupDTO>();

            var allShipping = await GetOrderShippingAsync(orderId);

            // The addresses, states and countries of the destinations, each read once and one after another (the
            // services are scoped to this request and not written for concurrent use; the reads are cached)
            var uniqueAddressIds = allShipping
                .Where(s => s.ShippingAddressId.HasValue)
                .Select(s => s.ShippingAddressId!.Value)
                .Distinct()
                .ToList();

            var addressMap = new Dictionary<int, Address>();

            foreach (var addressId in uniqueAddressIds)
            {
                var address = await _addressService.GetAddressByIdAsync(addressId);

                if (address != null)
                    addressMap[addressId] = address;
            }

            var uniqueStateIds = addressMap.Values
                .Where(a => a.StateProvinceId.HasValue)
                .Select(a => a.StateProvinceId!.Value)
                .Distinct()
                .ToList();

            var uniqueCountryIds = addressMap.Values
                .Where(a => a.CountryId.HasValue)
                .Select(a => a.CountryId!.Value)
                .Distinct()
                .ToList();

            var stateMap = new Dictionary<int, string>();

            foreach (var stateId in uniqueStateIds)
            {
                var state = await _stateProvinceService.GetStateProvinceByIdAsync(stateId);

                if (state != null)
                    stateMap[stateId] = state.Name;
            }

            var countryMap = (await _countryService.GetCountriesByIdsAsync(uniqueCountryIds.ToArray()))
                .ToDictionary(country => country.Id, country => country.Name);

            // --- Shipping lookup keyed by PackNameId ---
            var shippingByPackName = allShipping
                .Where(s => s.PackNameId.HasValue)
                .ToDictionary(s => s.PackNameId!.Value);

            // --- Build groups ---
            var groups = allItems
                .GroupBy(i => i.PackNameId)
                .Select(g =>
                {
                    var first = g.First();
                    shippingByPackName.TryGetValue(g.Key, out var shipping);

                    Address address = null;
                    if (shipping?.ShippingAddressId.HasValue == true)
                        addressMap.TryGetValue(shipping.ShippingAddressId.Value, out address);

                    string addressName = null;
                    string line1 = null;
                    string line2 = null;
                    string cityStateZip = null;
                    string country = null;
                    string phone = null;

                    if (address != null)
                    {
                        addressName = $"{address.FirstName} {address.LastName}".Trim();
                        line1 = address.Address1;
                        line2 = address.Address2;
                        cityStateZip = BuildCityStateZip(address, stateMap);
                        country = address.CountryId.HasValue && countryMap.TryGetValue(address.CountryId.Value, out var cn) ? cn : null;
                        phone = address.PhoneNumber;
                    }

                    return new PackNameOrderGroupDTO
                    {
                        PackNameId = g.Key,
                        PackName = first.PackName,
                        Branch = first.Branch,
                        BranchCode = first.BranchCode,
                        Department = first.Department,
                        CostCenter = first.CostCenter,
                        Items = g.ToList(),
                        Shipping = shipping,
                        ShippingAddress = address,
                        AddressName = addressName,
                        AddressLine1 = line1,
                        AddressLine2 = line2,
                        AddressCityStateZip = cityStateZip,
                        AddressCountry = country,
                        AddressPhone = phone,
                    };
                })
                .ToList();

            // Determine shared shipping: same ShippingAddressId + ShippingMethod
            var sharedGroups = groups
                .Where(g => g.Shipping != null)
                .GroupBy(g => (g.Shipping.ShippingAddressId, g.Shipping.ShippingMethod))
                .Where(grp => grp.Count() > 1);

            foreach (var sharedGroup in sharedGroups)
            {
                foreach (var dto in sharedGroup)
                {
                    dto.IsSharedShipping = true;
                    dto.SharedWithPackNames = sharedGroup
                        .Where(x => x.PackNameId != dto.PackNameId)
                        .Select(x => x.PackName)
                        .ToList();
                }
            }

            return (IList<PackNameOrderGroupDTO>)groups;
        });
    }

    public virtual async Task<IList<OrderShippingDestination>> GetOrderShippingDestinationsAsync(int orderId)
    {
        //GetPackNameOrderGroupsAsync is cached per order, so this is cheap to call from several screens.
        var groups = await GetPackNameOrderGroupsAsync(orderId);

        if (groups.Count == 0)
            return [];

        //Resolved here, once, so the destinations table, the Add-shipment buttons, the grid column and
        //the shipment badge all say the same thing — and say it in the admin's language.
        var pickupText = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Orders.ShippingDestinations.Pickup");
        var noAddressText = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Orders.ShippingDestinations.NoAddress");

        var destinations = groups
            .GroupBy(g => (
                g.Shipping?.ShippingAddressId,
                PickupInStore: g.Shipping?.PickupInStore ?? false,
                g.Shipping?.ShippingMethod))
            .Select(grp =>
            {
                var first = grp.First();

                return new
                {
                    Group = grp,
                    Destination = new OrderShippingDestination
                    {
                        Key = grp.Min(g => g.PackNameId),
                        ShippingAddressId = grp.Key.ShippingAddressId,
                        PickupInStore = grp.Key.PickupInStore,
                        ShippingMethod = grp.Key.ShippingMethod,
                        AddressSummary = BuildAddressSummary(first, grp.Key.PickupInStore, pickupText, noAddressText),
                        PackNames = grp.Select(g => g.PackName).ToList(),
                        PackNameIds = grp.Select(g => g.PackNameId).ToList(),
                        OrderItemIds = grp.SelectMany(g => g.Items).Select(i => i.Id).Distinct().ToList(),
                    }
                };
            })
            .OrderBy(x => x.Destination.Key)
            .ToList();

        foreach (var entry in destinations)
        {
            //least advanced wins — see the remark on OrderShippingDestination.ShippingStatus
            var status = entry.Group
                .Select(g => g.Shipping?.ShippingStatus ?? ShippingStatus.NotYetShipped)
                .Min();

            entry.Destination.ShippingStatus = await _localizationService.GetLocalizedEnumAsync(status);
        }

        return destinations.Select(x => x.Destination).ToList();

        //Always returns something displayable, so no caller has to branch on pickup or on emptiness.
        static string BuildAddressSummary(PackNameOrderGroupDTO group, bool pickupInStore, string pickupText, string noAddressText)
        {
            if (pickupInStore)
                return pickupText;

            var parts = new[] { group.AddressName, group.AddressLine1, group.AddressCityStateZip }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            return parts.Count > 0 ? string.Join(", ", parts) : noAddressText;
        }
    }

    public virtual async Task<OrderShippingDestination> GetShipmentDestinationAsync(int shipmentId, int orderId)
    {
        if (shipmentId <= 0 || orderId <= 0)
            return null;

        var destinations = await GetOrderShippingDestinationsAsync(orderId);

        //No destinations means this is not a packname order, so there is nothing to resolve and every
        //caller should fall back to nop's order-level address.
        if (destinations.Count == 0)
            return null;

        var shipmentItems = await _shipmentService.GetShipmentItemsByShipmentIdAsync(shipmentId);
        var orderItemIds = shipmentItems.Select(item => item.OrderItemId).ToHashSet();

        //AddShipmentDestinationFilter refuses a shipment that would span two destinations, so a shipment
        //belongs to exactly one and the first match is the only match.
        return destinations.FirstOrDefault(destination => destination.OrderItemIds.Any(orderItemIds.Contains));
    }

    #endregion
}
