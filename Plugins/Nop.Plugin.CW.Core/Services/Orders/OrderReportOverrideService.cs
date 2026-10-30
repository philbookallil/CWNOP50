using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Orders;

public class OrderReportOverrideService : OrderReportService
{
    #region Fields

    protected readonly IRepository<OrderSettlement> _orderSettlementRepository;

    #endregion

    #region Ctor

    public OrderReportOverrideService(CurrencySettings currencySettings,
        ICurrencyService currencyService,
        IDateTimeHelper dateTimeHelper,
        IPriceFormatter priceFormatter,
        IRepository<Address> addressRepository,
        IRepository<Order> orderRepository,
        IRepository<OrderItem> orderItemRepository,
        IRepository<OrderNote> orderNoteRepository,
        IRepository<Product> productRepository,
        IRepository<ProductCategory> productCategoryRepository,
        IRepository<ProductManufacturer> productManufacturerRepository,
        IRepository<ProductWarehouseInventory> productWarehouseInventoryRepository,
        IStoreMappingService storeMappingService,
        IWorkContext workContext,
        IRepository<OrderSettlement> orderSettlementRepository) : base(currencySettings,
            currencyService,
            dateTimeHelper,
            priceFormatter,
            addressRepository,
            orderRepository,
            orderItemRepository,
            orderNoteRepository,
            productRepository,
            productCategoryRepository,
            productManufacturerRepository,
            productWarehouseInventoryRepository,
            storeMappingService,
            workContext)
    {
        _orderSettlementRepository = orderSettlementRepository;
    }

    #endregion

    #region Overrides

    public override async Task<decimal> ProfitReportAsync(int storeId = 0, int vendorId = 0, int productId = 0,
        int warehouseId = 0, int billingCountryId = 0, int orderId = 0, string paymentMethodSystemName = null,
        List<int> osIds = null, List<int> psIds = null, List<int> ssIds = null,
        DateTime? startTimeUtc = null, DateTime? endTimeUtc = null,
        string billingPhone = null, string billingEmail = null, string billingLastName = "", string orderNotes = null)
    {
        var rawProfit = await base.ProfitReportAsync(storeId, vendorId, productId, warehouseId, billingCountryId, orderId, paymentMethodSystemName, osIds, psIds, ssIds, startTimeUtc, endTimeUtc, billingPhone, billingEmail, billingLastName, orderNotes);

        var orders = _orderRepository.Table;
        orders = orders.Where(o => !o.Deleted);

        if (storeId > 0)
            orders = orders.Where(o => o.StoreId == storeId);
        if (orderId > 0)
            orders = orders.Where(o => o.Id == orderId);

        if (osIds != null && osIds.Any())
            orders = orders.Where(o => osIds.Contains(o.OrderStatusId));
        if (psIds != null && psIds.Any())
            orders = orders.Where(o => psIds.Contains(o.PaymentStatusId));
        if (ssIds != null && ssIds.Any())
            orders = orders.Where(o => ssIds.Contains(o.ShippingStatusId));


        if (vendorId > 0)
        {
            orders = from o in orders
                     join oi in _orderItemRepository.Table on o.Id equals oi.OrderId
                     join p in _productRepository.Table on oi.ProductId equals p.Id
                     where p.VendorId == vendorId
                     select o;

            orders = orders.Distinct();
        }

        if (productId > 0)
        {
            orders = from o in orders
                     join oi in _orderItemRepository.Table on o.Id equals oi.OrderId
                     where oi.ProductId == productId
                     select o;

            orders = orders.Distinct();
        }

        if (warehouseId > 0)
        {
            var manageStockInventoryMethodId = (int)ManageInventoryMethod.ManageStock;

            orders = from o in orders
                     join oi in _orderItemRepository.Table on o.Id equals oi.OrderId
                     join p in _productRepository.Table on oi.ProductId equals p.Id
                     join pwi in _productWarehouseInventoryRepository.Table on p.Id equals pwi.ProductId
                     where
                         //"Use multiple warehouses" enabled
                         //we search in each warehouse
                         (p.ManageInventoryMethodId == manageStockInventoryMethodId && p.UseMultipleWarehouses && pwi.WarehouseId == warehouseId) ||
                         //"Use multiple warehouses" disabled
                         //we use standard "warehouse" property
                         ((p.ManageInventoryMethodId != manageStockInventoryMethodId || !p.UseMultipleWarehouses) && p.WarehouseId == warehouseId)
                     select o;

            orders = orders.Distinct();
        }

        orders = from o in orders
                 join oba in _addressRepository.Table on o.BillingAddressId equals oba.Id
                 where
                     (billingCountryId <= 0 || (oba.CountryId == billingCountryId)) &&
                     (string.IsNullOrEmpty(billingPhone) || (!string.IsNullOrEmpty(oba.PhoneNumber) && oba.PhoneNumber.Contains(billingPhone))) &&
                     (string.IsNullOrEmpty(billingEmail) || (!string.IsNullOrEmpty(oba.Email) && oba.Email.Contains(billingEmail))) &&
                     (string.IsNullOrEmpty(billingLastName) || (!string.IsNullOrEmpty(oba.LastName) && oba.LastName.Contains(billingLastName)))
                 select o;

        if (!string.IsNullOrEmpty(paymentMethodSystemName))
            orders = orders.Where(o => o.PaymentMethodSystemName == paymentMethodSystemName);

        if (startTimeUtc.HasValue)
            orders = orders.Where(o => startTimeUtc.Value <= o.CreatedOnUtc);

        if (endTimeUtc.HasValue)
            orders = orders.Where(o => endTimeUtc.Value >= o.CreatedOnUtc);

        if (!string.IsNullOrEmpty(orderNotes))
        {
            orders = from o in orders
                     join n in _orderNoteRepository.Table on o.Id equals n.OrderId
                     where n.Note.Contains(orderNotes)
                     select o;

            orders = orders.Distinct();
        }

        //nop's profit is built on OrderTotal, which is what the customer paid. The subsidy the employer paid
        //for the same goods is revenue too, so it is added back from the orders' settlement rows. Store credit
        //is not: a credit note is money the store already received for the returned goods.
        var subsidyQuery = from settlement in _orderSettlementRepository.Table
                           join o in orders on settlement.OrderId equals o.Id
                           select settlement;

        var totalSubsidy = await subsidyQuery.SumAsync(s => (decimal?)(s.SubsidyStockAmount + s.SubsidyShippingAmount)) ?? 0;

        return rawProfit + totalSubsidy;
    }

    #endregion
}