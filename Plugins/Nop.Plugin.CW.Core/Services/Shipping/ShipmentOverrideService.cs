using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Pickup;

namespace Nop.Plugin.CW.Core.Services.Shipping;

/// <summary>
/// Makes the admin shipment list's location filters test the address a shipment actually went to.
/// </summary>
/// <remarks>
/// <para>
/// nop's four location filters join the order and test <c>Order.ShippingAddressId</c>, which on a packname
/// order is the first packname's address. Filtering by a branch's city therefore misses the shipments that
/// went there and returns ones that did not. Nothing is displayed wrongly — the list has no address column
/// — so this is a mis-filter rather than a false statement, which is why it is the smallest item in the
/// phase.
/// </para>
/// <para>
/// The whole method is copied because the filters live inside a lambda handed to
/// <c>GetAllPagedAsync</c> and there is no narrower hook. <b>Only the four location clauses differ</b>;
/// everything else is nop's, line for line. Each of the four now tests
/// <c>vw_CORE_PN_ShipmentAddress</c>, which resolves to the order's own address on a non-packname order —
/// so ordinary orders filter exactly as before.
/// </para>
/// </remarks>
public class ShipmentOverrideService : ShipmentService
{
    #region Fields

    protected readonly IRepository<ShipmentAddress> _shipmentAddressRepository;

    #endregion

    #region Ctor

    public ShipmentOverrideService(IPickupPluginManager pickupPluginManager,
        IRepository<Address> addressRepository,
        IRepository<Order> orderRepository,
        IRepository<OrderItem> orderItemRepository,
        IRepository<Product> productRepository,
        IRepository<Shipment> shipmentRepository,
        IRepository<ShipmentItem> siRepository,
        IShippingPluginManager shippingPluginManager,
        IRepository<ShipmentAddress> shipmentAddressRepository)
        : base(pickupPluginManager,
            addressRepository,
            orderRepository,
            orderItemRepository,
            productRepository,
            shipmentRepository,
            siRepository,
            shippingPluginManager)
    {
        _shipmentAddressRepository = shipmentAddressRepository;
    }

    #endregion

    #region Methods

    public override async Task<IPagedList<Shipment>> GetAllShipmentsAsync(int vendorId = 0, int warehouseId = 0,
        int shippingCountryId = 0,
        int shippingStateId = 0,
        string shippingCounty = null,
        string shippingCity = null,
        string trackingNumber = null,
        bool loadNotShipped = false,
        bool loadNotReadyForPickup = false,
        bool loadNotDelivered = false,
        int orderId = 0,
        DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var shipments = await _shipmentRepository.GetAllPagedAsync(query =>
        {
            if (orderId > 0)
                query = query.Where(o => o.OrderId == orderId);

            if (!string.IsNullOrEmpty(trackingNumber))
                query = query.Where(s => s.TrackingNumber.Contains(trackingNumber));

            //--- the four location filters, resolved per shipment rather than per order ---

            if (shippingCountryId > 0)
                query = from s in query
                        where _shipmentAddressRepository.Table.Any(sa => sa.ShipmentId == s.Id &&
                            _addressRepository.Table.Any(a => a.Id == sa.AddressId && a.CountryId == shippingCountryId))
                        select s;

            if (shippingStateId > 0)
                query = from s in query
                        where _shipmentAddressRepository.Table.Any(sa => sa.ShipmentId == s.Id &&
                            _addressRepository.Table.Any(a => a.Id == sa.AddressId && a.StateProvinceId == shippingStateId))
                        select s;

            if (!string.IsNullOrWhiteSpace(shippingCounty))
                query = from s in query
                        where _shipmentAddressRepository.Table.Any(sa => sa.ShipmentId == s.Id &&
                            _addressRepository.Table.Any(a => a.Id == sa.AddressId && a.County.Contains(shippingCounty)))
                        select s;

            if (!string.IsNullOrWhiteSpace(shippingCity))
                query = from s in query
                        where _shipmentAddressRepository.Table.Any(sa => sa.ShipmentId == s.Id &&
                            _addressRepository.Table.Any(a => a.Id == sa.AddressId && a.City.Contains(shippingCity)))
                        select s;

            //--- everything below is nop's, unchanged ---

            if (loadNotShipped)
                query = from s in query
                        join o in _orderRepository.Table on s.OrderId equals o.Id
                        where !s.ShippedDateUtc.HasValue && !o.PickupInStore
                        select s;

            if (loadNotReadyForPickup)
                query = from s in query
                        join o in _orderRepository.Table on s.OrderId equals o.Id
                        where !s.ReadyForPickupDateUtc.HasValue && o.PickupInStore
                        select s;

            if (loadNotDelivered)
                query = query.Where(s => !s.DeliveryDateUtc.HasValue);

            if (createdFromUtc.HasValue)
                query = query.Where(s => createdFromUtc.Value <= s.CreatedOnUtc);

            if (createdToUtc.HasValue)
                query = query.Where(s => createdToUtc.Value >= s.CreatedOnUtc);

            query = from s in query
                    join o in _orderRepository.Table on s.OrderId equals o.Id
                    where !o.Deleted
                    select s;

            query = query.Distinct();

            if (vendorId > 0)
            {
                var queryVendorOrderItems = from orderItem in _orderItemRepository.Table
                                            join p in _productRepository.Table on orderItem.ProductId equals p.Id
                                            where p.VendorId == vendorId
                                            select orderItem.Id;

                query = from s in query
                        join si in _siRepository.Table on s.Id equals si.ShipmentId
                        where queryVendorOrderItems.Contains(si.OrderItemId)
                        select s;

                query = query.Distinct();
            }

            if (warehouseId > 0)
            {
                query = from s in query
                        join si in _siRepository.Table on s.Id equals si.ShipmentId
                        where si.WarehouseId == warehouseId
                        select s;

                query = query.Distinct();
            }

            query = query.OrderByDescending(s => s.CreatedOnUtc);

            return query;
        }, pageIndex, pageSize);

        return shipments;
    }

    #endregion
}