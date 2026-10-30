using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.CreditNotes;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Controllers
{
    public class CreditNotesController : BasePublicController
    {
        #region Fields

        protected readonly IWorkContext _workContext;
        protected readonly ICustomerService _customerService;
        protected readonly IPriceFormatter _priceFormatter;
        protected readonly ICreditNotesService _creditNotesService;
        protected readonly IDateTimeHelper _dateTimeHelper;
        protected readonly ICWFeatureService _cwFeatureService;
        protected readonly ICurrencyService _currencyService;

        #endregion

        #region Ctor

        public CreditNotesController(
            IWorkContext workContext,
            IPriceFormatter priceFormatter,
            ICreditNotesService creditNotesService,
            ICustomerService customerService,
            IDateTimeHelper dateTimeHelper,
            ICWFeatureService cwFeatureService,
            ICurrencyService currencyService)
        {
            _workContext = workContext;
            _priceFormatter = priceFormatter;
            _creditNotesService = creditNotesService;
            _customerService = customerService;
            _dateTimeHelper = dateTimeHelper;
            _cwFeatureService = cwFeatureService;
            _currencyService = currencyService;
        }

        #endregion

        #region Utilities

        protected virtual async Task<StoreCreditsModel> PrepareMyStoreCreditModelAsync(int customerId)
        {
            var model = new StoreCreditsModel();
            var section = model.StoreCredit;

            //amounts are stored in the primary store currency: shown in the customer's currency, as the cart shows them
            var workingCurrency = await _workContext.GetWorkingCurrencyAsync();

            async Task<string> formatAsync(decimal amount) =>
                await _priceFormatter.FormatPriceAsync(await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(amount, workingCurrency), true, false);

            // Positive-balance notes (FIFO, oldest first) drive both the balance and the "issued" list
            var creditNotes = await _creditNotesService.GetOpenCreditNotesByCustomerIdAsync(customerId);
            var histories = await _creditNotesService.GetCreditNoteHistoriesByCustomerIdAsync(customerId);

            section.HasStoreCredit = creditNotes.Any() || histories.Any();
            section.AvailableBalance = await formatAsync(creditNotes.Sum(c => c.Remain));

            foreach (var note in creditNotes)
            {
                section.IssuedCreditNotes.Add(new StoreCreditsModel.CreditNoteLineModel
                {
                    Name = note.Name,
                    OrderId = note.OrderId,
                    CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(note.CreatedOnUtc, DateTimeKind.Utc)).ToString("g"),
                    Amount = await formatAsync(note.Amount),
                    Remaining = await formatAsync(note.Remain)
                });
            }

            //the ledger names each line's credit note: every note of the customer, read once
            var creditNoteNames = histories.Count == 0
                ? new Dictionary<int, string>()
                : (await _creditNotesService.SearchCreditNotesByCustomerIdAsync(customerId)).ToDictionary(note => note.Id, note => note.Name);

            foreach (var history in histories.OrderByDescending(history => history.CreatedOnUtc).ThenByDescending(history => history.Id))
            {
                section.Ledger.Add(new StoreCreditsModel.CreditLedgerEntryModel
                {
                    Name = creditNoteNames.GetValueOrDefault(history.CreditNoteId),
                    OrderId = history.OrderId,
                    CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(history.CreatedOnUtc, DateTimeKind.Utc)).ToString("g"),
                    AmountApplied = await formatAsync(history.Amount)
                });
            }

            return model;
        }

        #endregion

        #region Methods

        [CheckAccessPublicStore(ignore: false)]
        public virtual async Task<IActionResult> CreditsForMe()
        {
            var customer = await _workContext.GetCurrentCustomerAsync();

            if (!await _customerService.IsRegisteredAsync(customer))
                return Challenge();

            if (!await _cwFeatureService.IsEnabledAsync(CWFeature.CreditNotes) && !await _creditNotesService.CustomerHasCreditNotesAsync(customer.Id))
                return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

            var model = await PrepareMyStoreCreditModelAsync(customer.Id);

            return View(model);
        }

        #endregion
    }
}
