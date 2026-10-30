using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Services.Stores;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Model Factory for Models related to the Credit Notes plugin
/// </summary>
public class CreditNotesModelFactory : ICreditNotesModelFactory
{
    #region Fields

    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IWorkContext _workContext;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IOrderService _orderService;
    protected readonly IStoreService _storeService;
    protected readonly ICustomerService _customerService;


    #endregion

    #region Ctor

    public CreditNotesModelFactory(ICreditNotesService creditNotesService,
        IWorkContext workContext,
        IPriceFormatter priceFormatter,
        IDateTimeHelper dateTimeHelper,
        IOrderService orderService,
        IStoreService storeService,
        ICustomerService customerService)
    {
        _creditNotesService = creditNotesService;
        _workContext = workContext;
        _priceFormatter = priceFormatter;
        _dateTimeHelper = dateTimeHelper;
        _orderService = orderService;
        _storeService = storeService;
        _customerService = customerService;
    }

    #endregion

    #region Utilities

    protected CreditNoteModel PrepareCreditNoteModel(CreditNote creditNote)
    {
        return new CreditNoteModel
        {
            Id = creditNote.Id,
            Amount = creditNote.Amount,
            CreatedOnUtc = creditNote.CreatedOnUtc,
            CustomerId = creditNote.CustomerId,
            Name = creditNote.Name,
            OrderId = creditNote.OrderId,
            Reason = creditNote.ReturnReason,
            Remain = creditNote.Remain,
            ReturnRequestId = creditNote.ReturnRequestId
        };
    }

    protected CreditNoteHistoryModel PrepareCreditNoteHistoryRecordModel(CreditNoteHistory creditNoteHistoryItem)
    {
        return new CreditNoteHistoryModel
        {
            Id = creditNoteHistoryItem.Id,
            Amount = creditNoteHistoryItem.Amount,
            CreatedOnUtc = creditNoteHistoryItem.CreatedOnUtc,
            CreditNoteId = creditNoteHistoryItem.CreditNoteId,
            CustomerId = creditNoteHistoryItem.CustomerId,
            OrderId = creditNoteHistoryItem.OrderId,
            Remain = creditNoteHistoryItem.Remain
        };
    }

    #endregion

    #region Methods

    public virtual async Task<CreditNoteListModel> PrepareCreditNoteListModelAsync(CreditNoteSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var creditNotes = await _creditNotesService.SearchCreditNotesByCustomerIdAsync(
            searchModel.CustomerId, searchModel.Page - 1, searchModel.PageSize);

        var model = await new CreditNoteListModel().PrepareToGridAsync(searchModel, creditNotes, () =>
        {
            return creditNotes.SelectAwait(async creditNote => new CreditNoteModel
            {
                Id = creditNote.Id,
                CustomerId = creditNote.CustomerId,
                OrderId = creditNote.OrderId,
                Reason = creditNote.ReturnReason,
                Amount = creditNote.Amount,
                Remain = creditNote.Remain,
                ReturnRequestId = creditNote.ReturnRequestId,
                AmountFormated = await _priceFormatter.FormatPriceAsync(creditNote.Amount, true, false),
                RemainFormated = await _priceFormatter.FormatPriceAsync(creditNote.Remain, true, false),
                CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(creditNote.CreatedOnUtc, DateTimeKind.Utc)).ToString("g"),
                CreatedOnUtc = creditNote.CreatedOnUtc
            });
        });

