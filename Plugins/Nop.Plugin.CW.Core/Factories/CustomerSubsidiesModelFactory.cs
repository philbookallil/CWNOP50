using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Builds the customer's subsidy page from the reporting queries the admin screens use, so the customer and
/// the store read the same figures.
/// </summary>
public class CustomerSubsidiesModelFactory : ICustomerSubsidiesModelFactory
{
    #region Fields

    protected readonly ISubsidyReportingService _subsidyReportingService;
    protected readonly IPackNameService _packNameService;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IWorkContext _workContext;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderService _orderService;
    protected readonly ICurrencyService _currencyService;

    #endregion

    #region Ctor

    public CustomerSubsidiesModelFactory(ISubsidyReportingService subsidyReportingService,
        IPackNameService packNameService,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        IPriceFormatter priceFormatter,
        IWorkContext workContext,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IOrderService orderService,
        ICurrencyService currencyService)
    {
        _subsidyReportingService = subsidyReportingService;
        _packNameService = packNameService;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _priceFormatter = priceFormatter;
        _workContext = workContext;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _orderService = orderService;
        _currencyService = currencyService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Formats a money amount, stored in the primary store currency, in the working currency; a negative amount comes out
    /// the way the currency writes it.
    /// </summary>
    protected virtual async Task<string> FormatAsync(decimal amount)
    {
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;
        var amountInWorkingCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(amount, currency);

        return await _priceFormatter.FormatPriceAsync(amountInWorkingCurrency, true, currency, languageId, true, false);
    }

    /// <summary>
    /// Formats a signed money movement: "+ $30.00" for money given, the currency's own negative for money taken,
    /// empty when nothing moved.
    /// </summary>
    protected virtual async Task<string> FormatMovementAsync(decimal amount)
    {
        if (amount == decimal.Zero)
            return string.Empty;

        return amount > decimal.Zero
            ? $"+ {await FormatAsync(amount)}"
            : await FormatAsync(amount);
    }

    /// <summary>
    /// Formats a signed unit movement: "+ 5", "− 2", empty when no unit moved.
    /// </summary>
    protected static string FormatUnits(int units)
    {
        return units switch
        {
            > 0 => $"+ {units}",
            < 0 => $"− {-units}",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Writes one history row as a sentence, with the units and money it moved.
    /// </summary>
    protected virtual async Task<CustomerSubsidyHistoryModel> PrepareHistoryModelAsync(SubsidyHistory item)
    {
        var model = new CustomerSubsidyHistoryModel
        {
            OrderId = item.OrderId,
            CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(item.CreatedOn, DateTimeKind.Utc)).ToString("g"),
            AssignmentRemoved = item.AssignmentRemoved
        };

        var units = 0;
        var credit = decimal.Zero;

        switch (item.HistoryType)
        {
            case HistoryType.Subsidy:
                model.What = await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.Assigned");
                credit = item.Credit;
                break;

            case HistoryType.SubsidyTopUp:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.Topup"), item.CreatorName, item.Reason);
                credit = item.CreditTopup;
                break;

            case HistoryType.ProductGroup:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.GroupAllocated"), item.ProductGroup);
                units = item.Quantity;
                credit = item.ProductGroupCredit;
                break;

            case HistoryType.ProductGroupTopup:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.GroupTopup"), item.ProductGroup, item.CreatorName, item.Reason);
                units = item.QuantityTopup;
                credit = item.ProductGroupCreditTopup;
                break;

            case HistoryType.SubsidyUsed:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.Order"), item.OrderId);
                credit = -(item.Credit + item.CreditTopup);
                break;

            case HistoryType.ProductGroupUsed:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.OrderGroup"), item.OrderId, item.ProductGroup);
                units = -(item.Quantity + item.QuantityTopup);
                credit = -(item.ProductGroupCredit + item.ProductGroupCreditTopup);
                break;

            case HistoryType.SubsidyUsedForShipping:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.OrderShipping"), item.OrderId);
                credit = -(item.Credit + item.CreditTopup);
                break;

            case HistoryType.SubsidyVariation:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.Variation"), item.CreatorName);
                credit = item.Credit;
                break;

            case HistoryType.Refund:
                model.What = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies.History.Refund"), item.OrderId);
                units = item.Quantity + item.QuantityTopup;
                credit = item.Credit + item.CreditTopup + item.ProductGroupCredit + item.ProductGroupCreditTopup;
                break;

            default:
                model.What = item.Reason;
                break;
        }

        model.Units = FormatUnits(units);
        model.Credit = await FormatMovementAsync(credit);

        return model;
    }

    /// <summary>
    /// Sorts an assignment's history into its seasons, each newest first. What stands for every season (the assignment,
    /// the allocations, changes) is in each season from the one it was made in; a top-up or an order is in its own
    /// season; a refund is in the season of the order it gives back to.
    /// </summary>
    /// <param name="history">The assignment's history, newest first</param>
    /// <param name="seasons">The seasons, oldest first</param>
    /// <returns>One list per season, in the seasons' order</returns>
    protected virtual IList<List<SubsidyHistory>> HistoryBySeason(IList<SubsidyHistory> history, IList<(DateTime Start, DateTime End)> seasons)
    {
        var timeZone = _dateTimeHelper.DefaultStoreTimeZone;

        int seasonOf(SubsidyHistory item) =>
            CustomerSubsidyHelper.SeasonIndexFor(SubsidyPeriodHelper.ToStoreLocal(item.CreatedOn, timeZone), seasons);

        var orderSeasons = new Dictionary<int, int>();

        foreach (var item in history.Where(item => item.OrderId > 0 && item.HistoryType is HistoryType.SubsidyUsed or HistoryType.ProductGroupUsed or HistoryType.SubsidyUsedForShipping))
            orderSeasons.TryAdd(item.OrderId, seasonOf(item));

        var bySeason = seasons.Select(_ => new List<SubsidyHistory>()).ToList();

        foreach (var item in history)
        {
            var season = item.HistoryType == HistoryType.Refund && orderSeasons.TryGetValue(item.OrderId, out var orderSeason)
                ? orderSeason
                : seasonOf(item);

            if (!CustomerSubsidyHelper.IsStanding(item.HistoryType))
            {
                bySeason[season].Add(item);
                continue;
            }

            for (var index = season; index < seasons.Count; index++)
                bySeason[index].Add(item);
        }

        return bySeason;
    }

    /// <summary>
    /// An earlier season as a closed statement: what was topped up and used in it, and its history
    /// </summary>
    protected virtual async Task<CustomerSubsidySeasonModel> PrepareSeasonModelAsync((DateTime Start, DateTime End) season, IList<SubsidyHistory> history)
    {
        var toppedUp = decimal.Zero;
        var used = decimal.Zero;
        var usedUnits = 0;

        foreach (var item in history)
        {
            switch (item.HistoryType)
            {
                case HistoryType.SubsidyTopUp:
                    toppedUp += item.CreditTopup;
                    break;
                case HistoryType.ProductGroupTopup:
                    toppedUp += item.ProductGroupCreditTopup;
                    break;
                case HistoryType.SubsidyUsed:
                case HistoryType.SubsidyUsedForShipping:
                    used += item.Credit + item.CreditTopup;
                    break;
                case HistoryType.ProductGroupUsed:
                    used += item.ProductGroupCredit + item.ProductGroupCreditTopup;
                    usedUnits += item.Quantity + item.QuantityTopup;
                    break;
                case HistoryType.Refund:
                    used -= item.Credit + item.CreditTopup + item.ProductGroupCredit + item.ProductGroupCreditTopup;
                    usedUnits -= item.Quantity + item.QuantityTopup;
                    break;
            }
        }

        var model = new CustomerSubsidySeasonModel
        {
            SeasonStart = season.Start.ToShortDateString(),
            SeasonEnd = season.End.ToShortDateString(),
            ToppedUpFormatted = toppedUp == decimal.Zero ? string.Empty : await FormatAsync(toppedUp),
            UsedFormatted = used == decimal.Zero ? string.Empty : await FormatAsync(used),
            UsedUnits = usedUnits,
            HasActivity = history.Any(item => !CustomerSubsidyHelper.IsStanding(item.HistoryType))
        };

        foreach (var item in history)
            model.History.Add(await PrepareHistoryModelAsync(item));

        return model;
    }

    /// <summary>
    /// One product group of an assignment: units and credit only where the group allocates them.
    /// </summary>
    protected virtual async Task<CustomerSubsidyProductGroupModel> PrepareProductGroupModelAsync(SubsidyProductGroupDTO productGroup)
    {
        var totalUnits = productGroup.Quantity + productGroup.TopupQuantity;
        var usedUnits = productGroup.UsedQuantity + productGroup.UsedQuantityTopup;
        var totalCredit = productGroup.Credit + productGroup.TopupCredit;
        var usedCredit = productGroup.UsedCredit + productGroup.UsedCreditTopup;

        return new CustomerSubsidyProductGroupModel
        {
            Name = productGroup.Name,
            Icon = productGroup.Icon,
            Colour = productGroup.Colour,
            HasUnits = totalUnits > 0,
            TotalUnits = totalUnits,
            UsedUnits = usedUnits,
            RemainingUnits = totalUnits - usedUnits,
            TopupUnits = productGroup.TopupQuantity,
            HasCredit = totalCredit > decimal.Zero,
            TotalCreditFormatted = await FormatAsync(totalCredit),
            UsedCreditFormatted = await FormatAsync(usedCredit),
            RemainingCreditFormatted = await FormatAsync(totalCredit - usedCredit),
            TopupCreditFormatted = await FormatAsync(productGroup.TopupCredit)
        };
    }

    /// <summary>
    /// One assignment with its season, figures, product groups and history; the history links the orders the
    /// customer may open.
    /// </summary>
    protected virtual async Task<CustomerSubsidyModel> PrepareSubsidyModelAsync(SubsidyPackNameDTO assignment, int packNameId, DateTime today, Customer customer)
    {
        var (seasonStart, seasonEnd) = CustomerSubsidyHelper.SeasonFor(assignment.DurationStart, assignment.DurationEnd, assignment.AnnualRecurrence, today);

        var totalCredit = assignment.Credit + assignment.TopupCredit;
        var usedCredit = assignment.UsedCredit + assignment.UsedCreditTopup;

        //an inactive product group cannot be ordered from, so it is not listed
        var productGroups = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(assignment.Id, assignment.SubsidyId, activeOnly: true);
        var totalUnits = productGroups.Sum(productGroup => productGroup.Quantity + productGroup.TopupQuantity);
        var usedUnits = productGroups.Sum(productGroup => productGroup.UsedQuantity + productGroup.UsedQuantityTopup);

        var model = new CustomerSubsidyModel
        {
            SubsidyPackNameId = assignment.Id,
            SubsidyId = assignment.SubsidyId,
            Name = assignment.Subsidy,
            Description = assignment.Description,
            Status = CustomerSubsidyHelper.StatusFor(assignment.InOrderingPeriod, seasonStart, today),
            SeasonStart = seasonStart.ToShortDateString(),
            SeasonEnd = seasonEnd.ToShortDateString(),
            AnnualRecurrence = assignment.AnnualRecurrence,
            HasCredit = totalCredit > decimal.Zero,
            CreditFormatted = await FormatAsync(assignment.Credit),
            TopupCreditFormatted = await FormatAsync(assignment.TopupCredit),
            TotalCreditFormatted = await FormatAsync(totalCredit),
            UsedCreditFormatted = await FormatAsync(usedCredit),
            RemainingCreditFormatted = await FormatAsync(totalCredit - usedCredit),
            UsedCreditPercent = CustomerSubsidyHelper.UsedPercent(totalCredit, usedCredit),
            HasUnits = totalUnits > 0,
            TotalUnits = totalUnits,
            UsedUnits = usedUnits,
            RemainingUnits = totalUnits - usedUnits,
            UsedUnitsPercent = CustomerSubsidyHelper.UsedPercent(totalUnits, usedUnits)
        };

        foreach (var productGroup in productGroups.OrderBy(group => group.DisplayOrder))
            model.ProductGroups.Add(await PrepareProductGroupModelAsync(productGroup));

        //the history by season, as the figures count it: the current season's under the figures, the earlier ones apart
        var seasons = CustomerSubsidyHelper.SeasonsUpTo(assignment.DurationStart, assignment.DurationEnd, assignment.AnnualRecurrence, today);
        var history = await _subsidyReportingService.GetSubsidyPackNameHistoryAsync(packNameId, assignment.SubsidyId);
        var historyBySeason = HistoryBySeason(history, seasons);

        foreach (var item in historyBySeason[^1])
            model.History.Add(await PrepareHistoryModelAsync(item));

        for (var index = seasons.Count - 2; index >= 0; index--)
            model.EarlierSeasons.Add(await PrepareSeasonModelAsync(seasons[index], historyBySeason[index]));

        //nop's order page opens only the customer's own orders: a row of an order somebody else placed (the
        //employee's own order on a manager's screen, the manager's order on the employee's) stays a sentence
        var historyModels = model.History.Concat(model.EarlierSeasons.SelectMany(season => season.History)).ToList();
        var orderIds = historyModels.Where(item => item.OrderId > 0).Select(item => item.OrderId).Distinct().ToArray();

        if (orderIds.Length > 0)
        {
            var ownOrderIds = (await _orderService.GetOrdersByIdsAsync(orderIds))
                .Where(order => !order.Deleted && order.CustomerId == customer.Id)
                .Select(order => order.Id)
                .ToHashSet();

            foreach (var item in historyModels)
                item.CanOpenOrder = ownOrderIds.Contains(item.OrderId);
        }

        return model;
    }

    #endregion

    #region Methods

    public virtual async Task<CustomerSubsidiesModel> PrepareCustomerSubsidiesModelAsync(PackName packName, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(packName);
        ArgumentNullException.ThrowIfNull(customer);

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        var branch = packNameList?.BranchId > 0 ? await _branchService.GetBranchByIdAsync(packNameList.BranchId.Value) : null;
        var department = packName.DepartmentId > 0 ? await _multiStoreService.GetDepartmentByIdAsync(packName.DepartmentId.Value) : null;

        var model = new CustomerSubsidiesModel
        {
            PackNameId = packName.Id,
            PackNameName = packName.Name,
            PackNameColour = PackNameColourHelper.Normalize(packName.Colour),
            PackNameBranch = branch?.Name,
            PackNameDepartment = department?.Name,
            PackNameIsGroup = packName.Group,
            PackNameIsCustomer = packName.IsCustomer,
            IsManagedPackName = packName.CustomerId != customer.Id
        };

        //the season is judged on the store's calendar day, the frame the periods are expressed in
        var today = SubsidyPeriodHelper.ToStoreLocal(DateTime.UtcNow, _dateTimeHelper.DefaultStoreTimeZone).Date;

        //every live assignment, in period or not: a season that has not opened yet is still the customer's to see
        var assignments = await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packName.Id, inOrderingPeriodOnly: false);

        foreach (var assignment in assignments.Where(item => item.Active && item.AssignmentActive))
            model.Subsidies.Add(await PrepareSubsidyModelAsync(assignment, packName.Id, today, customer));

        return model;
    }

    #endregion
}