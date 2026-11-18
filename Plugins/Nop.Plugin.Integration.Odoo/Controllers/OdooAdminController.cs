using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Infrastructure;
using Nop.Plugin.Integration.Odoo.Models;
using Nop.Plugin.Integration.Odoo.Services;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Integration.Odoo.Controllers;

/// <summary>
/// Admin pages of the Odoo integration: the connection settings, the sync log, from which the background syncs
/// are started, and the orders sent to Odoo.
/// </summary>
/// <remarks>
/// The sync and order actions answer <c>{ ConnectionStatus, TestResultMessage }</c>, read by
/// <c>Content/Admin/Odoo/odoo-admin.js</c>; ConnectionStatus is Failed, Busy, Queued or Error.
/// The Orders page has a permission of its own (<see cref="OdooPermissionConfigManager.SEND_ORDERS"/>), so
/// the staff who send orders need not manage plugins; everything else needs Manage plugins.
/// </remarks>
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class OdooAdminController : BasePluginController
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IOdooApiService _odooApiService;
    protected readonly IOdooSyncQueue _syncQueue;
    protected readonly IOdooLogService _odooLogService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IOrderSyncService _orderSyncService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public OdooAdminController(
        ISettingService settingService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IOdooApiService odooApiService,
        IOdooSyncQueue syncQueue,
        IOdooLogService odooLogService,
        IDateTimeHelper dateTimeHelper,
        IOrderSyncService orderSyncService,
        IStoreService storeService)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _odooApiService = odooApiService;
        _syncQueue = syncQueue;
        _odooLogService = odooLogService;
        _dateTimeHelper = dateTimeHelper;
        _orderSyncService = orderSyncService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// A view from the plugin's <c>Views/OdooAdmin</c> folder.
    /// </summary>
    protected ViewResult PluginView(string name, object model)
        => View($"{OdooConstants.ViewsPath}{name}.cshtml", model);

    /// <summary>
    /// Whether the URL, database name and API key needed to call Odoo are saved.
    /// </summary>
    protected static bool IsConnectionConfigured(OdooSettings settings)
        => !string.IsNullOrWhiteSpace(settings.OdooUrl)
            && !string.IsNullOrWhiteSpace(settings.DatabaseName)
            && !string.IsNullOrWhiteSpace(settings.ApiKey);

    /// <summary>
    /// The Configure page model from the saved settings, dates in the admin's time zone. The API key itself never
    /// goes to the view.
    /// </summary>
    protected async Task<ConfigurationModel> PrepareConfigurationModelAsync(OdooSettings settings) => new()
    {
        OdooUrl = settings.OdooUrl,
        DatabaseName = settings.DatabaseName,
        Username = settings.Username,
        ApiKey = string.Empty,
        HasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey),
        LastSyncDateTime = settings.LastSyncDateTime.HasValue
            ? await _dateTimeHelper.ConvertToUserTimeAsync(settings.LastSyncDateTime.Value, DateTimeKind.Utc)
            : null,
        DeliveryProductCode = settings.DeliveryProductCode,
        ExchangeFeeProductCode = settings.ExchangeFeeProductCode
    };

    /// <summary>
    /// Queues a background sync of the given type and reports the outcome as JSON for the sync and order
    /// buttons: connection not configured, already queued or running, queued, or failed to queue.
    /// </summary>
    /// <param name="syncType">The sync to queue.</param>
    /// <param name="label">Display name of the sync type, e.g. "Store".</param>
    /// <param name="entityIds">The nop records the request is for (orders), or null.</param>
    protected async Task<IActionResult> QueueSyncAsync(SyncEventType syncType, string label, IReadOnlyList<int> entityIds = null)
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        if (!IsConnectionConfigured(settings))
        {
            return Json(new
            {
                ConnectionStatus = "Failed",
                TestResultMessage = "Please fill in Odoo URL, Database Name, and API Key before syncing."
            });
        }

        try
        {
            // Claim and enqueue in one step: returns at once, and refuses a duplicate while a sync of
            // this type is queued or running
            var requestId = await _syncQueue.TryEnqueueSyncAsync(syncType, entityIds);
            if (requestId is null)
            {
                return Json(new
                {
                    ConnectionStatus = "Busy",
                    TestResultMessage = $"{label} sync is already queued or running. Please wait for it to complete before starting another."
                });
            }

            await UpdateLastSyncDateTimeAsync();

            return Json(new
            {
                ConnectionStatus = "Queued",
                TestResultMessage = entityIds == null
                    ? $"{label} sync has been queued (ID: {requestId}). Check the Sync Logs for progress."
                    : $"{label} of {entityIds.Count} order(s) has been queued (ID: {requestId}). Check the Sync Logs for progress, then refresh this page."
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                ConnectionStatus = "Error",
                TestResultMessage = $"Failed to queue the {label.ToLowerInvariant()} sync: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Saves the current UTC time as the last sync date/time in the global <see cref="OdooSettings"/>.
    /// </summary>
    protected async Task UpdateLastSyncDateTimeAsync()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();
        settings.LastSyncDateTime = DateTime.UtcNow;

        await _settingService.SaveSettingAsync(settings, storeId: 0);
        await _settingService.ClearCacheAsync();
    }

    /// <summary>
    /// The Orders page's date filters in UTC: from the start of the first day to the end of the last.
    /// </summary>
    protected async Task<(DateTime? FromUtc, DateTime? ToUtc)> ToUtcRangeAsync(OdooOrderSearchModel searchModel)
    {
        var timeZone = await _dateTimeHelper.GetCurrentTimeZoneAsync();

        DateTime? fromUtc = searchModel.CreatedFrom.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(searchModel.CreatedFrom.Value.Date, timeZone)
            : null;
        DateTime? toUtc = searchModel.CreatedTo.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(searchModel.CreatedTo.Value.Date, timeZone).AddDays(1)
            : null;

        return (fromUtc, toUtc);
    }

    #endregion

    #region Configuration

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        return PluginView("Configure", await PrepareConfigurationModelAsync(settings));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return PluginView("Configure", model);

        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        settings.OdooUrl = model.OdooUrl.TrimEnd('/');
        settings.DatabaseName = model.DatabaseName.Trim();
        settings.Username = model.Username.Trim();

        // Only overwrite the stored API key if the user supplied a new one
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
            settings.ApiKey = model.ApiKey.Trim();

        settings.DeliveryProductCode = model.DeliveryProductCode?.Trim() ?? string.Empty;
        settings.ExchangeFeeProductCode = model.ExchangeFeeProductCode?.Trim() ?? string.Empty;

        // StoreId = 0 → global setting (not store-scoped)
        await _settingService.SaveSettingAsync(settings, storeId: 0);
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction(nameof(Configure));
    }

    /// <summary>
    /// Tests the saved connection and shows the Configure page with the result.
    /// </summary>
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> TestConnection()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();
        var model = await PrepareConfigurationModelAsync(settings);

        if (!IsConnectionConfigured(settings))
        {
            model.ConnectionStatus = "Failed";
            model.TestResultMessage = "Please fill in Odoo URL, Database Name, and API Key before testing.";
            return PluginView("Configure", model);
        }

        var result = await _odooApiService.TestConnectionAsync(settings.OdooUrl, settings.DatabaseName, settings.ApiKey);

        model.ConnectionStatus = result.Success ? "Connected" : "Failed";
        model.TestResultMessage = result.Success
            ? $"Success! Connected to Odoo version {result.OdooVersion}. Logged in as {result.LoggedInUser}. Product count: {result.ProductCount}."
            : $"Connection failed: {result.ErrorMessage}";

        return PluginView("Configure", model);
    }

    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Diagnostics()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        var model = new DiagnosticsModel
        {
            OdooUrl = settings.OdooUrl,
            DatabaseName = settings.DatabaseName,
            ApiKeyExists = !string.IsNullOrWhiteSpace(settings.ApiKey),
            ConnectionStatus = "Not tested"
        };

        if (!IsConnectionConfigured(settings))
        {
            model.TestResultMessage = "Please fill in Odoo URL, Database Name, and API Key before testing.";
            return Json(model);
        }

        try
        {
            var result = await _odooApiService.TestConnectionAsync(settings.OdooUrl, settings.DatabaseName, settings.ApiKey);

            model.ConnectionStatus = result.Success ? "Connected" : "Failed";
            model.TestResultMessage = result.Success
                ? $"Odoo Version: {result.OdooVersion}, Products: {result.ProductCount}, User: {result.LoggedInUser}"
                : result.ErrorMessage;

            if (result.ProductCount != null)
                await UpdateLastSyncDateTimeAsync();
        }
        catch (Exception ex)
        {
            model.ConnectionStatus = "Error";
            model.TestResultMessage = $"An error occurred: {ex.Message}";
        }

        return Json(model);
    }

    #endregion

    #region Syncs

    // The Sync Log page lists the buttons in run order: each sync resolves its parents from the ones before it.

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncStores() => QueueSyncAsync(SyncEventType.Store, "Store");

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncDepartments() => QueueSyncAsync(SyncEventType.Department, "Department");

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncProducts() => QueueSyncAsync(SyncEventType.Product, "Product");

    /// <summary>
    /// Queues a branch sync across all Odoo companies; each company's store is resolved through the
    /// store mapping, and companies with no mapped store are skipped.
    /// </summary>
    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncBranches() => QueueSyncAsync(SyncEventType.Branch, "Branch");

    #endregion

    #region Sync log

    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public IActionResult SyncLogs()
    {
        var model = new OdooLogEventSearchModel();
        model.SetGridPageSize();

        model.AvailableEventTypes.Add(new SelectListItem { Text = "All", Value = "-1" });
        foreach (var type in new[] { SyncEventType.Store, SyncEventType.Department, SyncEventType.Product, SyncEventType.Branch,
                     SyncEventType.OrderSend, SyncEventType.OrderCancel })
            model.AvailableEventTypes.Add(new SelectListItem { Text = type.ToString(), Value = ((int)type).ToString() });

        return PluginView("SyncLogs", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> LogEventList(OdooLogEventSearchModel searchModel)
    {
        var eventType = searchModel.EventTypeId >= 0
            ? (SyncEventType?)searchModel.EventTypeId
            : null;

        var logEvents = await _odooLogService.GetAllLogEventsAsync(
            eventType: eventType,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        // Batch-load statistics for the current page
        var eventIds = logEvents.Select(e => e.Id).ToList();
        var stats = await _odooLogService.GetEventStatisticsAsync(eventIds);

        var model = await new OdooLogEventListModel().PrepareToGridAsync(searchModel, logEvents, () =>
        {
            return logEvents.SelectAwait(async logEvent =>
            {
                var (traceCount, hasErrors) = stats.TryGetValue(logEvent.Id, out var s) ? s : (0, false);

                var summary = logEvent.FinishedOnUtc.HasValue
                    ? $"Created: {logEvent.TotalCreated} | Updated: {logEvent.TotalUpdated} | Skipped: {logEvent.TotalSkipped} | Processed: {logEvent.TotalProcessed}"
                    : string.Empty;

                var status = logEvent.FinishedOnUtc.HasValue
                    ? (hasErrors ? "Done (with errors)" : "Done")
                    : "Running";

                return new OdooLogEventModel
                {
                    Id = logEvent.Id,
                    Name = logEvent.Name,
                    EventType = logEvent.EventType.ToString(),
                    StartedOn = await _dateTimeHelper.ConvertToUserTimeAsync(logEvent.StartedOnUtc, DateTimeKind.Utc),
                    FinishedOn = logEvent.FinishedOnUtc.HasValue
                        ? await _dateTimeHelper.ConvertToUserTimeAsync(logEvent.FinishedOnUtc.Value, DateTimeKind.Utc)
                        : null,
                    TraceCount = traceCount,
                    HasErrors = hasErrors,
                    Status = status,
                    Summary = summary
                };
            });
        });

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> LogEventTraceList(OdooLogSearchModel searchModel)
    {
        var logItems = await _odooLogService.GetLogsByEventIdAsync(
            searchModel.LogEventId,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        var model = await new OdooLogListModel().PrepareToGridAsync(searchModel, logItems, () =>
        {
            return logItems.SelectAwait(async logItem => new OdooLogModel
            {
                Id = logItem.Id,
                LogLevel = logItem.LogLevel.ToString(),
                ShortMessage = logItem.ShortMessage,
                FullMessage = logItem.FullMessage,
                EntityType = logItem.EntityType,
                OdooEntityId = logItem.OdooEntityId,
                NopEntityId = logItem.NopEntityId,
                CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(logItem.CreatedOnUtc, DateTimeKind.Utc),
                LogEventId = logItem.LogEventId ?? 0
            });
        });

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> LogEventDeleteSelected(ICollection<int> selectedIds)
    {
        if (selectedIds == null || !selectedIds.Any())
            return NoContent();

        var logEvents = new List<OdooLogEvent>();
        foreach (var id in selectedIds)
        {
            var logEvent = await _odooLogService.GetLogEventByIdAsync(id);
            if (logEvent != null)
                logEvents.Add(logEvent);
        }

        await _odooLogService.DeleteLogEventsAsync(logEvents);

        return Json(new { Result = true });
    }

    [HttpPost, ActionName("SyncLogs")]
    [FormValueRequired("clearall")]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> SyncLogClearAll()
    {
        await _odooLogService.ClearLogAsync();

        _notificationService.SuccessNotification("Odoo sync log has been cleared.");

        return RedirectToAction(nameof(SyncLogs));
    }

    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> SyncLogView(int id)
    {
        var log = await _odooLogService.GetLogByIdAsync(id);
        if (log == null)
            return RedirectToAction(nameof(SyncLogs));

        var model = new OdooLogModel
        {
            Id = log.Id,
            LogLevel = log.LogLevel.ToString(),
            ShortMessage = log.ShortMessage,
            FullMessage = log.FullMessage,
            EntityType = log.EntityType,
            OdooEntityId = log.OdooEntityId,
            NopEntityId = log.NopEntityId,
            CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(log.CreatedOnUtc, DateTimeKind.Utc)
        };

        return PluginView("SyncLogView", model);
    }

    #endregion

    #region Orders

    [HttpGet]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> Orders()
    {
        var model = new OdooOrderSearchModel();
        model.SetGridPageSize();

        model.AvailableStores.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });
        foreach (var store in await _storeService.GetAllStoresAsync())
            model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });

        foreach (var (filter, text) in new[]
        {
            (OdooOrderSyncFilter.All, "All"),
            (OdooOrderSyncFilter.NotSent, OdooOrderSyncSummary.NotSent),
            (OdooOrderSyncFilter.Sent, OdooOrderSyncSummary.Sent),
            (OdooOrderSyncFilter.NeedsAttention, "Held, failed or in progress"),
            (OdooOrderSyncFilter.ToCancel, OdooOrderSyncSummary.ToCancel)
        })
            model.AvailableSyncStates.Add(new SelectListItem { Text = text, Value = ((int)filter).ToString() });

        model.AvailablePaymentStatuses.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });
        foreach (var paymentStatus in Enum.GetValues<PaymentStatus>())
            model.AvailablePaymentStatuses.Add(new SelectListItem
            {
                Text = await _localizationService.GetLocalizedEnumAsync(paymentStatus),
                Value = ((int)paymentStatus).ToString()
            });

        return PluginView("Orders", model);
    }

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> OrderList(OdooOrderSearchModel searchModel)
    {
        var (fromUtc, toUtc) = await ToUtcRangeAsync(searchModel);
        var orders = await _orderSyncService.SearchOrdersAsync(searchModel.StoreId, fromUtc, toUtc,
            (OdooOrderSyncFilter)searchModel.SyncState, searchModel.PaymentStatusId, searchModel.Page - 1, searchModel.PageSize);

        var mappings = (await _orderSyncService.GetMappingsAsync(orders.Select(order => order.Id).ToList()))
            .ToLookup(mapping => mapping.NopOrderId);
        var storeNames = (await _storeService.GetAllStoresAsync()).ToDictionary(store => store.Id, store => store.Name);

        var model = await new OdooOrderListModel().PrepareToGridAsync(searchModel, orders, () =>
        {
            return orders.SelectAwait(async order =>
            {
                var rows = mappings[order.Id].OrderBy(mapping => mapping.DestinationKey).ToList();
                var summary = OdooOrderSyncSummary.For(order.Deleted || order.OrderStatus == OrderStatus.Cancelled, rows);

                return new OdooOrderModel
                {
                    Id = order.Id,
                    CustomOrderNumber = order.CustomOrderNumber,
                    StoreName = storeNames.TryGetValue(order.StoreId, out var storeName) ? storeName : string.Empty,
                    CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc),
                    OrderTotal = order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture),
                    OrderStatus = order.Deleted ? "Deleted" : await _localizationService.GetLocalizedEnumAsync(order.OrderStatus),
                    PaymentStatus = await _localizationService.GetLocalizedEnumAsync(order.PaymentStatus),
                    OdooStatus = summary.Status,
                    CanSend = summary.CanSend,
                    CanCancel = summary.CanCancel,
                    OdooOrders = string.Join(", ", rows.Where(mapping => !string.IsNullOrEmpty(mapping.OdooOrderName)).Select(mapping => mapping.OdooOrderName)),
                    Message = string.Join(" | ", rows.Where(mapping => !string.IsNullOrEmpty(mapping.Message)).Select(mapping => mapping.Message))
                };
            });
        });

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> SendSelectedOrders(ICollection<int> selectedIds)
    {
        if (selectedIds == null || selectedIds.Count == 0)
            return NoContent();

        return await QueueSyncAsync(SyncEventType.OrderSend, "Order send", selectedIds.Distinct().ToList());
    }

    /// <summary>
    /// Queues the one order of a row's Sync button.
    /// </summary>
    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public Task<IActionResult> SendOrder(int id) => QueueSyncAsync(SyncEventType.OrderSend, "Order send", new List<int> { id });

    /// <summary>
    /// Queues every order the list shows with the posted filters, on every page, that a send would act on: orders
    /// already sent, cancelled or deleted are left out.
    /// </summary>
    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> SendAllOrders(OdooOrderSearchModel searchModel)
    {
        var (fromUtc, toUtc) = await ToUtcRangeAsync(searchModel);
        var orderIds = await _orderSyncService.GetSendableOrderIdsAsync(searchModel.StoreId, fromUtc, toUtc,
            (OdooOrderSyncFilter)searchModel.SyncState, searchModel.PaymentStatusId);

        if (orderIds.Count == 0)
            return Json(new { ConnectionStatus = "Failed", TestResultMessage = "No listed order needs sending." });

        return await QueueSyncAsync(SyncEventType.OrderSend, "Order send", orderIds.ToList());
    }

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public Task<IActionResult> CancelOrder(int id) => QueueSyncAsync(SyncEventType.OrderCancel, "Order cancel", new List<int> { id });

    #endregion
}