        return model;
    }

    public virtual async Task<CreditNoteHistoryListModel> PrepareCreditNoteHistoryListModelAsync(CreditNoteHistorySearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var history = await _creditNotesService.GetCreditNoteHistoriesByCreditNoteAsync(searchModel.CreditNoteId);
        var paged = new PagedList<CreditNoteHistory>(history.ToList(), searchModel.Page - 1, searchModel.PageSize);

        var model = await new CreditNoteHistoryListModel().PrepareToGridAsync(searchModel, paged, () =>
        {
            return paged.SelectAwait(async h => new CreditNoteHistoryModel
            {
                Id = h.Id,
                CreditNoteId = h.CreditNoteId,
                OrderId = h.OrderId,
                Amount = h.Amount,
                Remain = h.Remain,
                AmountFormated = await _priceFormatter.FormatPriceAsync(h.Amount, true, false),
                RemainFormated = await _priceFormatter.FormatPriceAsync(h.Remain, true, false),
                CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(h.CreatedOnUtc, DateTimeKind.Utc)).ToString("g")
            });
        });

        return model;
    }

    public virtual async Task<CreditNoteAddFormModel> PrepareCreditNoteAddFormModelAsync(int? customerId = null)
    {
        var currentCustomer = customerId ?? (await _workContext.GetCurrentCustomerAsync()).Id;

        var model = new CreditNoteAddFormModel { CustomerId = currentCustomer };

        //var orders = _bulkOrderService?.GetOrdersForCustomer(model.CustomerId);
        var orders = await _orderService.SearchOrdersAsync(customerId: currentCustomer);
        model.AvailableOrders = orders
            .Select(o => new SelectListItem { Text = $"#{o.CustomOrderNumber}", Value = o.Id.ToString() })
            .ToList();

        return model;
    }


    public virtual async Task<CreditNotesCustomerSummaryModel> PrepareCreditNotesCustomerSummaryModelAsync(int customerId)
    {
        var cn = new CreditNotesCustomerSummaryModel
        {
            CustomerId = customerId,
        };

        var creditNotes = await _creditNotesService.GetOpenCreditNotesByCustomerIdAsync(customerId);
        if (creditNotes == null)
            return cn;

        var total = creditNotes.Sum(c => c.Remain);
        cn.CreditNotesAvailableAmount = total;
        cn.CreditNotesAvailableAmountFormated = await _priceFormatter.FormatPriceAsync(total, true, false);

        return cn;
    }

    #endregion


    #region Liability report

    public virtual async Task<CreditNotesMainReportSearchModel> PrepareCreditNotesReportModelAsync()
    {
        var stores = await _storeService.GetAllStoresAsync();
        var numberOfCreditNotesWithBalance = 0;
        var totalCreditNotesValue = decimal.Zero;
        foreach (var store in stores)
        {
            var withBalance = (await _creditNotesService.GetCreditNotesByStoreAsync(store.Id)).Where(c => c.Remain > 0).ToList();
            numberOfCreditNotesWithBalance += withBalance.Count;
            totalCreditNotesValue += withBalance.Sum(c => c.Remain);
        }

        return new CreditNotesMainReportSearchModel
        {
            CreditNotesWithBalance = numberOfCreditNotesWithBalance,
            TotalCreditNotesValue = await _priceFormatter.FormatPriceAsync(totalCreditNotesValue, true, false)
        };
    }

    public virtual async Task<StoreLiabilityListModel> PrepareStoreLiabilityListModelAsync(StoreLiabilitySearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var storesLiabilityData = (await StoresLiabilityDataAsync()).ToPagedList(searchModel);

        var gridModel = await new StoreLiabilityListModel().PrepareToGridAsync(searchModel, storesLiabilityData, () => storesLiabilityData.ToAsyncEnumerable()); 


        return gridModel;

    }

    protected async Task<IList<StoreLiabilityModel>> StoresLiabilityDataAsync()
    {
        var stores = await _storeService.GetAllStoresAsync();
        var storesLiabilityData = new List<StoreLiabilityModel>();
        foreach (var store in stores)
        {
            var creditNotes = await _creditNotesService.GetCreditNotesByStoreAsync(store.Id);
            var issuedValue = creditNotes.Sum(c => c.Amount);
            var liabilityValue = creditNotes.Sum(c => c.Remain);
            var percentageUsed = issuedValue > 0 ? (issuedValue - liabilityValue) / issuedValue * 100 : decimal.Zero;

            storesLiabilityData.Add(new StoreLiabilityModel
            {
                Id = store.Id,
                StoreId = store.Id,
                StoreName = store.Name,
                CreditNotesIssued = creditNotes.Count,
                CreditNotesConsumed = creditNotes.Count(c => c.Remain == 0),
                IssuedCreditNotesValue = issuedValue,
                IssuedCreditNotesValueStr = await _priceFormatter.FormatPriceAsync(issuedValue, true, false),
                LiabilityValue = liabilityValue,
                LiabilityValueStr = await _priceFormatter.FormatPriceAsync(liabilityValue, true, false),
                PercentageUsedStr = $"{percentageUsed:0.##}%",
                PercentageUsed = percentageUsed
            });
        }

        return storesLiabilityData;
    }


    public virtual async Task<StoreCustomerCreditNotesListModel> PrepareStoreCustomerCreditNotesListModelAsync(StoreLiabilitySearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var customersCreditNotesList = (await CustomersCreditNotesDetailsAsync(searchModel)).ToPagedList(searchModel);

        var gridModel = await new StoreCustomerCreditNotesListModel().PrepareToGridAsync(searchModel, customersCreditNotesList, () =>
        {
            return customersCreditNotesList.SelectAwait(async c => new StoreCustomerCreditNotesModel
            {
                CustomerName = c.CustomerName,
                NumberOfAvailableCreditNotes = c.NumberOfAvailableCreditNotes,
                CreditNotesValueStr = await _priceFormatter.FormatPriceAsync(c.CreditNotesValue, true, false),
                CreditNotesValue = c.CreditNotesValue
            });
        });



        return gridModel;
    }

    private async Task<IList<StoreCustomerCreditNotesModel>> CustomersCreditNotesDetailsAsync(StoreLiabilitySearchModel searchModel)
    {
        var creditNotes = (await _creditNotesService.GetCreditNotesByStoreAsync(searchModel.StoreId)).Where(c => c.Remain > 0).ToList();

        var customersCreditNotesDict = new Dictionary<int, StoreCustomerCreditNotesModel>();

        foreach (var creditNote in creditNotes)
        {
            var customerId = creditNote.CustomerId;
            if (!customersCreditNotesDict.TryGetValue(customerId, out StoreCustomerCreditNotesModel customerCreditNotesData))
            {
                var customer = await _customerService.GetCustomerByIdAsync(customerId);
                customersCreditNotesDict.Add(customerId, new StoreCustomerCreditNotesModel
                {
                    CreditNotesValue = creditNote.Remain,
                    CustomerName = await _customerService.GetCustomerFullNameAsync(customer),
                    NumberOfAvailableCreditNotes = creditNotes.Where(c => c.CustomerId == customerId).Count()
                });
            }
            else
            {
                customerCreditNotesData.CreditNotesValue += creditNote.Remain;
            }
        }

        return customersCreditNotesDict.Values.ToList();
    }

    #endregion

}