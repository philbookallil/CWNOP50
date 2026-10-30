using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Messages;
using Nop.Data;
using Nop.Plugin.CW.Core.Areas.Admin.Components;
using Nop.Plugin.CW.Core.Components;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.DataBase;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.CW.Core;

public class CWCorePlugin : BasePlugin, IWidgetPlugin
{
    #region Fields

    /// <summary>
    /// The view component that renders each widget zone the plugin draws in. The storefront zones and the product
    /// page's bulk-ordering settings join with their features.
    /// </summary>
    private static readonly Dictionary<string, Type> _widgetComponents = new()
    {
        [PublicWidgetZones.HeaderLinksAfter] = typeof(PackNameSelectorViewComponent),
        [PublicWidgetZones.ContentBefore] = typeof(PackNameInfoViewComponent),
        [PublicWidgetZones.HeaderLinksBefore] = typeof(BulkOrderLinkViewComponent),
        [PublicWidgetZones.ProductDetailsOverviewBottom] = typeof(BulkOrderProductPageViewComponent),
        [PublicWidgetZones.ProductDetailsBottom] = typeof(BulkOrderProductPageViewComponent),
        [PublicWidgetZones.ContentAfter] = typeof(BulkOrderProductPageViewComponent),
        [CWWidgetsZones.WIDGET_CREDITNOTES_FOR_CART] = typeof(CreditNotesWidgetsViewComponent),
        [CWWidgetsZones.WIDGET_CREDITNOTES_ORDER_CUSTOMER] = typeof(CreditNotesWidgetsViewComponent),
        [CWWidgetsZones.WIDGET_CREDITNOTES_ORDER_ADMIN] = typeof(CreditNotesWidgetsViewComponent),
        [CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_CART] = typeof(ExchangesWidgetsViewComponent),
        [CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_ORDER] = typeof(ExchangesWidgetsViewComponent),
        [CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_ORDER_ADMIN] = typeof(ExchangesWidgetsViewComponent),
        [AdminWidgetZones.OrderDetailsBlock] = typeof(PackNameOrderAdminDetailsViewComponent),
        [AdminWidgetZones.ProductDetailsBlock] = typeof(BulkOrderProductPageAdminViewComponent),
        [AdminWidgetZones.OrderShipmentAddButtons] = typeof(CWShipmentDestinationViewComponent),
        [AdminWidgetZones.OrderShipmentDetailsButtons] = typeof(CWShipmentDestinationViewComponent),
        [AdminWidgetZones.OrderListButtons] = typeof(OrderListBadgesViewComponent),
        [AdminWidgetZones.CustomerDetailsButtons] = typeof(OrderListBadgesViewComponent)
    };

    private readonly ISettingService _settingService;
    private readonly WidgetSettings _widgetSettings;
    private readonly ILocalizationService _localizationService;
    private readonly IRepository<ActivityLogType> _activityLogTypeRepository;
    private readonly ICWDatabaseService _cwDatabaseService;
    private readonly IMessageTemplateService _messageTemplateService;
    private readonly IEmailAccountService _emailAccountService;
    private readonly EmailAccountSettings _emailAccountSettings;

    #endregion

    #region Ctor

    public CWCorePlugin(
        ISettingService settingService,
        WidgetSettings widgetSettings,
        ILocalizationService localizationService,
        IRepository<ActivityLogType> activityLogTypeRepository,
        ICWDatabaseService cwDatabaseService,
        IMessageTemplateService messageTemplateService,
        IEmailAccountService emailAccountService,
        EmailAccountSettings emailAccountSettings)
    {
        _settingService = settingService;
        _widgetSettings = widgetSettings;
        _localizationService = localizationService;
        _activityLogTypeRepository = activityLogTypeRepository;
        _cwDatabaseService = cwDatabaseService;
        _messageTemplateService = messageTemplateService;
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
    }

    #endregion

    #region Utilities

    protected async Task InstallOrUpdateAsync()
    {
        await InstallActivityLogTypesAsync();
        await InstallReturnReceiptTemplateAsync();
        await InstallCustomerRegistrationTemplateAsync();

        if (!_widgetSettings.ActiveWidgetSystemNames.Contains(CWCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add(CWCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        var allStoreSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(0);

        if (!await _settingService.SettingExistsAsync(allStoreSettings, x => x.PackNameEnable))
        {
            allStoreSettings.PackNameEnable = true;
            await _settingService.SaveSettingAsync(allStoreSettings, x => x.PackNameEnable, 0);
        }

        if (!await _settingService.SettingExistsAsync(allStoreSettings, x => x.SubsidyEnable))
        {
            allStoreSettings.SubsidyEnable = true;
            await _settingService.SaveSettingAsync(allStoreSettings, x => x.SubsidyEnable, 0);
        }

        if (!await _settingService.SettingExistsAsync(allStoreSettings, x => x.ExchangesEnable))
        {
            allStoreSettings.ExchangesEnable = true;
            await _settingService.SaveSettingAsync(allStoreSettings, x => x.ExchangesEnable, 0);
        }

        if (!await _settingService.SettingExistsAsync(allStoreSettings, x => x.CreditNotesEnable))
        {
            allStoreSettings.CreditNotesEnable = true;
            await _settingService.SaveSettingAsync(allStoreSettings, x => x.CreditNotesEnable, 0);
        }

        //Stored so the list shows (and can be edited) in All settings; the class default is the same value.
        if (!await _settingService.SettingExistsAsync(allStoreSettings, x => x.OwnPlacementPaymentMethods))
            await _settingService.SaveSettingAsync(allStoreSettings, x => x.OwnPlacementPaymentMethods, 0);
    }

    protected async Task InstallReturnReceiptTemplateAsync()
    {
        var existing = await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnReceipt);
        if (existing.Any())
            return;

        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
        {
            Name = CWMessageTemplateSystemNames.ReturnReceipt,
            Subject = "Your Return Request has been received",
            Body = "Thanks %Customer.FullName%, your return has been logged!<br /><br />" +
                   "What to do next?<br />" +
                   "Simply send your unworn and unwashed garments back to the address below.<br />" +
                   "Please clearly mark 'Your Name', 'Company Name' and the 'E-Store Order #' " +
                   "the return has come from, in the format below:<br /><br />" +
                   "%Store.CompanyName%<br />" +
                   "Attn: %Store.Name% RETURNS<br />" +
                   "RETURN REQUEST BY: %Customer.FullName%<br />" +
                   "REF: Order #%Order.OrderNumber%<br />" +
                   "%Store.CompanyAddress%<br />",
            IsActive = true,
            EmailAccountId = emailAccount?.Id ?? 0
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnReceipt}",
            "Shown to the customer on the return confirmation screen and printed on the PDF return receipt.");
    }

    /// <summary>
    /// Installs the welcome e-mail of a customer registered by a manager, once: a store that has edited it keeps
    /// its wording.
    /// </summary>
    protected async Task InstallCustomerRegistrationTemplateAsync()
    {
        var existing = await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.CustomerRegistrationImport);
        if (existing.Any())
            return;

        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
        {
            Name = CWMessageTemplateSystemNames.CustomerRegistrationImport,
            Subject = "Your account is now registered with %Store.Name%",
            Body = "Hello %Customer.FullName%,<br /><br />" +
                   "Your account at <a href=\"%Store.URL%\">%Store.Name%</a> is now registered and ready to use.<br /><br />" +
                   "You have been set up as:<br />" +
                   "Name: %PackName.Name%<br />" +
                   "%if (%PackName.EmployeeNumber% != \"\") Employee number: %PackName.EmployeeNumber%<br /> endif%" +
                   "Branch: %PackName.Branch%<br />" +
                   "%if (%Subsidy.SubsidyName% != \"\") Subsidy: %Subsidy.SubsidyName%<br /> endif%" +
                   "<br />To set your password and sign in, <a href=\"%Customer.PasswordRecoveryURL%\">click here</a>.<br /><br />" +
                   "If anything here is wrong, please let your manager know.<br />",
            IsActive = true,
            EmailAccountId = emailAccount?.Id ?? 0
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.CustomerRegistrationImport}",
            "Sent to a customer a manager registers for a packname: the packname details, the subsidies assigned and a link to set the password.");
    }

    /// <summary>
    /// Points the three shipment emails at the shipment's own delivery address instead of the order's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// nop's default bodies print the address from <c>%Order.Shipping…%</c>. On a packname order that is
    /// the first packname's address, so a customer with two parcels going to two branches receives two
    /// emails naming the same one. <c>MessageTokenProviderOverride.AddShipmentTokensAsync</c> supplies
    /// shipment-scoped equivalents; this swaps the templates over to them.
    /// </para>
    /// <para>
    /// A targeted token substitution rather than a rewritten body, so any wording a store has customised
    /// survives — and it is <b>idempotent</b>: once swapped there are no <c>%Order.Shipping…%</c> tokens
    /// left to match, so running it again on every plugin update changes nothing.
    /// </para>
    /// <para>
    /// Only these three templates, by system name. <c>%Order.ShippingMethod%</c> also appears in the order
    /// confirmation email, where it is order-level and correct — a global replace would break it.
    /// </para>
    /// </remarks>
    protected async Task PointShipmentTemplatesAtShipmentAddressAsync()
    {
        var templateNames = new[]
        {
            MessageTemplateSystemNames.SHIPMENT_SENT_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.SHIPMENT_DELIVERED_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.SHIPMENT_READY_FOR_PICKUP_CUSTOMER_NOTIFICATION
        };

        //Exactly the tokens nop's default bodies use for the delivery address, and nothing else.
        var replacements = new[]
        {
            "ShippingMethod", "ShippingFirstName", "ShippingLastName", "ShippingCompany",
            "ShippingAddress1", "ShippingAddress2", "ShippingCity", "ShippingZipPostalCode",
            "ShippingStateProvince", "ShippingCountry", "ShippingPhoneNumber", "ShippingEmail", "ShippingFaxNumber", "ShippingCounty",
            "ShippingCustomAttributes", "ShippingAddressLine"
        };

        foreach (var templateName in templateNames)
        {
            foreach (var template in await _messageTemplateService.GetMessageTemplatesByNameAsync(templateName))
            {
                if (string.IsNullOrEmpty(template.Body))
                    continue;

                var body = template.Body;

                foreach (var token in replacements)
                    body = body.Replace($"%Order.{token}%", $"%Shipment.{token}%");

                if (body == template.Body)
                    continue;

                template.Body = body;

                await _messageTemplateService.UpdateMessageTemplateAsync(template);
            }
        }
    }

    /// <summary>
    /// Swap the order-level delivery-address block for <c>%Order.ShippingDestinations%</c> in the eight
    /// stock templates that carry it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The block is cut by its delimiters rather than matched as a literal string, because its body runs
    /// to twenty lines whose newlines depend on the machine the store was installed from. Cutting from
    /// <c>%if (%Order.Shippable%)</c> to the first following <c>endif%</c> is safe for these eight: none
    /// of nop's default bodies nests a conditional inside that block. The three shipment templates do
    /// nest one, which is a second reason they are not in this list.
    /// </para>
    /// <para>
    /// Idempotent: after the swap no <c>%if (%Order.Shippable%)</c> remains, so a re-run finds nothing.
    /// A template an admin has rewritten far enough to lose the opening delimiter is left alone rather
    /// than guessed at.
    /// </para>
    /// </remarks>
    protected async Task PointOrderTemplatesAtShippingDestinationsAsync()
    {
        var templateNames = new[]
        {
            MessageTemplateSystemNames.ORDER_PLACED_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_PLACED_STORE_OWNER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_PROCESSING_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_COMPLETED_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_COMPLETED_STORE_OWNER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_PAID_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_CANCELLED_CUSTOMER_NOTIFICATION,
            MessageTemplateSystemNames.ORDER_REFUNDED_CUSTOMER_NOTIFICATION
        };

        const string blockStart = "%if (%Order.Shippable%)";
        const string blockEnd = "endif%";

        foreach (var templateName in templateNames)
        {
            foreach (var template in await _messageTemplateService.GetMessageTemplatesByNameAsync(templateName))
            {
                if (string.IsNullOrEmpty(template.Body))
                    continue;

                var start = template.Body.IndexOf(blockStart, StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                    continue;

                var end = template.Body.IndexOf(blockEnd, start + blockStart.Length, StringComparison.OrdinalIgnoreCase);

                if (end < 0)
                    continue;

                template.Body = string.Concat(
                    template.Body[..start],
                    "%Order.ShippingDestinations%",
                    template.Body[(end + blockEnd.Length)..]);

                await _messageTemplateService.UpdateMessageTemplateAsync(template);
            }
        }
    }

    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task InstallActivityLogTypesAsync()
    {
        var activityLogTypes = new List<ActivityLogType>
        {
            new()
            {
                SystemKeyword = "AddNewBranch",
                Enabled = true,
                Name = "Add a new branch"
            },
            new()
            {
                SystemKeyword = "EditBranch",
                Enabled = true,
                Name = "Edit a branch"
            },
            new()
            {
                SystemKeyword = "DeleteBranch",
                Enabled = true,
                Name = "Delete a branch"
            },
            new()
            {
                SystemKeyword = "AddNewDepartment",
                Enabled = true,
                Name = "Add a new department"
            },
            new()
            {
                SystemKeyword = "EditDepartment",
                Enabled = true,
                Name = "Edit a department"
            },
            new()
            {
                SystemKeyword = "DeleteDepartment",
                Enabled = true,
                Name = "Delete a department"
            },
            new()
            {
                SystemKeyword = "AddNewCostCenter",
                Enabled = true,
                Name = "Add a new cost centre"
            },
            new()
            {
                SystemKeyword = "EditCostCenter",
                Enabled = true,
                Name = "Edit a cost centre"
            },
            new()
            {
                SystemKeyword = "DeleteCostCenter",
                Enabled = true,
                Name = "Delete a cost centre"
            },
            new()
            {
                SystemKeyword = "AddNewPackNameList",
                Enabled = true,
                Name = "Add a new packaging name list"
            },
            new()
            {
                SystemKeyword = "EditPackNameList",
                Enabled = true,
                Name = "Edit a packaging name list"
            },
            new()
            {
                SystemKeyword = "DeletePackNameList",
                Enabled = true,
                Name = "Delete a packaging name list"
            },
            new()
            {
                SystemKeyword = "EditPackNameListDefault",
                Enabled = true,
                Name = "Edit the default packaging name list for a branch"
            },
            new()
            {
                SystemKeyword = "AddNewPackName",
                Enabled = true,
                Name = "Add a new packaging name"
            },
            new()
            {
                SystemKeyword = "EditPackName",
                Enabled = true,
                Name = "Edit an packaging name"
            },
            new()
            {
                SystemKeyword = "DeletePackName",
                Enabled = true,
                Name = "Delete an packaging name"
            },
            new()
            {
                SystemKeyword = "PackNameSelection.Started",
                Enabled = true,
                Name = "Customer packaging name selection. Started"
            },
            new()
            {
                SystemKeyword = "PackNameSelection.Finished",
                Enabled = true,
                Name = "Customer packaging name selection. Finished"
            },
            new()
            {
                SystemKeyword = "AddNewSubsidy",
                Enabled = true,
                Name = "Add a new subsidy"
            },
            new()
            {
                SystemKeyword = "EditSubsidy",
                Enabled = true,
                Name = "Edit an subsidy"
            },
            new()
            {
                SystemKeyword = "DeleteSubsidy",
                Enabled = true,
                Name = "Delete an subsidy"
            },
            new()
            {
                SystemKeyword = "AddNewProductGroup",
                Enabled = true,
                Name = "Add a new product group"
            },
            new()
            {
                SystemKeyword = "EditProductGroup",
                Enabled = true,
                Name = "Edit a product group"
            },
            new()
            {
                SystemKeyword = "DeleteProductGroup",
                Enabled = true,
                Name = "Delete a product group"
            },
            new()
            {
                SystemKeyword = "AddNewSubsidyProductGroup",
                Enabled = true,
                Name = "Add a new subsidy product group mapping"
            },
            new()
            {
                SystemKeyword = "EditSubsidyProductGroup",
                Enabled = true,
                Name = "Edit an subsidy product group mapping"
            },
            new()
            {
                SystemKeyword = "DeleteSubsidyProductGroup",
                Enabled = true,
                Name = "Delete an subsidy product group mapping"
            },
            new()
            {
                SystemKeyword = "EditSubsidyTopup",
                Enabled = true,
                Name = "Edit an subsidy topup"
            },
            new()
            {
                SystemKeyword = "EditProductGroupTopup",
                Enabled = true,
                Name = "Edit a product group topup"
            },
            new()
            {
                SystemKeyword = "AddSubsidyVariation",
                Enabled = true,
                Name = "Add an subsidy variation for packName"
            },
            new()
            {
                SystemKeyword = "EditSubsidyVariation",
                Enabled = true,
                Name = "Edit an subsidy variation for packName"
            },
            new()
            {
                SystemKeyword = CustomerActivityTypes.CREDIT_NOTES_UNDO.Key,
                Enabled = true,
                Name = CustomerActivityTypes.CREDIT_NOTES_UNDO.Value
            },
            new()
            {
                SystemKeyword = CustomerActivityTypes.CREDIT_NOTES_MANUAL.Key,
                Enabled = true,
                Name = CustomerActivityTypes.CREDIT_NOTES_MANUAL.Value
            },
            new() { SystemKeyword = "AddPackNameListManager", Enabled = true, Name = "Add a packName list manager" },
            new() { SystemKeyword = "DeletePackNameListManager", Enabled = true, Name = "Delete a packName list manager" },
            new() { SystemKeyword = "AddDepartmentManager", Enabled = true, Name = "Add a department manager" },
            new() { SystemKeyword = "DeleteDepartmentManager", Enabled = true, Name = "Delete a department manager" },
            new() { SystemKeyword = "EditStoreSettings", Enabled = true, Name = "Edit the CW settings of a store" },
            new() { SystemKeyword = "EditDefaultStoreSettings", Enabled = true, Name = "Edit the CW default settings for all stores" },
            new() { SystemKeyword = "GeneratePackNames", Enabled = true, Name = "Generate packNames" },
            new() { SystemKeyword = "GeneratePackNameLists", Enabled = true, Name = "Generate packName lists" },
            new() { SystemKeyword = "AddSubsidyPackName", Enabled = true, Name = "Assign a packName to a subsidy" },
            new() { SystemKeyword = "DeleteSubsidyPackName", Enabled = true, Name = "Remove a subsidy assignment" },
            new() { SystemKeyword = "AddProductGroupProduct", Enabled = true, Name = "Add products to a product group" },
            new() { SystemKeyword = "DeleteProductGroupProduct", Enabled = true, Name = "Remove a product from a product group" },
            new() { SystemKeyword = "PublicStore.ImportPackNames", Enabled = true, Name = "Public store. Import packnames from Excel" },
            new() { SystemKeyword = "ImportPackNames", Enabled = true, Name = "Import packnames from Excel (admin)" },
          };

        foreach (var activityLogType in activityLogTypes)
        {
            if (!await _activityLogTypeRepository.Table.Where(alt => alt.SystemKeyword == activityLogType.SystemKeyword).AnyAsync())
            {
                await _activityLogTypeRepository.InsertAsync(activityLogType);
            }
        }
    }

    protected async Task AddOrUpdateLocaleResources()
    {
        // CW locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            //Admin Menu
            ["Plugins.CW.Core.Admin.Menu"] = "CW Admin",
            ["Plugins.CW.Core.Admin.Menu.Branches"] = "Branches",

            ["Plugins.CW.Core.Admin.Menu.ExchangesAndReturns"] = "Exchanges & Returns",
            ["Plugins.CW.Core.Admin.Menu.FeeSettings"] = "Fee Settings",
            ["Plugins.CW.Core.Admin.Menu.CreditNotesReport"] = "Credit Notes Report",

            //Common
            ["Plugins.CW.Core.Admin.Saving"] = "Saving...",

            //MultiStore
            ["Plugins.CW.Core.Admin.MultiStores"] = "MultiStores",
            ["Plugins.CW.Core.Admin.MultiStores.SearchStore"] = "Store",
            ["Plugins.CW.Core.Admin.MultiStores.StoreManagement"] = "Store Management",
            ["Plugins.CW.Core.Admin.MultiStores.Store.BackToList"] = "back to store list",
            ["Plugins.CW.Core.Admin.MultiStores.Store.Fields.Name"] = "Store",
            ["Plugins.CW.Core.Admin.MultiStores.Store.Fields.BranchesCount"] = "Number of Branches",
            ["Plugins.CW.Core.Admin.MultiStores.Store.ManageStore"] = "Manage Store",

            //MultiStore tabs
            ["Plugins.CW.Core.Admin.MultiStores.Info"] = "General Info",
            ["Plugins.CW.Core.Admin.MultiStores.Branches"] = "Branches",
            ["Plugins.CW.Core.Admin.MultiStores.Departments"] = "Departments",
            ["Plugins.CW.Core.Admin.MultiStores.CostCenters"] = "Cost Centres",
            ["Plugins.CW.Core.Admin.MultiStores.PackNames"] = "PackNames",
            ["Plugins.CW.Core.Admin.MultiStores.Settings"] = "Store Settings",
            ["Plugins.CW.Core.Admin.MultiStores.ExchangesReturns"] = "Exchanges and Returns",
            ["Plugins.CW.Core.Admin.MultiStores.FeeSettings"] = "Fee Settings",

            ["Plugins.CW.Core.Admin.Breadcrumb.NewPackName"] = "New packName",
            ["Plugins.CW.Core.Admin.Breadcrumb.NewCostCenter"] = "New cost centre",
            ["Plugins.CW.Core.Admin.Breadcrumb.NewDepartment"] = "New department",
            ["Plugins.CW.Core.Admin.Breadcrumb.NewProductGroup"] = "New product group",
            ["Plugins.CW.Core.Admin.Breadcrumb.NewSubsidy"] = "New subsidy",

            ["Plugins.CW.Core.Admin.MultiStore.Settings.PackNameListNameFrom.Invalid"] = "The selected packName list name source is not valid.",
            ["Plugins.CW.Core.Admin.MultiStore.Settings.DefaultScopeNotAllowed"] = "Default settings for all stores can only be edited from the default settings page",

            ["Plugins.CW.Core.Admin.StoreNav.Common.Save"] = "Save",
            ["Plugins.CW.Core.Admin.StoreNav.Common.Saving"] = "Saving...",
            ["Plugins.CW.Core.Admin.StoreNav.Common.Loading"] = "Loading...",
            ["Plugins.CW.Core.Admin.StoreNav.Common.Retry"] = "Retry",
            ["Plugins.CW.Core.Admin.StoreNav.Common.FailedToLoad"] = "Failed to load.",
            ["Plugins.CW.Core.Admin.StoreNav.Common.ServerError"] = "A server error occurred. Please try again.",
            ["Plugins.CW.Core.Admin.StoreNav.Common.CouldNotDelete"] = "Could not delete.",
            ["Plugins.CW.Core.Admin.StoreNav.Common.ConfirmDelete"] = "Delete this item?",
            ["Plugins.CW.Core.Admin.StoreNav.Common.CouldNotSetDefault"] = "Could not set default.",
            ["Plugins.CW.Core.Admin.StoreNav.Common.Saved"] = "Saved",
            ["Plugins.CW.Core.Admin.StoreNav.Common.UnsavedChanges"] = "This form has unsaved changes. Leave without saving?",
            ["Plugins.CW.Core.Admin.StoreNav.Common.SelectAFile"] = "Please select a file.",
            ["Plugins.CW.Core.Admin.StoreNav.Branch.CouldNotSave"] = "Could not save the branch.",
            ["Plugins.CW.Core.Admin.StoreNav.PackNames.FailedToLoad"] = "Failed to load packNames.",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.Store"] = "Store",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.Branch"] = "Branch",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.PackNameList"] = "PackName list",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.PackName"] = "PackName",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.CostCentre"] = "Cost centre",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.Department"] = "Department",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.ProductGroup"] = "Product Group",
            ["Plugins.CW.Core.Admin.StoreNav.Entity.Subsidy"] = "Subsidy",
            ["Plugins.CW.Core.Admin.StoreNav.New.CostCentre"] = "New cost centre",
            ["Plugins.CW.Core.Admin.StoreNav.New.Department"] = "New department",
            ["Plugins.CW.Core.Admin.StoreNav.New.PackName"] = "New packName",
            ["Plugins.CW.Core.Admin.StoreNav.New.ProductGroup"] = "New product group",
            ["Plugins.CW.Core.Admin.StoreNav.New.Subsidy"] = "New subsidy",
            ["Plugins.CW.Core.Admin.StoreNav.Generate.PackNames.Failed"] = "Could not generate packaging names.",
            ["Plugins.CW.Core.Admin.StoreNav.Generate.PackNames.Queued"] = "Generating packaging names in the background",
            ["Plugins.CW.Core.Admin.StoreNav.Generate.PackNames.Error"] = "Failed to generate packaging names",
            ["Plugins.CW.Core.Admin.StoreNav.Generate.Lists.Failed"] = "Could not generate packaging name lists.",
            ["Plugins.CW.Core.Admin.StoreNav.Generate.Lists.Queued"] = "Generating packaging name lists in the background",
            ["Plugins.CW.Core.Admin.StoreNav.Generate.Lists.Error"] = "Failed to generate packaging name lists",

            // Exchanges order return requests grid (admin order page widget)
            ["Plugins.CW.Core.Admin.Exchanges.OrderReturns.Title"] = "Return Requests",
            ["Plugins.CW.Core.Admin.Exchanges.OrderReturns.ItemDeleted"] = "Order item deleted",

            // Exchanges fee settings (admin)
            ["Plugins.CW.Core.Admin.Exchanges.Fees.MenuTitle"] = "Fee Settings",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.GlobalTitle"] = "Exchanges & Returns — global fee settings",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.GlobalInfo"] = "Global fee rules apply to all stores. A store can override a rule from its own Fee Settings tab; the override then takes precedence for that store.",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.StoreInfo"] = "Rules marked as inherited come from the global fee settings. Editing an inherited rule creates a store-specific override.",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.GridTitle"] = "Fee rules",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Fields.FeeType"] = "Fee",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Fields.Amount"] = "Amount",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Fields.Operator"] = "Rule type",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Fields.UnitsQuantity"] = "Units",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Fields.UnitsQuantity.Hint"] = "The fee amount is charged once per this many units (rounded up).",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Fields.Inherited"] = "Inherited",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Override"] = "Override",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Modal.AddTitle"] = "Add a fee rule",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Modal.EditTitle"] = "Edit fee rule",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Delete.Confirm"] = "Are you sure you want to delete this fee rule?",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Validation.Amount"] = "Please enter an amount greater than zero.",
            ["Plugins.CW.Core.Admin.Exchanges.Fees.Validation.Qty"] = "The units quantity must be at least 1.",

            // Exchanges and Returns settings (admin)

            ["Plugins.CW.Core.Admin.Exchanges.Rules.Settings"] = "Exchanges and Returns Settings",

            ["Plugins.CW.Core.Admin.Exchanges.Rules.ReasonRules"] = "Return Reason Rules",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.ReasonRules.LinkActions"] = "Link Actions",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.ReasonRules.NoRules"] = "No Return Reason rules have been defined yet",

            ["Plugins.CW.Core.Admin.Exchanges.Rules.ActionRules"] = "Return Action Rules",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.ActionRules.NoRules"] = "No Return Action rules have been defined yet",

            ["Plugins.CW.Core.Admin.Exchanges.Rules.StatusRules"] = "Return Status Rules",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.StatusRules.NoRules"] = "No Return Status rules have been defined yet",

            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.ReasonRules"] = "Return Reason Rule",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.ActionRules"] = "Return Action Rule",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.StatusRules"] = "Return Status Rule",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowStoreCredit"] = "Allow Store Credit",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowExchange"] = "Allow Exchange",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowRepackagingFee"] = "Repackaging Fee",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowShippingFee"] = "Redelivery Fee",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.BackInStock"] = "Back in Stock",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.IsActive"] = "Is Active",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.DisplayOrder"] = "Display Order",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.LinkedActions"] = "Linked Actions",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.PerformStoreCredit"] = "Perform Store Credit",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.PerformExchange"] = "Perform Exchange",
            ["Plugins.CW.Core.Admin.Exchanges.Rules.Fields.Authorised"] = "Authorised",

            // Linked Actions Popup
            ["Plugins.CW.Core.Admin.LinkedActions.Title"] = "Linked Actions",
            ["Plugins.CW.Core.Admin.LinkedActions.Select"] = "Select Actions",

            //Branches
            ["Plugins.CW.Core.Admin.Branches"] = "Branches",
            ["Plugins.CW.Core.Admin.Branches.AddRootBranch"] = "Add root branch",
            ["Plugins.CW.Core.Admin.Branches.AddChildBranch"] = "Add child branch",
            ["Plugins.CW.Core.Admin.Branches.NoBranchesYet"] = "No branches yet. Add the first one below.",
            ["Plugins.CW.Core.Admin.Branches.DeleteConfirmMessage"] = "Delete this branch?",

            ["Plugins.CW.Core.Admin.Branches.NewRootBranch"] = "New root branch",
            ["Plugins.CW.Core.Admin.Branches.NewChildBranch"] = "New child branch",

            ["Plugins.CW.Core.Admin.Branches.Branch.Field.BranchCode"] = "Branch Code",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.Name"] = "Name",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.CardCode"] = "Card Code",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.BranchNumber"] = "Branch Number",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.StoreId"] = "Parent Store",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.PaymentTerms"] = "Payment Terms",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ShippingMethod"] = "Shipping Methods",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ShippingMethod.Hint"] = "The shipping methods packNames of this branch may choose at the checkout, the first one selected by default. Leave empty to use the store's shipping methods.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.Active"] = "Active",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.StaffCount"] = "Staff",
            ["Plugins.CW.Core.Admin.Branches.Branch.Details"] = "Branch details",
            ["Plugins.CW.Core.Admin.Branches.Branch.Inactive"] = "inactive",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ManageStaff"] = "Manage Staff",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.PackNamesCount"] = "PackNames",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranchId"] = "Parent Branch",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranchId.Hint"] = "The branch this branch sits under. Leave it empty for a root branch.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranchId.None"] = "[None]",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranch.WrongStore"] = "The parent branch belongs to a different store.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranch.Cycle"] = "A branch cannot sit under itself or one of its sub-branches.",
            ["Plugins.CW.Core.Admin.Branches.Branch.NotFound"] = "That branch no longer exists. Refresh the tree and try again.",

            ["Plugins.CW.Core.Admin.Branches.Branch.AddNew"] = "Add a new branch",
            ["Plugins.CW.Core.Admin.Branches.Branch.EditBranchDetails"] = "Edit branch details",
            ["Plugins.CW.Core.Admin.Branches.Branch.BackToList"] = "back to branch list",
            ["Plugins.CW.Core.Admin.Branches.Branch.Info"] = "Branch info",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses"] = "Addresses",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.AddButton"] = "Add new address",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.AddInline"] = "Add an address\u2026",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.Added"] = "The new address has been added successfully.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.Updated"] = "The address has been updated successfully.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.AddNew"] = "Add new address",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.BackToBranch"] = "back to branch details",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.Edit"] = "Edit address",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.Default"] = "Default",
            ["Plugins.CW.Core.Admin.Branches.Branch.Addresses.SetDefault"] = "Set as Default",

            ["Plugins.CW.Core.Admin.Branches.Branch.Field.StoreId.Required"] = "Parent Store is required",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.BranchCode.Required"] = "Branch Code is required",
            ["Plugins.CW.Core.Admin.Branches.Branch.Field.Name.Required"] = "Name is required",

            ["Plugins.CW.Core.Admin.Branches.Branch.Added"] = "The new branch has been added successfully.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Updated"] = "The branch has been updated successfully.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Deleted"] = "The branch has been deleted successfully.",

            ["Plugins.CW.Core.Admin.Branches.Branch.Delete.HasChildren"] = "Cannot delete a branch that has sub-branches, packName lists or cost centers.",
            ["Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"] = "Address not found",

            //PackNameLists
            ["Plugins.CW.Core.Admin.PackNameLists"] = "PackName lists",
            ["Plugins.CW.Core.Admin.PackNameLists.Add"] = "Add list",
            ["Plugins.CW.Core.Admin.PackNameLists.AddInline"] = "Add a packName list…",
            ["Plugins.CW.Core.Admin.PackNames.AddInline"] = "Add a packName...",
            ["Plugins.CW.Core.Admin.Common.AddHint"] = "Type a name and press Enter",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Inactive"] = "Inactive",
            ["Plugins.CW.Core.Admin.Common.Back"] = "Back",
            ["Plugins.CW.Core.Admin.Customers.Customers.PackName.OpenInMultiStore"] = "Open in Manage Store",
            ["Plugins.CW.Core.PackNames.AddPackName.RegistrationFailed"] = "There was an error trying to register the customer",
            //COUNT CHIPS, one resource per grammatical number. A single "{0} manager(s)" reads badly
            //and does not survive translation into languages whose plural rules are not English's -
            //the caller picks the key, so a locale that needs a different split can supply it.
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.ManagerCount.One"] = "{0} manager",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.ManagerCount.Many"] = "{0} managers",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.PackNameCount.One"] = "{0} packName",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.PackNameCount.Many"] = "{0} packNames",
            ["Plugins.CW.Core.Admin.PackNameLists.Managers.SearchManagerName"] = "Name",
            ["Plugins.CW.Core.Admin.PackNameLists.Managers.SearchOtherBranches"] = "From Other Branches",
            ["Plugins.CW.Core.Admin.PackNameLists.DeleteConfirmMessage"] = "Delete this list? PackNames inside will also be removed.",

            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.SetDefault"] = "Set default list",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.New"] = "New packName list",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Edit"] = "Edit",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Delete"] = "Delete",

            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.ParentManagers"] = "Parent Managers",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Name"] = "List Name",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Name.Required"] = "List name is required.",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Store"] = "Store",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Store.Required"] = "Store is required.",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Branch"] = "Branch",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.AllManagers"] = "All Managers",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Owners"] = "List Owners",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.IsDefault"] = "Is Default",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.IsDefault.Hint"] = "Is the default list for the branch",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Active"] = "Active",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.PackNameListId"] = "PackName list",

            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Managers"] = "Owners",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Managers.AllManagersSelected"] = "All managers has been selected",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Managers.SaveBeforeEdit"] = "You need to save the packName list before you can add owners.",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Managers.AddNew"] = "Add new owner",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Managers.SearchManagerName"] = "Name",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.Managers.SearchOtherBranches"] = "From Other Branches",

            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.WrongBranch"] = "That list does not belong to this branch.",
            ["Plugins.CW.Core.Admin.PackNameLists.PackNameList.SetDefault.Button"] = "Set default",

            //PackNames
            ["Plugins.CW.Core.Admin.PackNames.AddNew"] = "Add a new PackName",
            ["Plugins.CW.Core.Admin.PackNames.ImportFromExcel"] = "Import from Excel",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Activate"] = "Activate",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Deactivate"] = "Deactivate",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Move"] = "Move to list",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Delete"] = "Delete",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.ClearSelection"] = "Clear selection",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.SelectPage"] = "Select every packname on this page",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.SelectRow"] = "Select {0}",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Export"] = "Export to Excel",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.ExportSelected"] = "Export selected to Excel",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Selected"] = "{0} selected",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.NoneSelected"] = "Tick packnames to act on several at once",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.TooMany"] = "Select at most {0} packnames at a time.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.NothingSelected"] = "No packnames were selected.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.UnknownAction"] = "That action is not available.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.ConfirmDelete"] = "Delete {0} packname(s)? Their subsidy assignments and cart lines are removed too.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.ConfirmDelete.Managers"] = "{0} of them are managers.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.ConfirmDelete.Customers"] = "{0} of them have a customer account; the accounts are kept.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.ResultTitle"] = "Bulk action",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Result.Changed"] = "{0} packname(s) changed.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Result.Unchanged"] = "{0} packname(s) were already that way.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Result.Skipped"] = "{0} packname(s) skipped:",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Close"] = "Close",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Cancel"] = "Cancel",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Move.Title"] = "Move packnames to another list",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Move.Intro"] = "Move {0} packname(s) to:",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Move.Confirm"] = "Move",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Move.NoTargets"] = "This store has no other switched-on list to move packnames to.",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Skip.NotFound"] = "it no longer exists in this store",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Skip.NameTaken"] = "the list {0} already has a packname with this name",
            ["Plugins.CW.Core.Admin.PackNames.Bulk.Stopped"] = "The action stopped part way because of an error: {0} packname(s) were not processed. The ones before them were. The error is in the log.",
            ["Plugins.CW.Core.Admin.PackNames.Import.BackToPackNames"] = "back to the store's packnames",
            ["Plugins.CW.Core.Admin.PackNames.Import.StoreNotFound"] = "The store could not be found.",
            ["Plugins.CW.Core.Admin.PackNames.Import.ReportFailed"] = "The error report could not be built from that file. Choose the file again and import it to see its problems.",
            ["Plugins.CW.Core.Admin.PackNames.SearchBranchName"] = "Branch",
            ["Plugins.CW.Core.Admin.PackNames.SearchPackName"] = "Name",
            ["Plugins.CW.Core.Admin.PackNames.SearchEmail"] = "Email",
            ["Plugins.CW.Core.Admin.PackNames.SearchPackNameType"] = "PackName Type",
            ["Plugins.CW.Core.Admin.PackNames.SearchGender"] = "Gender",
            ["Plugins.CW.Core.Admin.PackNames.SearchDepartmentId"] = "Department",
            ["Plugins.CW.Core.Admin.PackNames.SearchCostCenterId"] = "Cost Centre",
            ["Plugins.CW.Core.Admin.PackNames.SearchManager"] = "Manager",
            ["Plugins.CW.Core.Admin.PackNames.SearchManager.All"] = "All",
            ["Plugins.CW.Core.Admin.PackNames.SearchManager.ManagersOnly"] = "Managers only",
            ["Plugins.CW.Core.Admin.PackNames.SearchManager.NonManagersOnly"] = "Non-managers only",
            ["Plugins.CW.Core.Admin.PackNames.DeleteConfirmMessage"] = "Delete this packName?",

            ["Plugins.CW.Core.Admin.PackNames.Type.Both"] = "Both",
            ["Plugins.CW.Core.Admin.PackNames.Type.Customer"] = "Customer",
            ["Plugins.CW.Core.Admin.PackNames.Type.Packname"] = "PackName",

            ["Plugins.CW.Core.Admin.PackNames.ShowCustomersWithoutPackName"] = "Customers Without PackName",
            ["Plugins.CW.Core.Admin.PackNames.ShowCustomersWithoutPackName.Hint"] = "Show customers without a packName assigned",

            ["Plugins.CW.Core.Admin.PackNames.PackNameAddress.FromCustomer"] = "From Customer",
            ["Plugins.CW.Core.Admin.PackNames.AddPackName"] = "Add PackName",
            ["Plugins.CW.Core.Admin.PackNames.AddPackName.Hint"] = "Add new packName to the list",

            ["Plugins.CW.Core.Admin.PackNames.PackName.New"] = "New packName",
            ["Plugins.CW.Core.Admin.PackNames.PackName.AddPackNameAddress"] = "Add new address",
            ["Plugins.CW.Core.Admin.PackNames.PackName.AddPackNameAddress.Hint"] = "Add new address to the packName",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Addresses.SaveBeforeEdit"] = "You need to save the packName before you can add addresses.",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Addresses.AddNew"] = "Add new address",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Addresses.Edit"] = "Edit address",

            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList"] = "PackName List",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Required"] = "PackName List is required.",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.DoesNotExist"] = "New PackName List",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Alert.Lists.Failed"] = "Failed to retrieve packName lists.",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Name"] = "Name",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Name.Required"] = "Name is required",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Name.Exists"] = "A packname named {0} already exists in this list",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Colour"] = "Colour",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender"] = "Gender",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Mixed"] = "Mixed",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Male"] = "Male",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Female"] = "Female",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Neutral"] = "Neutral",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PrintName"] = "Print Name",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.EmployeeNumber"] = "Employee Number",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.POEntry"] = "PO Entry",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Comments"] = "Comments",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Group"] = "Group",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer"] = "Customer",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.AddNew"] = "Associate a customer",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.Clear"] = "Clear",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.HasPackName"] = "Has PackName",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.AlreadyHasPackName"] = "The customer already has a packName assigned",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.Alert.SelectBranch"] = "Please select a branch before associating the customer with a packName.",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.IsCustomer"] = "Customer",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch"] = "Branch",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch.Required"] = "Branch is required.",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Manager"] = "Manager",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Active"] = "Active",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Department"] = "Department",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter"] = "Cost Centre",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongStore"] = "The selected packName list does not belong to this store",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongBranch"] = "The selected packName list does not belong to the selected branch",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Inactive"] = "The selected packName list is switched off and takes no new packNames",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.WrongStore"] = "The selected customer belongs to a branch in another store",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Department.WrongStore"] = "The selected department is not available for this store",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter.WrongStore"] = "The selected cost centre is not available for this store",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Department.Inactive"] = "That department is no longer active and cannot take new assignments.",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter.Inactive"] = "That cost centre is no longer active and cannot take new assignments.",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch.NotFound"] = "The selected branch could not be found.",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch.WrongStore"] = "That branch belongs to a different store.",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch.Inactive"] = "That branch is no longer active and cannot take new cost centres.",

            //Cost Centers
            ["Plugins.CW.Core.Admin.CostCenters.AddNew"] = "Add new cost centre",
            ["Plugins.CW.Core.Admin.CostCenters.SearchCostCenterName"] = "Cost centre",
            ["Plugins.CW.Core.Admin.CostCenters.SearchInBranches"] = "From Branches",
            ["Plugins.CW.Core.Admin.CostCenters.SearchBranchName"] = "Branch",
            ["Plugins.CW.Core.Admin.CostCenters.DeleteConfirmMessage"] = "Delete this cost centre?",
            ["Plugins.CW.Core.Admin.CostCenters.AddInline"] = "Add a cost centre\u2026",

            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.StoreId"] = "Store",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.StoreId.Required"] = "Parent Store is required",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name"] = "Name",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name.Required"] = "Name is required",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name.Unique"] = "The specified cost centre already exists for this store",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Active"] = "Active",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch"] = "Branch",

            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.New"] = "New cost centre",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.AddCostCenterAddress"] = "Add new address",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.AddCostCenterAddress.Hint"] = "Add new address to the cost centre",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Addresses.SaveBeforeEdit"] = "You need to save the cost centre before you can add addresses.",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Addresses.AddNew"] = "Add new address",
            ["Plugins.CW.Core.Admin.CostCenters.CostCenter.Addresses.Edit"] = "Edit address",

            //Departments
            ["Plugins.CW.Core.Admin.Departments.AddNew"] = "Add a new department",
            ["Plugins.CW.Core.Admin.Departments.SearchDepartmentName"] = "Department",
            ["Plugins.CW.Core.Admin.Departments.DeleteConfirmMessage"] = "Delete this department?",

            ["Plugins.CW.Core.Admin.Departments.Department.New"] = "New department",
            ["Plugins.CW.Core.Admin.Departments.Department.Field.StoreId"] = "Store",
            ["Plugins.CW.Core.Admin.Departments.Department.Field.StoreId.Required"] = "Parent Store is required",
            ["Plugins.CW.Core.Admin.Departments.Department.Field.Name"] = "Name",
            ["Plugins.CW.Core.Admin.Departments.Department.Field.Name.Required"] = "Name is required",
            ["Plugins.CW.Core.Admin.Departments.Department.Field.Name.Unique"] = "The specified department already exists for this store",
            ["Plugins.CW.Core.Admin.Departments.Department.Field.Active"] = "Active",

            ["Plugins.CW.Core.Admin.Departments.Department.Managers"] = "Managers",
            ["Plugins.CW.Core.Admin.Departments.Department.Managers.AddNew"] = "Add managers",
            ["Plugins.CW.Core.Admin.Departments.Department.Managers.SearchManagerName"] = "Name",
            ["Plugins.CW.Core.Admin.Departments.Department.Managers.SaveBeforeEdit"] = "You need to save the department before you can add managers.",

            //Customers
            ["Plugins.CW.Core.Admin.Customers.List.SearchStoreId"] = "Registered by store",
            ["Plugins.CW.Core.Admin.Customers.List.SearchStoreId.Hint"] = "Check here to show only customers registered in an specific store",
            ["Plugins.CW.Core.Admin.Customers.List.Impersonate"] = "Impersonate",

            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId"] = "Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.SelectBranch"] = "Select Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNeedsBranch"] = "The customer's packname {0} is in a branch, so the customer needs a branch too. Delete the packname or unlink it from the customer first.",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore"] = "The customer's packname {0} belongs to another store, so it cannot follow the customer to branch {1}.",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.RegisteredInBranch"] = "Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.NotFound"] = "That customer could not be loaded.",

            ["Plugins.CW.Core.Admin.Customers.Customers.PackName"] = "PackName",
            ["Plugins.CW.Core.Admin.Customers.Customers.PackName.AddPackName"] = "Generate a PackName",
            ["Plugins.CW.Core.Admin.Customers.Customers.PackName.AddPackName.Hint"] = "Generates a new packName for the customer if the customer is associated to a branch",
            ["Plugins.CW.Core.Admin.Customers.Customers.PackName.CannotAddPackName"] = "The packName can't be generated because the customer is not assigned to a branch",

            // Credit Notes (admin)
            ["Plugins.CW.Core.Admin.CreditNotes.CardTitle"] = "Store Credit (Credit Notes)",
            ["Plugins.CW.Core.Admin.CreditNotes.Description"] = "Store credit issued to this customer. Available credit is applied automatically to future orders.",
            ["Plugins.CW.Core.Admin.CreditNotes.AvailableBalance"] = "Available balance",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.Title"] = "Add a manual credit note",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.Order"] = "Order",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.Description"] = "Description",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.Amount"] = "Amount",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.Add"] = "Add credit note",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.SelectOrder"] = "Please select an order.",
            ["Plugins.CW.Core.Admin.CreditNotes.AddForm.InvalidAmount"] = "Please enter an amount greater than zero.",
            ["Plugins.CW.Core.Admin.CreditNotes.Fields.Order"] = "Order #",
            ["Plugins.CW.Core.Admin.CreditNotes.Fields.Reason"] = "Reason",
            ["Plugins.CW.Core.Admin.CreditNotes.Fields.Amount"] = "Amount",
            ["Plugins.CW.Core.Admin.CreditNotes.Fields.Remain"] = "Remaining",
            ["Plugins.CW.Core.Admin.CreditNotes.Fields.CreatedOn"] = "Created on",
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.Confirm"] = "Are you sure you want to delete this credit note?",
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.ConfirmReturnRequest"] = "This credit note was created by a return request. Deleting it will NOT undo the return itself. Are you sure you want to delete it?",
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.Error"] = "Failed to delete the credit note.",
            ["Plugins.CW.Core.Admin.CreditNotes.AppliedTitle"] = "Credit Notes",
            ["Plugins.CW.Core.Admin.CreditNotes.AppliedLabel"] = "Applied to this order",

            // Credit Notes Report
            ["Plugins.CW.Core.Admin.Customer.CreditNote"] = "Credit Notes",
            ["Plugins.CW.Core.Admin.CreditNotes.Report"] = "Main Credit Notes Report",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Total"] = "Total Credit Notes Value",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.CreditNotesWithBalance"] = "Number of not consumed Credit Notes",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.MenuTitle"] = "Credit Notes Report",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Title"] = "Credit Notes — store liability",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.WithBalance"] = "Credit notes with balance",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.TotalValue"] = "Total outstanding value",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.Store"] = "Store",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.Issued"] = "Issued",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.Consumed"] = "Fully consumed",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.IssuedValue"] = "Issued value",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.Liability"] = "Liability (remaining)",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.PercentUsed"] = "% used",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.Customer"] = "Customer",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.AvailableCount"] = "Available credit notes",
            ["Plugins.CW.Core.Admin.CreditNotes.Report.Fields.Value"] = "Available value",

            //Product Groups
            ["Plugins.CW.Core.Admin.ProductGroups"] = "Product Groups",
            ["Plugins.CW.Core.Admin.ProductGroups.SearchProductGroupName"] = "Name",
            ["Plugins.CW.Core.Admin.ProductGroups.AddNew"] = "Add new product group",
            ["Plugins.CW.Core.Admin.ProductGroups.DeleteConfirmMessage"] = "Delete this product group?",

            ["Plugins.CW.Core.Admin.ProductGroups.Products"] = "Products",
            ["Plugins.CW.Core.Admin.ProductGroups.Products.AddNew"] = "Add product",
            ["Plugins.CW.Core.Admin.ProductGroups.Products.SaveBeforeEdit"] = "You need to save the product group before you can add products.",

            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.New"] = "New product group",

            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Name"] = "Name",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Name.Required"] = "Product group name is required.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Description"] = "Description",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Icon"] = "Icon",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Icon.NoneSelected"] = "None Selected",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Icon.Choose"] = "Choose Icon",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Icon.Close"] = "Close",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Colour"] = "Colour",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.DisplayOrder"] = "Display Order",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Active"] = "Active",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.ProductCount"] = "No. of Products",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Quantity"] = "Quantity",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Credit"] = "Credit",

            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity"] = "Topup Qty",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit"] = "Topup Credit",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.UsedQuantity"] = "Used Units",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.UsedCredit"] = "Used Credit",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Reason"] = "Reason",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Reason.Required"] = "Reason is required.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Product.NotFound"] = "That product is no longer in this product group. Refresh the list and try again.",

            //Subsidies
            ["Plugins.CW.Core.Admin.Subsidies"] = "Subsidies",

            ["Plugins.CW.Core.Admin.Subsidies.SearchSubsidyName"] = "Name",
            ["Plugins.CW.Core.Admin.Subsidies.AddNew"] = "Add new subsidy",
            ["Plugins.CW.Core.Admin.Subsidies.DeleteConfirmMessage"] = "Delete this subsidy?",

            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.New"] = "New subsidy",

            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Name"] = "Name",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Name.Required"] = "Subsidy name is required",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Description"] = "Description",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit"] = "Credit Allocation",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationStart"] = "Duration Start",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd"] = "Duration End",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationStart.Required"] = "Duration start is required.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd.Required"] = "Duration end is required.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd.GreaterThanDurationStart"] = "The duration end should be greater than duration start.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.AnnualRecurrence"] = "Annual Recurrence",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.OrderOutsidePeriod"] = "Order Outside Period",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.OnlyAllocatedProducts"] = "View Only Allocated Products",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.OnlyAllocatedProducts.Hint"] = "PackNames allocated to this subsidy can only see allocated products.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.IncludeManagers"] = "Include Managers",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.IncludeManagers.Hint"] = "If check managers are limited to subsidy products.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.RestrictToAllocatedQty"] = "Restrict To Allocated Qty",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.RestrictToAllocatedQty.Hint"] = "PackNames allocated to this subsidy can only order the allocated quantity set in the product group. Does not apply if $ value set for packName",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Active"] = "Active",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.StartDate"] = "Start",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.EndDate"] = "End",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.AllocatedStaff"] = "Allocated Staff",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.TotalCredit"] = "$Value",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.UsedPercent"] = "% Used",

            ["Plugins.CW.Core.Admin.Subsidies.ChangeCreditConfirmMessage"] = "If you change the credit value for this subsidy, the quantity limit will be turned off. Are you sure you want to continue?",

            ["Plugins.CW.Core.Admin.Subsidies.PackNames"] = "PackNames",
            ["Plugins.CW.Core.Admin.Subsidies.PackNames.AddNew"] = "Add packName",
            ["Plugins.CW.Core.Admin.Subsidies.PackNames.SaveBeforeEdit"] = "You need to save the subsidy before you can add packNames.",

            ["Plugins.CW.Core.Admin.PackNames.WithoutSubsidy"] = "Show packNames without subsidy",

            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.Store"] = "Store",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.UsedPercent"] = "% Used",
            ["Plugins.CW.Core.Admin.PackNames.PackName.Field.SubsidyVariation"] = "Variation",

            ["Plugins.CW.Core.Admin.Subsidies.ProductGroups"] = "Product Groups",
            ["Plugins.CW.Core.Admin.Subsidies.ProductGroups.AddNew"] = "Add product group",
            ["Plugins.CW.Core.Admin.Subsidies.ProductGroups.SaveBeforeEdit"] = "You need to save the subsidy before you can add product groups.",

            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit"] = "Topup Credit",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Reason"] = "Reason",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Reason.Required"] = "Reason is required.",

            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit.GreaterThanZero"] = "Enter an amount greater than zero. To reduce a packName's credit, use a variation.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit.Maximum"] = "The amount is too large. The maximum is {ComparisonValue}.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.PeriodClosed"] = "This assignment's ordering period is closed, so it cannot be topped up.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.NotInOrderingPeriod"] = "Not in its ordering period",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit.NotNegative"] = "Credit cannot be negative.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit.Maximum"] = "Credit is too large. The maximum is {ComparisonValue}.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.QuantityOrCreditRequired"] = "Enter a quantity or a credit amount greater than zero.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity.NotNegative"] = "Quantity cannot be negative.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity.Maximum"] = "Quantity is too large. The maximum is {ComparisonValue}.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit.NotNegative"] = "Credit cannot be negative.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit.Maximum"] = "Credit is too large. The maximum is {ComparisonValue}.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Quantity.NotNegative"] = "Quantity cannot be negative.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Quantity.Maximum"] = "Quantity is too large. The maximum is {ComparisonValue}.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Credit.NotNegative"] = "Credit cannot be negative.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Credit.Maximum"] = "Credit is too large. The maximum is {ComparisonValue}.",
            ["Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.DisplayOrder.NotNegative"] = "Display order cannot be negative.",

            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Edit"] = "Override Subsidy",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Change"] = "Change",

            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationStart"] = "Duration Start",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationEnd"] = "Duration End",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit"] = "Credit",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Reason"] = "Reason",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Reason.Required"] = "Reason is required.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationEnd.GreaterThanDurationStart"] = "Duration end must be after duration start.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Dates.BothOrNeither"] = "Enter both dates, or leave both empty to keep the subsidy's.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.NotNegative"] = "Credit cannot be negative.",
            ["Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.Maximum"] = "Credit is too large. The maximum is {ComparisonValue}.",

            //Subsidies for PackName panel
            ["Plugins.CW.Core.Admin.PackName.Subsidies"] = "Subsidies",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.NoSubsidies"] = "The packName doesn't have any subsidy assigned",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Info"] = "Subsidy Info",

            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.TopupCredit"] = "Topup Credit",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.Topup.Change"] = "Change",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.Topup.Change.Hint"] = "Change topup credit value for the packName",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.Topup.Edit"] = "Change Topup Credit",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.Topup.Closed"] = "closed",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.TotalCredit"] = "Total Credit",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.UsedCredit"] = "Used Credit",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.RemainingCredit"] = "Remaining Credit",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.TotalQuantity"] = "Total Units",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.UsedQuantity"] = "Used Units",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.RemainingQuantity"] = "Remaining Units",

            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.ProductGroup.Topup.Edit"] = "Change Topup",

            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History"] = "Subsidy History",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.OrderId"] = "Order StoreId",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.CreatedOn"] = "Created On",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Credit"] = "Subsidy $",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.CreditTopup"] = "Topup $",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Quantity"] = "Unit Qty",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.ProductGroupCredit"] = "Unit $",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.QuantityTopup"] = "Topup Unit Qty",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.ProductGroupCreditTopup"] = "Topup Unit $",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.ProductGroup"] = "Product Group",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.CreatorName"] = "Allocated By",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason"] = "Reason",

            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.Subsidy"] = "Initial subsidy allocation",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.ProductGroup"] = "Initial product group allocation",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.SubsidyUsed"] = "Consumed $ subsidy",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.ProductGroupUsed"] = "Consumed unit subsidy",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.SubsidyUsedForShipping"] = "Consumed $ subsidy for shipping",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.Refund"] = "Returned from order {0}. Restored to the allocation it came from",
            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.AssignmentRemoved"] = "Assignment",

            ["Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.AssignmentRemoved"] = "Removed",

            //ACL Departments
            ["Plugins.CW.Core.Admin.AclDepartment.NoDepartmentsAvailable"] = "No departments available",
            ["Plugins.CW.Core.Admin.AclDepartment.Field.SelectedDepartmentIds"] = "Departments",
            ["Plugins.CW.Core.Admin.AclDepartment.Field.SelectedDepartmentIds.Hint"] = "Choose one or several departments who will be able to use or see this item. If you don't need this option just leave this field empty.",

            //MultiStore settings
            ["Plugins.CW.Core.Admin.MultiStores.Default.Settings"] = "Default Settings",


            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNames"] = "PackNames",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.Registration"] = "Registration",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.UseStaffAsPackName.Hint"] = "Create a packName automatically for every customer linked to a branch of this store, in the branch's default list (created when the branch has none). The button below creates them now for the customers that have none.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.UseStaffAsPackName"] = "Use Staff as PackNames",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.UseStaffAsPackName.Generate"] = "Generate PackNames for all Customers",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNameEnable.Hint"] = "Enable the PackName (employee) functionality for this store.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNameEnable"] = "Enable PackNames",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.UseBranchesAsPackNameList.Hint"] = "Create the default packName list automatically when a branch is added to this store. The button below creates it now for the branches that have none.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.UseBranchesAsPackNameList"] = "Use Branches as PackName Lists",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.UseBranchesAsPackNameList.Generate"] = "Generate PackName Lists for Branches",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNameListNameFrom.Hint"] = "What to use from the branch to initially  populate the packName list name.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNameListNameFrom"] = "PackName List Name From",
            ["Enums.Nop.Plugin.CW.Core.Domain.PackNames.PackNameListNameFrom.BranchCode"] = "Branch Code",
            ["Enums.Nop.Plugin.CW.Core.Domain.PackNames.PackNameListNameFrom.Name"] = "Branch Name",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.EmployeeNumberEnabled.Hint"] = "Set if \"Employee Number\" is enabled.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.EmployeeNumberEnabled"] = "\"Employee Number\" enabled",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.PrintNameEnabled.Hint"] = "Set if \"Print Name\" is enabled.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.PrintNameEnabled"] = "\"Print Name\" enabled",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.PrintNameProductAttributeId.Hint"] = "Product attribute to be populated with the packName's \"Print Name\".",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.PrintNameProductAttributeId"] = "\"Print Name\" Product Attribute",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.POEntryEnabled.Hint"] = "Set if \"PO Entry\" is enabled.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.POEntryEnabled"] = "\"PO Entry\" enabled",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.CommentsEnabled.Hint"] = "Set if \"Comments\" is enabled.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.CommentsEnabled"] = "\"Comments\" enabled",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.GenderEnabled.Hint"] = "Set if \"Gender\" is enabled.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.GenderEnabled"] = "\"Gender\" enabled",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.ShowDepartmentPackNamesByBranch.Hint"] = "Show to the department managers the list of each branch with only the department packNames instead of showing them only one list with the department",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.ShowDepartmentPackNamesByBranch"] = "Show Department PackNames by Branch",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowManagersToAddPackNames.Hint"] = "Managers can add in packNames through the front end interface",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowManagersToAddPackNames"] = "Allow Managers to Add PackNames",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowToAddCustomerToPackName.Hint"] = "This will enable managers to add in customers when adding packNames through the front end interface and also it will enable customer import from the packName Excel",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowToAddCustomerToPackName"] = "Allow to Add Customer",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidySelection.Hint"] = "If this is selected then managers can also select subsidies for the packName",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidySelection"] = "Allow Subsidy Selection",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowEditSubsidyTopup.Hint"] = "If this is selected then managers can also change topup for subsidies",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowEditSubsidyTopup"] = "Allow Edit Topup",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowToAddByExcel.Hint"] = "This will show an Excel icon on the front end where the manager can download an Excel Template and then upload a list of packNames to the system",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowToAddByExcel"] = "Allow to Add by Excel",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel.Hint"] = "The number of subsidy column groups in the Excel import template (1 to 10)",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel"] = "Subsidy Count",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel.Range"] = "Subsidy Count must be between 1 and 10",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.LimitAssignedBranches.Hint"] = "This will limit managers to add or import packNames to the branches they have been assigned to.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.LimitAssignedBranches"] = "Limit to Manager's Assigned Branches",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNamesOwnGlobalValue.Hint"] = "If this is selected then packNames can have their own global value for subsidies",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNamesOwnGlobalValue"] = "PackNames Can Have Their Own Global Value",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNamesOwnStartAndFinishDates.Hint"] = "If this is selected then packNames can have their own start and finish dates for subsidies",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.PackNamesOwnStartAndFinishDates"] = "PackNames Can Have Their Own Start And Finish Dates",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.ShippingMethod.Hint"] = "The shipping methods packNames may choose at the checkout, the first one selected by default. A branch with its own selection overrides this. Leave empty to offer every method of the freight plugin.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.ShippingMethod"] = "Shipping Methods",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowHomeDelivery.Hint"] = "Set to allow home delivery for the packName",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowHomeDelivery"] = "Allow Home Delivery",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowCustomersToAddAddress.Hint"] = "If selected then customers will have the ability to add new addresses for their own when placing an order",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowCustomersToAddAddress"] = "Allow Customers To Add Address",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.DoNotPreSelectShippingAddress.Hint"] = "If this is selected then shipping address would not be preselected when placing an order",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.DoNotPreSelectShippingAddress"] = "Do Not Preselect Shipping Address",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.IncludeShippingOnSubsidy.Hint"] = "If this is selected then the available subsidy $ credit will be used for shipping value",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.IncludeShippingOnSubsidy"] = "Include Shipping On Subsidy",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.SubsidySendUserAfterLogin.Hint"] = "After signing in, selecting a packname or being impersonated, a customer whose selected packname has subsidy product groups lands on the subsidy catalogue instead of the home page. Bulk ordering's own landing setting comes second.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.SubsidySendUserAfterLogin"] = "Send customers to the subsidy catalogue after login",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.CreditNotesAndExchanges"] = "Credit Notes & Exchanges",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.CreditNotesEnable.Hint"] = "Enable the Credit Notes functionality for this store.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.CreditNotesEnable"] = "Enable Credit Notes",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.ExchangesEnable.Hint"] = "Enable the Exchanges / Returns functionality for this store.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.ExchangesEnable"] = "Enable Exchanges",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowCloseWithoutPrintingReturnNote"] = "Allow closing without printing the return note",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.AllowCloseWithoutPrintingReturnNote.Hint"] = "When enabled, customers can dismiss the return confirmation without printing. Leave off so the printed return note goes in the box with the garments.",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.SubsidySection"] = "Subsidy",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.Subsidy.Hint"] = "Enable the Subsidy functionality for this store.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.Subsidy"] = "Enable Subsidy",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.DefaultPackNameCustomerRoleIds.Hint"] = "Choose one or several customer roles to be added to the packName (other than 'Registered') when registering customers for a store.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.DefaultPackNameCustomerRoleIds"] = "Default Customer Roles",

            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderSection"] = "Bulk Ordering",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderEnable"] = "Enable Bulk Ordering",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderEnable.Hint"] = "Show the bulk ordering page. Customers also need the \"Access bulk ordering\" permission.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAxisAttributeIds"] = "Grid attributes, in order",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAxisAttributeIds.Hint"] = "Preference order for laying attributes out on the grid. The first becomes the columns, the rest the rows. A product attribute not listed here can still take a free slot; a listed attribute a product does not have is skipped.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAxisSlot"] = "Axis {0}",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderMaxCellsPerProduct"] = "Maximum cells per product",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderMaxCellsPerProduct.Hint"] = "When a product's grid would exceed this, the last attribute moves from the grid to the selectors above it.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderDefaultViewMode"] = "Default view mode",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowProductImages"] = "Show product images",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderProductImageSize"] = "Product image size (px)",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowAttributeImages"] = "Show attribute images",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAttributeImageSize"] = "Attribute image size (px)",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAttributeImageSquaresSize"] = "Attribute image squares size (px)",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAttributeImageSquaresSize.Hint"] = "0 uses the store's default image square size.",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderHideQtyArrows"] = "Hide quantity arrows",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowAttributePrices"] = "Show a price per grid cell",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowProductStock"] = "Show availability",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderDefaultPageSize"] = "Products per page",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderPageSizeOptions"] = "Page size options",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderSendUserAfterLogin"] = "Send users to bulk ordering after login",
            ["Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderEnableOnProductPage"] = "Show the bulk grid on product pages",
            ["Plugins.CW.Core.Admin.BulkOrder.ProductPanel"] = "Bulk ordering",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage"] = "Grid on this product's page",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage.Store"] = "Follow the store setting (currently {0})",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage.StoreOn"] = "shown",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage.StoreOff"] = "not shown",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage.On"] = "Always show it",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage.Off"] = "Never show it",
            ["Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage.Hint"] = "Replaces the attribute controls and Add to cart on this product's page with the bulk grid. Products the grid cannot express - grouped, gift card, rental, or with an attribute the customer must type - keep the standard controls whatever this says.",

            ["Plugins.CW.Core.Admin.Orders.Products.ShippingCost"] = "Shipping Cost",
            ["Plugins.CW.Core.Admin.Orders.Field.Phone"] = "Phone",

            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations"] = "Shipping destinations",
            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations.Hint"] = "This order ships to more than one place. Each destination is shipped separately — a shipment cannot mix items going to different addresses.",
            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations.Destination"] = "Ships to",
            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations.PackNames"] = "PackNames",
            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations.Pickup"] = "Pickup in store",
            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations.NoAddress"] = "No shipping address recorded",
            ["Plugins.CW.Core.Admin.Orders.Shipments.Destination"] = "Ships to",
            ["Plugins.CW.Core.Admin.Orders.Shipments.AddForDestination"] = "Add shipment",
            ["Plugins.CW.Core.Admin.Orders.Shipments.ShippingTo"] = "Shipping to",
            ["Plugins.CW.Core.Admin.Orders.Shipments.ShippingTo.Hint"] = "Only the items going to this destination are listed. To ship somewhere else on this order, go back and pick that destination.",
            ["Plugins.CW.Core.Admin.Orders.Shipments.OneDestinationOnly"] = "A shipment can only contain items going to one destination. Nothing was saved — pick a destination from the order page and try again.",
            ["Plugins.CW.Core.Admin.Orders.PackNameOrderNotEditable"] = "This order's lines and addresses cannot be edited here: they carry packName items, per-packName destinations and subsidy consumption. Use the packName order screens.",
            ["Plugins.CW.Core.Admin.Orders.Badge.Exchange"] = "E",
            ["Plugins.CW.Core.Admin.Orders.Badge.Exchange.Hint"] = "Order has exchanged items",
            ["Plugins.CW.Core.Admin.Orders.Badge.Return"] = "R",
            ["Plugins.CW.Core.Admin.Orders.Badge.Return.Hint"] = "Order has returned items",

            ["Plugins.CW.Core.Admin.Order.SubsidyCreditLabel"] = "Subsidy (items)",
            ["Plugins.CW.Core.Admin.Order.SubsidyCreditForShipping"] = "Subsidy (shipping)",
            ["Plugins.CW.Core.Admin.Order.SettleableTotal"] = "Total before subsidy and credit",
            ["Plugins.CW.Core.Admin.Order.CreditNotes"] = "Store credit",

            ["Plugins.CW.Core.Admin.Order.SubsidySummary"] = "Subsidy and store credit",
            ["Plugins.CW.Core.Admin.Order.SubsidyForStock"] = "Subsidy (items)",
            ["Plugins.CW.Core.Admin.Order.SubsidyForShipping"] = "Subsidy (shipping)",
            ["Plugins.CW.Core.Admin.Order.SubsidyTotal"] = "Subsidy applied",
            ["Plugins.CW.Core.Admin.Order.PayableBalance"] = "Payable balance",
            ["Plugins.CW.Core.Admin.Order.SubsidyApplied"] = "Subsidy",

            ["Plugins.CW.Core.Features.Disabled"] = "This feature is not enabled for this store.",
            ["Plugins.CW.Core.Features.Disabled.ReadOnly"] = "This feature is not enabled for this store. Existing records are shown for reference and cannot be changed.",

            //Public Store

            ["Plugins.CW.Core.PackNames.PackName.Field.Branch"] = "Branch",
            ["Plugins.CW.Core.PackNames.PackName.Field.Department"] = "Department",
            ["Plugins.CW.Core.PackNames.PackName.Field.CostCenter"] = "Cost Centre",
            ["Plugins.CW.Core.PackNames.PackName.Field.Name"] = "Name",
            ["Plugins.CW.Core.PackNames.PackName.Field.Group"] = "Group",
            ["Plugins.CW.Core.PackNames.PackName.Field.EmployeeNumber"] = "Employee Number",
            ["Plugins.CW.Core.PackNames.PackName.Field.PrintName"] = "Print Text",
            ["Plugins.CW.Core.PackNames.PackName.Field.POEntry"] = "PO Entry",
            ["Plugins.CW.Core.PackNames.PackName.Field.Comments"] = "Comments",
            ["Plugins.CW.Core.PackNames.PackName.Field.Gender"] = "Gender",
            ["Plugins.CW.Core.PackNames.PackName.Field.Colour"] = "Colour",
            ["Plugins.CW.Core.PackNames.PackName.Field.IsCustomer"] = "Customer",
            ["Plugins.CW.Core.PackNames.PackName.Field.AddAddress"] = "Add Ship Address",
            ["Plugins.CW.Core.PackNames.PackName.Field.AddCustomer"] = "Create Customer",
            ["Plugins.CW.Core.PackNames.PackName.Field.Subsidies"] = "Subsidies",
            ["Plugins.CW.Core.PackNames.PackName.Field.Subsidies.None"] = "This store has no active subsidy.",

            ["Plugins.CW.Core.PackNames.PackName.SelectBranch"] = "Select branch",
            ["Plugins.CW.Core.PackNames.PackName.Field.Branch.Required"] = "Branch is required",
            ["Plugins.CW.Core.PackNames.PackName.Field.Branch.DoesntExist"] = "Branch with name {0} doesn't exist",
            ["Plugins.CW.Core.PackNames.PackName.Field.Department.DefaultText"] = "Use Branch",
            ["Plugins.CW.Core.PackNames.PackName.Field.Department.DoesntExist"] = "Department with name {0} doesn't exist",
            ["Plugins.CW.Core.PackNames.PackName.Field.CostCenter.DefaultText"] = "Use Branch",
            ["Plugins.CW.Core.PackNames.PackName.Field.CostCenter.DoesntExist"] = "Cost center with name {0} doesn't exist",
            ["Plugins.CW.Core.PackNames.PackName.Field.Name.Required"] = "Name is required",
            ["Plugins.CW.Core.PackNames.PackName.Field.Name.Duplicated"] = "The name {0} is duplicated in the import file, it exists in row {1} for the branch {2}",
            ["Plugins.CW.Core.PackNames.PackName.Field.Name.Exists"] = "The name {0} already exists for the branch {1}",
            ["Plugins.CW.Core.PackNames.PackName.Field.Gender.Required"] = "Gender is required",
            ["Plugins.CW.Core.PackNames.PackName.Field.Gender.DoesntExist"] = "Gender {0} doesn't exist",
            ["Plugins.CW.Core.PackNames.PackName.Field.CountryCode.DoesntExist"] = "The country with code {0} doesn't exist",
            ["Plugins.CW.Core.PackNames.PackName.Field.StateProvince.DoesntExist"] = "The state / province with code {0} doesn't exist or is not valid for the country {1}",
            ["Plugins.CW.Core.PackNames.PackName.Field.DateOfBirth.NotValid"] = "Date of birth {0} is not valid",
            ["Plugins.CW.Core.PackNames.PackName.Field.GroupName"] = "Group Name",

            ["Plugins.CW.Core.PackNames.PackName.Field.Department.WrongStore"] = "The selected department is not available for this store",
            ["Plugins.CW.Core.PackNames.PackName.Field.CostCenter.WrongStore"] = "The selected cost centre is not available for this store",
            ["Plugins.CW.Core.PackNames.PackName.Field.Department.Inactive"] = "That department is no longer active and cannot take new assignments.",
            ["Plugins.CW.Core.PackNames.PackName.Field.CostCenter.Inactive"] = "That cost centre is no longer active and cannot take new assignments.",

            ["Plugins.CW.Core.PackNames.PackName.Field.Branch.Inactive"] = "That branch is no longer active and cannot take new assignments.",
            ["Plugins.CW.Core.PackNames.PackName.Field.Branch.NoAccess"] = "The manager has no access to any list from the selected branch.",
            ["Plugins.CW.Core.PackNames.PackName.Field.Branch.NoActiveList"] = "The selected branch has no active packname list.",
            ["Plugins.CW.Core.PackNames.PackName.Field.PackNameList.NotFound"] = "The packaging name list could not be found.",
            ["Plugins.CW.Core.PackNames.PackName.Field.Customer.NotFound"] = "The customer for this packaging name could not be found.",

            ["Plugins.CW.Core.PackNames.PackName.ImportXls"] = "Import PackNames from Excel",
            ["Plugins.CW.Core.PackNames.PackName.ImportXls.DownloadTemplate"] = "Download the template",
            ["Plugins.CW.Core.PackNames.PackName.ImportXls.ImportFromExcel"] = "Excel file to import",

            ["Plugins.CW.Core.PackNames.Import.Intro"] = "Add many packnames at once from one Excel file.",
            ["Plugins.CW.Core.PackNames.Import.Step.Template.Title"] = "Get the template",
            ["Plugins.CW.Core.PackNames.Import.Step.Template"] = "Its columns follow your store's settings, so download it again whenever the settings change.",
            ["Plugins.CW.Core.PackNames.Import.Step.Fill.Title"] = "Fill it in",
            ["Plugins.CW.Core.PackNames.Import.Step.Fill"] = "One row per packname, up to {0} rows per file. Pick values from the dropdowns, and write dates as Excel dates or as text like {1}.",
            ["Plugins.CW.Core.PackNames.Import.Step.Upload.Title"] = "Upload and import",
            ["Plugins.CW.Core.PackNames.Import.Step.Upload"] = "Drop the Excel file here, or",
            ["Plugins.CW.Core.PackNames.Import.ChooseFile"] = "choose a file",
            ["Plugins.CW.Core.PackNames.Import.ChangeFile"] = "Choose another file",
            ["Plugins.CW.Core.PackNames.Import.Columns"] = "Show the template's {0} columns",
            ["Plugins.CW.Core.PackNames.Import.AllOrNothing"] = "Every row is checked before anything is saved. If one row has a problem, nothing is imported.",
            ["Plugins.CW.Core.PackNames.Import.Button"] = "Import",
            ["Plugins.CW.Core.PackNames.Import.Working"] = "Importing…",
            ["Plugins.CW.Core.PackNames.Import.WorkingHint"] = "Checking and importing. A full file can take about half a minute; keep this page open.",
            ["Plugins.CW.Core.PackNames.Import.NoFile"] = "Choose the Excel file to import first.",
            ["Plugins.CW.Core.PackNames.Import.DownloadReport"] = "Download the error report",
            ["Plugins.CW.Core.PackNames.Import.Result.Created"] = "{0} packname(s) imported",
            ["Plugins.CW.Core.PackNames.Import.Result.Customers"] = "{0} customer account(s) created.",
            ["Plugins.CW.Core.PackNames.Import.Result.WelcomeEmails"] = "{0} of them will receive a welcome e-mail with a link to set a password.",
            ["Plugins.CW.Core.PackNames.Import.Result.Lists"] = "{0} packname list(s) created for branches without an active list.",
            ["Plugins.CW.Core.PackNames.Import.Result.Failed"] = "Nothing was imported",
            ["Plugins.CW.Core.PackNames.Import.Result.PartlyTitle"] = "The import stopped at row {0}",
            ["Plugins.CW.Core.PackNames.Import.Result.Partly"] = "The rows above row {0} were imported; row {0} and the rows after it were not. Remove the imported rows from the file before you upload it again.",
            ["Plugins.CW.Core.PackNames.Import.Result.ProblemRows"] = "{0} of {1} row(s) need fixing.",
            ["Plugins.CW.Core.PackNames.Import.Result.FixHint"] = "Fix them in your file and upload it again, or download the error report: your file with an Errors column that lists each row's problems.",
            ["Plugins.CW.Core.PackNames.Import.Result.Row"] = "Row {0}",
            ["Plugins.CW.Core.PackNames.Import.Result.Error"] = "Rows to fix",
            ["Plugins.CW.Core.PackNames.Import.Errors.InvalidFile"] = "The file could not be read as an Excel workbook (.xlsx).",
            ["Plugins.CW.Core.PackNames.Import.Errors.FileTooLarge"] = "The file is larger than {0} MB.",
            ["Plugins.CW.Core.PackNames.Import.Errors.MissingColumns"] = "The file is missing the column(s): {0}. Download the template again.",
            ["Plugins.CW.Core.PackNames.Import.Errors.NoRows"] = "The file has no rows to import.",
            ["Plugins.CW.Core.PackNames.Import.Errors.TooManyRows"] = "The file has more than {0} rows; split it into files of at most {0} rows.",
            ["Plugins.CW.Core.PackNames.Import.Errors.RetiredColumns"] = "The file has values in column(s) your store no longer takes: {0}. Download the template again and copy the rows into it.",
            ["Plugins.CW.Core.PackNames.Import.Errors.DuplicatedColumns"] = "The file has the column(s) {0} more than once. Keep one of each and upload it again.",
            ["Plugins.CW.Core.PackNames.Import.Result.Rows"] = "{0} row(s) checked.",
            ["Plugins.CW.Core.PackNames.Import.WholeStore"] = "whole store",
            ["Plugins.CW.Core.PackNames.Import.Errors.TooLong"] = "{0} is longer than {1} characters",
            ["Plugins.CW.Core.PackNames.Import.Errors.Unexpected"] = "Something went wrong while importing row {0}. The error has been logged; please tell the store administrator.",
            ["Plugins.CW.Core.PackNames.Import.Errors.UnexpectedCheck"] = "Something went wrong while checking the file, so nothing was imported. The error has been logged; please tell the store administrator.",
            ["Plugins.CW.Core.PackNames.Import.AddressMessage"] = "Delivery address: {0}",
            ["Plugins.CW.Core.PackNames.Import.Result.ListsUsed"] = "Where they went",
            ["Plugins.CW.Core.PackNames.Import.RequestFailed"] = "The import could not be completed. Reload the page and try again.",

            ["Plugins.CW.Core.PackNames.PackName.Field.Group.NoCustomer"] = "A group cannot have a customer account",
            ["Plugins.CW.Core.PackNames.PackName.Field.YesNo.NotValid"] = "{0} must be Yes or No",
            ["Plugins.CW.Core.PackNames.PackName.Field.Email.Duplicated"] = "The e-mail {0} is already used in row {1}",
            ["Plugins.CW.Core.PackNames.PackName.Field.Username.Duplicated"] = "The username {0} is already used in row {1}",
            ["Plugins.CW.Core.PackNames.PackName.Field.Attribute.ValueDoesntExist"] = "{0}: {1} is not one of the options",
            ["Plugins.CW.Core.PackNames.PackName.Field.Subsidy.DoesntExist"] = "Subsidy {0} doesn't exist or is not active",
            ["Plugins.CW.Core.PackNames.PackName.Field.Subsidy.NamedTwice"] = "Subsidy {0} is named again in {1}: give its credit and dates where it is first named",
            ["Plugins.CW.Core.PackNames.PackName.Field.Subsidy.NameRequired"] = "{0} is needed when its credit or dates are given",
            ["Plugins.CW.Core.PackNames.PackName.Field.Subsidy.CreditNotValid"] = "{0}: {1} is not a valid amount",
            ["Plugins.CW.Core.PackNames.PackName.Field.Date.NotValid"] = "{0}: {1} is not a valid date (use yyyy-mm-dd)",
            ["Plugins.CW.Core.PackNames.PackName.Subsidies.VariationOnCreate"] = "Set when the packname was created",

            ["Plugins.CW.Core.PackNames.PackName.Address"] = "Address",

            ["Plugins.CW.Core.PackNames.PackNameSelector.PackNameList"] = "List",
            ["Plugins.CW.Core.PackNames.PackNameSelector.PackName"] = "Order for",
            ["Plugins.CW.Core.PackNames.PackNameSelector.AddPackName"] = "Add PackName",
            ["Plugins.CW.Core.PackNames.PackNameSelector.AddPackNameExcel"] = "Import PackNames from Excel",
            ["Plugins.CW.Core.PackNames.PackNameSelector.TypeToSearch"] = "Showing the first {0} packnames. Type a name to find the others.",
            ["Plugins.CW.Core.PackNames.PackNameSelector.TypeToNarrow"] = "More than {0} packnames match. Keep typing to narrow the search.",

            ["Plugins.CW.Core.PackNames.Add"] = "Add PackName",
            ["Plugins.CW.Core.PackNames.AddGroup"] = "Add Group",
            ["Plugins.CW.Core.PackNames.Edit"] = "Edit PackName",
            ["Plugins.CW.Core.PackNames.EditGroup"] = "Edit Group",

            ["Plugins.CW.Core.Department.Abbreviation"] = "Dept",

            ["Plugins.CW.Core.CustomerSubsidies"] = "Subsidies",
            ["Plugins.CW.Core.CustomerCreditsForMe"] = "My Store Credit",

            ["Plugins.CW.Core.ShoppingCart.Mini.CurrentPackName"] = "{0}'s cart:",
            ["Plugins.CW.Core.ShoppingCart.Mini.AllPackNames"] = "Total in cart:",
            ["Plugins.CW.Core.ShoppingCart.Mini.NoItemsForCurrentPackName"] = "There are no items in the shopping cart for the packName.",
            ["Plugins.CW.Core.ShoppingCart.Mini.Remove"] = "Remove this item",
            ["Plugins.CW.Core.ShoppingCart.CannotRemoveReturnedItem"] = "This item belongs to a return request and cannot be removed from the cart.",
            ["Plugins.CW.Core.ShoppingCart.CannotChangeReturnedItem"] = "This item is part of an approved return and cannot be reduced below {0}. You can still increase the quantity.",
            ["Plugins.CW.Core.ShoppingCart.ItemNotFound"] = "This item is no longer in your cart. Please refresh the page.",

            ["Plugins.CW.Core.PackNameCheckout.ShippingAddress"] = "Shipping Address",
            ["Plugins.CW.Core.PackNameCheckout.EmptyShoppingCart"] = "Your Shopping Cart is empty!",
            ["Plugins.CW.Core.PackNameCheckout.CheckoutAttributes"] = "Checkout attributes",
            ["Plugins.CW.Core.PackNameCheckout.EnterShippingAddress"] = "Shipping addresses or default shipping method is missing for some branches",
            ["Plugins.CW.Core.PackNameCheckout.EnterShippingAddressPackName"] = "The shipping address is missing for some packNames",
            ["Plugins.CW.Core.PackNameCheckout.EnterShippingMethodPackName"] = "The shipping method is missing for some packNames",
            ["Plugins.CW.Core.PackNameCheckout.InvalidShippingAddress"] = "The selected address cannot be used for this packname.",
            ["Plugins.CW.Core.PackNameCheckout.EnterPaymnetMethod"] = "Payment Required",
            ["Plugins.CW.Core.PackNameCheckout.NoPaymentMethodsOrRequired"] = "No payment methods available or payment method is not required",
            ["Plugins.CW.Core.PackNameCheckout.DefaultShippingAddressName"] = "{0} Branch - Default",
            ["Plugins.CW.Core.PackNameCheckout.ShippingAddresses"] = "Addresses",
            ["Plugins.CW.Core.PackNameCheckout.ShippingAddresses.AddNew"] = "Add New",
            ["Plugins.CW.Core.PackNameCheckout.ShippingAddresses.AddAddress"] = "Add Address",
            ["Plugins.CW.Core.PackNameCheckout.ViewByPackName"] = "View by packName",
            ["Plugins.CW.Core.PackNameCheckout.ViewByProduct"] = "View by product",

            ["Plugins.CW.Core.PackNameCheckout.ShippingAddresses.NotSelected"] = "No shipping address selected",
            ["Plugins.CW.Core.PackNameCheckout.ShippingAddresses.Available"] = "{0} shipping addresses available",
            ["Plugins.CW.Core.PackNameCheckout.ShippingAddresses.Available.SeeMore"] = "see more",

            ["Plugins.CW.Core.PackNameCheckout.ShippingMethods"] = "Shipping Methods",
            ["Plugins.CW.Core.PackNameCheckout.SharedDestination"] = "Shipped in one parcel with {0}. Shipping to this address is charged once: {1}.",
            ["Plugins.CW.Core.PackNameCheckout.ShippingMethod.NotSelected"] = "No shipping method selected",

            ["Plugins.CW.Core.Order.PackNameOrderMustUsePackNameCheckout"] = "This order contains PackName items and must be placed through the PackName checkout.",
            ["Plugins.CW.Core.Order.PackNameCartNotOrderable"] = "None of the packname items in your cart can be ordered right now. Open the checkout to see why.",
            ["Plugins.CW.Core.Order.PackNameCartHasOtherItems"] = "Your cart holds items that cannot be ordered with your packname order. Remove them from the cart, then pay again.",
            ["Plugins.CW.Core.Order.PaymentAmountDiffers"] = "The amount approved for payment differs from the order total. No order was placed; please start the payment again.",
            ["Plugins.CW.Core.PackNameCheckout.OwnPlacementPaymentNote"] = "Press Checkout to continue to the payment page. Your order is placed once the payment is approved.",
            ["Plugins.CW.Core.PackNameCheckout.PaymentHandoff"] = "Taking you to the payment page...",
            ["Plugins.CW.Core.PackNameCheckout.BackToCheckout"] = "Back to checkout",
            ["Plugins.CW.Core.ReOrder.PackNameNoLongerManaged"] = "The items for {0} were not added: you no longer order for this packname.",

            ["Plugins.CW.Core.Order.SharedShippingWith"] = "Shared with: {0}",
            ["Plugins.CW.Core.Order.PackNameSection.Title"] = "PackNames",
            ["Plugins.CW.Core.Order.Shipments.Destination"] = "Delivered to",

            ["Plugins.CW.Core.Pdf.ShippingDestination.Packs"] = "Packs",
            ["Plugins.CW.Core.Pdf.GrossTotal"] = "Total before subsidy and credit",
            ["Plugins.CW.Core.Pdf.SubsidyForStock"] = "Subsidy (items)",
            ["Plugins.CW.Core.Pdf.SubsidyForShipping"] = "Subsidy (shipping)",
            ["Plugins.CW.Core.Pdf.CreditNotes"] = "Store credit",
            ["Plugins.CW.Core.Pdf.ExchangeFees"] = "Exchange fees",
            ["Plugins.CW.Core.Pdf.PayableBalance"] = "Payable balance",
            ["Plugins.CW.Core.Pdf.SubTotalDiscount"] = "Subtotal discount",
            ["Plugins.CW.Core.Pdf.OrderDiscount"] = "Order discount",
            ["Plugins.CW.Core.Pdf.Product.For"] = "For: {0}",

            ["Plugins.CW.Core.Message.Order.ShippingAddress"] = "Shipping Address",
            ["Plugins.CW.Core.Message.Order.ShippingMethod"] = "Shipping Method",

            ["Plugins.CW.Core.SubsidyCatalog.PageTitle"] = "My Subsidies",
            ["Plugins.CW.Core.SubsidyCatalog.ProductGroup.Exhausted"] = "Completed",

            ["Plugins.CW.Core.SubsidyBar.Available.Title"] = "Available",
            ["Plugins.CW.Core.SubsidyBar.GlobalCreditRemaining"] = "Global Subsidy Remaining",
            ["Plugins.CW.Core.SubsidyBar.GlobalCreditUsed"] = "Global Subsidy Used",
            ["Plugins.CW.Core.SubsidyBar.NothingAvailable"] = "Nothing available",
            ["Plugins.CW.Core.SubsidyBar.NotAvailable"] = "N/A",
            ["Plugins.CW.Core.SubsidyBar.Cart.Title"] = "Cart — {0}",
            ["Plugins.CW.Core.SubsidyBar.Cart.Empty"] = "No items in cart",
            ["Plugins.CW.Core.SubsidyBar.Legend.Available"] = "Available",
            ["Plugins.CW.Core.SubsidyBar.Legend.InCart"] = "In Cart",
            ["Plugins.CW.Core.SubsidyBar.Legend.Used"] = "Used",
            ["Plugins.CW.Core.SubsidyBar.ChangeTopup"] = "Change Topup",
            ["Plugins.CW.Core.SubsidyBar.ChangeTopup.Title"] = "Manage Topup",

            ["Plugins.CW.Core.Subsidy.Topup.Fields.Credit"] = "Topup Credit",
            ["Plugins.CW.Core.Subsidy.Topup.Fields.Reason"] = "Reason",
            ["Plugins.CW.Core.Subsidy.Topup.Fields.Reason.Required"] = "Reason is required.",
            ["Plugins.CW.Core.Subsidy.Topup.Fields.Credit.Required"] = "Credit to add must be greater than zero.",

            ["Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Quantity"] = "Topup Qty",
            ["Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Credit"] = "Topup Credit",
            ["Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Reason"] = "Reason",
            ["Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Reason.Required"] = "Reason is required.",
            ["Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Credit.Required"] = "At least one of Quantity or Credit must be greater than zero.",

            ["Plugins.CW.Core.ShoppingCart.Totals.CreditNotes"] = "Store credit",
            ["Plugins.CW.Core.ShoppingCart.Totals.ExchangeFees"] = "Exchange fees",
            ["Plugins.CW.Core.ShoppingCart.Totals.SubTotalDiscount"] = "Subtotal discount",
            ["Plugins.CW.Core.ShoppingCart.Totals.OrderTotalDiscount"] = "Order discount",

            ["Plugins.CW.Core.Subsidy.ShoppingCart.Totals.Subsidy"] = "Subsidy applied",
            ["Plugins.CW.Core.Subsidy.ShoppingCart.Totals.SubsidyForStock"] = "Subsidy (items)",
            ["Plugins.CW.Core.Subsidy.ShoppingCart.Totals.SubsidyForShipping"] = "Subsidy (shipping)",
            ["Plugins.CW.Core.Subsidy.ShoppingCart.Totals.PayableBalance"] = "Payable balance",
            ["Plugins.CW.Core.Subsidy.ShoppingCart.Totals.SettleableTotal"] = "Total before subsidy and credit",
            ["Plugins.CW.Core.Subsidy.ShoppingCart.SubsidyQtyExceeded"] = "The quantity exceeds the amount allocated for this item by your subsidy. There are {0} left.",
            ["Plugins.CW.Core.PackName.CannotOrder"] = "Ordering is closed for {0}: the subsidy ordering period has ended.",
            ["Plugins.CW.Core.PackName.BlockOrderingReason"] = "Ordering is closed for this packname.",
            ["Plugins.CW.Core.PackName.NotAvailableForOrdering"] = "This packname is not available for ordering.",

            ["Plugins.CW.Core.Subsidy.GlobalCredit"] = "Global subsidy credit",
            ["Plugins.CW.Core.Subsidy.ShippingCoveredBy"] = "Shipping covered by subsidy:",

            ["Plugins.CW.Core.PackNameInfo.ViewSubsidy"] = "View my subsidy",
            ["Plugins.CW.Core.PackNameInfo.ViewFullSubsidy"] = "View full subsidy",
            ["Plugins.CW.Core.PackNameInfo.SubsidyFlyoutError"] = "Could not load subsidy",
            ["Plugins.CW.Core.PackNameInfo.NoShippingAddress"] = "{0} has no shipping address set up: an order cannot be placed until one is added.",

            // Change Topup UI
            ["Plugins.CW.Core.ChangeTopup.NoSubsidies"] = "No active subsidies found for this period.",
            ["Plugins.CW.Core.ChangeTopup.BaseCredit"] = "Subsidy $",
            ["Plugins.CW.Core.ChangeTopup.TopupCredit"] = "Topup $",
            ["Plugins.CW.Core.ChangeTopup.TopupQty"] = "Topup Unit Qty",
            ["Plugins.CW.Core.ChangeTopup.ProductGroup"] = "Product Group",
            ["Plugins.CW.Core.ChangeTopup.BaseQty"] = "Unit Qty",
            ["Plugins.CW.Core.ChangeTopup.Change"] = "Change",
            ["Plugins.CW.Core.ChangeTopup.Save"] = "Save",
            ["Plugins.CW.Core.ChangeTopup.SubsidyTopup.DialogTitle"] = "Add Subsidy Topup",
            ["Plugins.CW.Core.ChangeTopup.ProductGroupTopup.DialogTitle"] = "Add Product Group Topup",
            ["Plugins.CW.Core.ChangeTopup.AccessDenied"] = "You cannot change this top-up.",
            ["Plugins.CW.Core.ChangeTopup.PeriodClosed"] = "This subsidy's ordering period is closed, so it cannot be topped up.",

            ["Plugins.CW.Core.Order.SubsidyUsed"] = "Subsidy applied",
            ["Plugins.CW.Core.Order.StockSubsidy"] = "Subsidy (items)",
            ["Plugins.CW.Core.Order.ShippingSubsidy"] = "Subsidy (shipping)",
            ["Plugins.CW.Core.Order.PayableBalance"] = "Payable balance",
            ["Plugins.CW.Core.Order.CreditNotes"] = "Store credit",
            ["Plugins.CW.Core.Order.SettleableTotal"] = "Total before subsidy and credit",
            ["Plugins.CW.Core.Order.SubTotalDiscount"] = "Subtotal discount",
            ["Plugins.CW.Core.Order.OrderDiscount"] = "Order discount",
            ["Plugins.CW.Core.Order.SubsidyUnits"] = "{0} items",
            ["Plugins.CW.Core.Order.SubsidyQtyExceeded"] = "{0}: the subsidy allocation allows {1} more unit(s) of this product, fewer than the order asks for.",

            ["Plugins.CW.Core.Messages.Order.SettleableTotal"] = "Total before subsidy and credit:",
            ["Plugins.CW.Core.Messages.Order.SubsidyForStock"] = "Subsidy (items):",
            ["Plugins.CW.Core.Messages.Order.SubsidyForShipping"] = "Subsidy (shipping):",
            ["Plugins.CW.Core.Messages.Order.CreditNotes"] = "Store credit:",
            ["Plugins.CW.Core.Messages.Order.SubTotalDiscount"] = "Subtotal discount:",
            ["Plugins.CW.Core.Messages.Order.OrderDiscount"] = "Order discount:",
            ["Plugins.CW.Core.Messages.Order.PayableBalance"] = "Payable balance:",

            // Exchanges — customer-facing return wizard
            ["Plugins.CW.Core.Exchanges.Continue"] = "Continue",
            ["Plugins.CW.Core.Exchanges.CreditNoteNotAvailable"] = "A credit note is not available for the selected reason and action. Please choose a different combination.",

            // Wizard — validation
            ["Plugins.CW.Core.Exchanges.Wizard.ModeRequired"] = "Choose whether you would like an exchange or a credit note.",
            ["Plugins.CW.Core.Exchanges.Wizard.NoItemsSelected"] = "Select at least one item to return.",
            ["Plugins.CW.Core.Exchanges.Wizard.ExchangeTypeRequired"] = "Choose an exchange type for every item you are returning.",
            ["Plugins.CW.Core.Exchanges.Wizard.ReplacementRequired"] = "Choose a replacement product for every item you are exchanging.",
            ["Plugins.CW.Core.Exchanges.Wizard.SessionExpired"] = "Your return session has expired. Please start again.",

            // Wizard — progress indicator
            ["Plugins.CW.Core.Exchanges.Wizard.Steps.Progress"] = "Return request progress",
            ["Plugins.CW.Core.Exchanges.Wizard.Steps.Completed"] = "completed",
            ["Plugins.CW.Core.Exchanges.Wizard.Steps.SelectItems"] = "Select items",
            ["Plugins.CW.Core.Exchanges.Wizard.Steps.Reason"] = "Reason",
            ["Plugins.CW.Core.Exchanges.Wizard.Steps.Options"] = "Options",
            ["Plugins.CW.Core.Exchanges.Wizard.Steps.Confirm"] = "Confirm",

            // Wizard — gather step
            ["Plugins.CW.Core.Exchanges.Wizard.PageTitle"] = "Return Request - Additional Info",
            ["Plugins.CW.Core.Exchanges.Wizard.ChooseMode"] = "Tell us how you would like to proceed with the returned items",
            ["Plugins.CW.Core.Exchanges.Wizard.RequestCreditNote"] = "Request Credit Note",
            ["Plugins.CW.Core.Exchanges.Wizard.RequestExchange"] = "Request Exchange",
            ["Plugins.CW.Core.Exchanges.Wizard.CreditNoteItems"] = "Below are the items you will receive a Credit Note for:",
            ["Plugins.CW.Core.Exchanges.Wizard.ExchangeItems"] = "Specify what you would like to do with each returned set of items",
            ["Plugins.CW.Core.Exchanges.Wizard.NoReturnOptions"] = "There are no return options available for the reason and action you selected. Please choose a different combination.",
            ["Plugins.CW.Core.Exchanges.Wizard.ChooseDifferentReason"] = "Choose a different reason",
            ["Plugins.CW.Core.Exchanges.Wizard.Back"] = "Back to reason and items",
            ["Plugins.CW.Core.Exchanges.Wizard.NextStep"] = "Next Step",

            // Wizard — returned items table
            ["Plugins.CW.Core.Exchanges.Wizard.Columns.Product"] = "Product",
            ["Plugins.CW.Core.Exchanges.Wizard.Columns.UnitFullPrice"] = "Unit Full Price",
            ["Plugins.CW.Core.Exchanges.Wizard.Columns.Quantity"] = "Qty",
            ["Plugins.CW.Core.Exchanges.Wizard.Columns.ExchangeType"] = "Exchange Type",
            ["Plugins.CW.Core.Exchanges.Wizard.Columns.ExchangeProduct"] = "Exchange Product",
            ["Plugins.CW.Core.Exchanges.Wizard.Columns.PriceDifference"] = "Price difference",
            ["Plugins.CW.Core.Exchanges.Wizard.Fields.Packname"] = "Packname",
            ["Plugins.CW.Core.Exchanges.Wizard.NoReplacement"] = "None",
            ["Plugins.CW.Core.Exchanges.Wizard.Change"] = "Change",

            // Wizard — fees
            ["Plugins.CW.Core.Exchanges.Wizard.Fees.Title"] = "Fees that may apply to your exchange",
            ["Plugins.CW.Core.Exchanges.Wizard.Fees.Disclaimer"] = "These are estimates. Final fees are confirmed when our team reviews your return.",

            // Wizard — next step hints (rendered by JavaScript, {0} is the number of unresolved items)
            ["Plugins.CW.Core.Exchanges.Wizard.Hint.ChooseTypeSingular"] = "Choose what you would like to do with the remaining item.",
            ["Plugins.CW.Core.Exchanges.Wizard.Hint.ChooseTypePlural"] = "Choose what you would like to do with the remaining {0} items.",
            ["Plugins.CW.Core.Exchanges.Wizard.Hint.ChooseReplacementSingular"] = "Choose a replacement product for the remaining item.",
            ["Plugins.CW.Core.Exchanges.Wizard.Hint.ChooseReplacementPlural"] = "Choose a replacement product for the remaining {0} items.",

            // Wizard — replacement product picker
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.Title"] = "Select an exchange product",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.Search"] = "Search",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.SearchLabel"] = "Search products",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.SearchPlaceholder"] = "Search by name or SKU",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.NoProducts"] = "There are no products available to exchange for.",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.NoMatches"] = "No products match \"{0}\".",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.Choose"] = "Choose",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.ItemBeingReturned"] = "Item being returned",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.Previous"] = "Previous",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.Next"] = "Next",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.PageStatus"] = "Page {0} of {1}",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.Back"] = "Back to products",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.ChooseThisProduct"] = "Choose this product",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.LoadingProducts"] = "Loading products...",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.LoadingOptions"] = "Loading options...",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.LoadProductsError"] = "Could not load products: {0}",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.LoadOptionsError"] = "Could not load product options: {0}",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.SelectAllOptions"] = "Please choose every option for this product before selecting it.",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.OptionsNotAvailable"] = "This product cannot be exchanged online because it needs details we cannot take here ({0}). Please choose a different product, or contact us and we will arrange this exchange for you.",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.RentalDates"] = "rental dates",
            ["Plugins.CW.Core.Exchanges.Wizard.Picker.GiftCardDetails"] = "gift card details",

            // Wizard — editing an exchange already submitted
            ["Plugins.CW.Core.Exchanges.Wizard.Edit.SameCombination"] = "This is the same combination already on the exchange. Choose a different option, or close this window.",
            ["Plugins.CW.Core.Exchanges.Wizard.Edit.UpdateFailed"] = "The exchange item could not be updated.",
            ["Plugins.CW.Core.Exchanges.Wizard.Edit.UpdateError"] = "Could not update the exchange item: {0}",

            // Exchanges — summary step
            ["Plugins.CW.Core.Exchanges.Summary.Title"] = "Return Request - Confirm",
            ["Plugins.CW.Core.Exchanges.Summary.AlmostDone"] = "Almost done",
            ["Plugins.CW.Core.Exchanges.Summary.Heading"] = "Review and confirm your return",
            ["Plugins.CW.Core.Exchanges.Summary.Subheading"] = "Check the details below, then confirm to submit your request.",
            ["Plugins.CW.Core.Exchanges.Summary.Information"] = "Return request information",
            ["Plugins.CW.Core.Exchanges.Summary.Order"] = "Order",
            ["Plugins.CW.Core.Exchanges.Summary.Method"] = "Requested method",
            ["Plugins.CW.Core.Exchanges.Summary.ReturnAction"] = "Return action",
            ["Plugins.CW.Core.Exchanges.Summary.ReturnReason"] = "Return reason",
            ["Plugins.CW.Core.Exchanges.Summary.Comments"] = "Comments",
            ["Plugins.CW.Core.Exchanges.Summary.Items"] = "Items being returned",
            ["Plugins.CW.Core.Exchanges.Summary.Totals"] = "Summary",
            ["Plugins.CW.Core.Exchanges.Summary.CreditNotesTotal"] = "Credit notes — estimated maximum",
            ["Plugins.CW.Core.Exchanges.Summary.EstimatedFees"] = "Fees estimates",
            ["Plugins.CW.Core.Exchanges.Summary.Cancel"] = "Cancel",
            ["Plugins.CW.Core.Exchanges.Summary.Confirm"] = "Confirm return",
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.NegativeAmount"] = "<strong>(*)</strong> A <em>negative</em> amount is in <strong>your favor</strong>. Anything you paid for with your allowance goes back to your allowance; only what you paid by card or cash comes back as store credit.",
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.FeesEstimate"] = "<strong>(**)</strong> Fee amounts are <strong>estimates</strong> and may change after our staff reviews your return request.",
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.FeesNotExcluded"] = "Redelivery and repackaging fees are not excluded from the total even if there is an amount in your favor.",
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.SubsidyReturned"] = "If this item was paid for with your allowance, that value is returned to your allowance when we approve the return, so the amounts above may be higher than what you actually receive as store credit.",
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.CreditNoteApproval"] = "The credit note amounts above are credited to your account once our staff approves the return request.",
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.CreditNoteFees"] = "Fee amounts may vary as our staff can apply additional fees after reviewing your request.",

            // Exchanges — confirmation panel shown after a return is submitted
            ["Plugins.CW.Core.Exchanges.Confirmation.DialogTitle"] = "Return requests created",
            ["Plugins.CW.Core.Exchanges.Confirmation.Lead"] = "Your return request has been logged. <strong>Please print this page</strong> and include it in the box with the garments.",
            ["Plugins.CW.Core.Exchanges.Confirmation.PrintReminder"] = "Please print the return request and include it in the box with the garments.",
            ["Plugins.CW.Core.Exchanges.Confirmation.Order"] = "Order #",
            ["Plugins.CW.Core.Exchanges.Confirmation.Packname"] = "Packname",
            ["Plugins.CW.Core.Exchanges.Confirmation.Attributes"] = "Attributes",
            ["Plugins.CW.Core.Exchanges.Confirmation.ExchangeType"] = "Exchange type",
            ["Plugins.CW.Core.Exchanges.Confirmation.ReplacementProduct"] = "Replacement product",
            ["Plugins.CW.Core.Exchanges.Confirmation.PrintAll"] = "Print all",
            ["Plugins.CW.Core.Exchanges.Confirmation.Close"] = "Close",

            // Exchanges — extra detail shown on the customer's return request list
            ["Plugins.CW.Core.Exchanges.ReturnRequests.ExchangeType"] = "Exchange Type:",
            ["Plugins.CW.Core.Exchanges.ReturnRequests.ReplacementProduct"] = "Replacement Product:",
            ["Plugins.CW.Core.Exchanges.ReturnRequests.EditExchangeItem"] = "Edit Exchange Item",

            // Exchanges — return wizard: start page, checks, picker labels, summary totals, receipts
            ["Plugins.CW.Core.Exchanges.Wizard.Start.Step1"] = "<strong>Step 1 -</strong> Specify the items you want to return or exchange",
            ["Plugins.CW.Core.Exchanges.Wizard.Start.Step2"] = "<strong>Step 2 -</strong> Tell us why you are returning these items",
            ["Plugins.CW.Core.Exchanges.Wizard.Start.ReturnsUnavailable"] = "Returns can't be requested online at the moment. Please contact us about returning items from this order.",
            ["Plugins.CW.Core.Exchanges.Wizard.SelectPlaceholder"] = "- Select -",
            ["Plugins.CW.Core.Exchanges.Wizard.Fees.PerItem"] = "{0} per item",
            ["Plugins.CW.Core.Exchanges.Wizard.Fees.PerItems"] = "{0} per {1} items",
            ["Plugins.CW.Core.Exchanges.Wizard.ExchangeTypeFor"] = "Exchange type for {0}",
            ["Plugins.CW.Core.Exchanges.Wizard.ChooseReplacement"] = "Choose",
            ["Plugins.CW.Core.Exchanges.Wizard.ChooseReplacementFor"] = "Choose a replacement for {0}",
            ["Plugins.CW.Core.Exchanges.Wizard.ChangeReplacementFor"] = "Change the replacement for {0}",
            ["Plugins.CW.Core.Exchanges.Wizard.ReasonActionNotAvailable"] = "The reason and action you chose are not available together. Please choose them again.",
            ["Plugins.CW.Core.Exchanges.Wizard.QuantityNotAvailable"] = "You asked to return more units than you can. Please choose the quantities again.",
            ["Plugins.CW.Core.Exchanges.Wizard.ModeNotAvailable"] = "The return option you chose is not available for this reason and action.",
            ["Plugins.CW.Core.Exchanges.Wizard.ReplacementNotAvailable"] = "A replacement you chose is not available for its exchange type. Please choose it again.",
            ["Plugins.CW.Core.Exchanges.Wizard.Edit.NotFound"] = "Return request not found.",
            ["Plugins.CW.Core.Exchanges.Wizard.Edit.NotPending"] = "This return request can no longer be changed.",
            ["Plugins.CW.Core.Exchanges.Wizard.Edit.NoExchangeItem"] = "This return request does not have an exchange item.",
            ["Plugins.CW.Core.Exchanges.Summary.Totals.PriceDifferences"] = "Price differences total (*)",
            ["Plugins.CW.Core.Exchanges.Summary.Totals.Fees"] = "Total fees (**)",
            ["Plugins.CW.Core.Exchanges.Summary.Totals.MightPay"] = "You might have to pay",
            ["Plugins.CW.Core.Exchanges.Summary.Totals.MostCreditNotes"] = "Most you could receive as credit notes",
            ["Plugins.CW.Core.Exchanges.Receipt.NotFound"] = "The return request was not found.",
            ["Plugins.CW.Core.Exchanges.Receipt.PrintError"] = "The return request receipt could not be printed. Please try again later.",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.FeeType.ReShippingFee"] = "Redelivery Fee",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.FeeType.RepackagingFee"] = "Repackaging Fee",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.FeeType.AdditionalFee"] = "Additional Fees",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.ExchangeType.SameStyle"] = "Same Style",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.ExchangeType.DifferentStyle"] = "Different Style",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.ExchangeType.Exact"] = "Exact Exchange",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.ExchangeReturnMode.Exchange"] = "Exchange",
            ["Enums.Nop.Plugin.CW.Core.Domain.Exchanges.ExchangeReturnMode.CreditNote"] = "Credit Note",

            // Store credit — customer-facing "My Store Credit" page
            ["Plugins.CW.Core.StoreCredit.PageTitle"] = "My Store Credit",
            ["Plugins.CW.Core.StoreCredit.AvailableBalance"] = "Available Balance",
            ["Plugins.CW.Core.StoreCredit.NoCredit"] = "You don't have any store credit yet.",
            ["Plugins.CW.Core.StoreCredit.IssuedNotes"] = "Your credit notes",
            ["Plugins.CW.Core.StoreCredit.Ledger"] = "Credit applied to orders",
            ["Plugins.CW.Core.StoreCredit.Fields.Description"] = "Description",
            ["Plugins.CW.Core.StoreCredit.Fields.Order"] = "Order #",
            ["Plugins.CW.Core.StoreCredit.Fields.Date"] = "Date",
            ["Plugins.CW.Core.StoreCredit.Fields.Amount"] = "Amount",
            ["Plugins.CW.Core.StoreCredit.Fields.Remaining"] = "Remaining",
            ["Plugins.CW.Core.StoreCredit.Fields.Applied"] = "Applied",

            // My Account > Subsidies
            ["Plugins.CW.Core.CustomerSubsidies.PageTitle"] = "Subsidies",
            ["Plugins.CW.Core.CustomerSubsidies.ShowingPackName"] = "Showing the packname you are ordering for \u2014 change it in the packname selector.",
            ["Plugins.CW.Core.CustomerSubsidies.NoSubsidies"] = "No subsidy is assigned to this packname.",
            ["Plugins.CW.Core.CustomerSubsidies.Status.Open"] = "Open until {0}",
            ["Plugins.CW.Core.CustomerSubsidies.Status.OpenRecurring"] = "Open until {0} \u00b7 resets every year",
            ["Plugins.CW.Core.CustomerSubsidies.Status.NotStarted"] = "Opens {0}",
            ["Plugins.CW.Core.CustomerSubsidies.Status.Closed"] = "Closed since {0}",
            ["Plugins.CW.Core.CustomerSubsidies.Remaining"] = "Remaining to spend",
            ["Plugins.CW.Core.CustomerSubsidies.RemainingUnits"] = "Remaining",
            ["Plugins.CW.Core.CustomerSubsidies.CreditWhenOpen"] = "When it opens",
            ["Plugins.CW.Core.CustomerSubsidies.Used"] = "Used",
            ["Plugins.CW.Core.CustomerSubsidies.Total"] = "Total",
            ["Plugins.CW.Core.CustomerSubsidies.CreditBreakdown"] = "= credit {0} + top-up {1}",
            ["Plugins.CW.Core.CustomerSubsidies.Units"] = "{0} units",
            ["Plugins.CW.Core.CustomerSubsidies.NoMoneyCredit"] = "no money credit on this subsidy",
            ["Plugins.CW.Core.CustomerSubsidies.NotStartedNote"] = "Not in its ordering period yet \u2014 nothing can be spent from it until it opens.",
            ["Plugins.CW.Core.CustomerSubsidies.ClosedNote"] = "Its ordering period has ended.",
            ["Plugins.CW.Core.CustomerSubsidies.ProductGroup"] = "Product group",
            ["Plugins.CW.Core.CustomerSubsidies.UnitsLeft"] = "Units left",
            ["Plugins.CW.Core.CustomerSubsidies.UnitsUsed"] = "Used",
            ["Plugins.CW.Core.CustomerSubsidies.UnitsTotal"] = "Total",
            ["Plugins.CW.Core.CustomerSubsidies.CreditLeft"] = "Credit left",
            ["Plugins.CW.Core.CustomerSubsidies.CreditUsed"] = "Used",
            ["Plugins.CW.Core.CustomerSubsidies.CreditTotal"] = "Total",
            ["Plugins.CW.Core.CustomerSubsidies.History"] = "History",
            ["Plugins.CW.Core.CustomerSubsidies.History.Date"] = "Date",
            ["Plugins.CW.Core.CustomerSubsidies.History.What"] = "What happened",
            ["Plugins.CW.Core.CustomerSubsidies.History.Units"] = "Units",
            ["Plugins.CW.Core.CustomerSubsidies.History.Credit"] = "Credit",
            ["Plugins.CW.Core.CustomerSubsidies.History.Empty"] = "Nothing has happened on this subsidy yet.",
            ["Plugins.CW.Core.CustomerSubsidies.History.Latest"] = "Showing the latest {0} movements.",
            ["Plugins.CW.Core.CustomerSubsidies.History.Assigned"] = "Subsidy assigned",
            ["Plugins.CW.Core.CustomerSubsidies.History.Topup"] = "Top-up by {0}: {1}",
            ["Plugins.CW.Core.CustomerSubsidies.History.GroupAllocated"] = "{0} allocated",
            ["Plugins.CW.Core.CustomerSubsidies.History.GroupTopup"] = "{0} top-up by {1}: {2}",
            ["Plugins.CW.Core.CustomerSubsidies.History.Order"] = "Order #{0}",
            ["Plugins.CW.Core.CustomerSubsidies.History.OrderGroup"] = "Order #{0} \u2014 {1}",
            ["Plugins.CW.Core.CustomerSubsidies.History.OrderShipping"] = "Order #{0} \u2014 shipping",
            ["Plugins.CW.Core.CustomerSubsidies.History.Variation"] = "Credit or dates changed by {0}",
            ["Plugins.CW.Core.CustomerSubsidies.History.Refund"] = "Return on order #{0} \u2014 given back",
            ["Plugins.CW.Core.CustomerSubsidies.History.AssignmentRemoved"] = "(assignment since removed)",
            ["Plugins.CW.Core.CustomerSubsidies.History.EarlierSeasons"] = "Earlier seasons ({0})",
            ["Plugins.CW.Core.CustomerSubsidies.History.Season"] = "Season {0} \u2013 {1}",
            ["Plugins.CW.Core.CustomerSubsidies.History.Season.ToppedUp"] = "Topped up",
            ["Plugins.CW.Core.CustomerSubsidies.History.Season.Used"] = "Used",
            ["Plugins.CW.Core.CustomerSubsidies.History.Season.UnitsUsed"] = "Units used",
            ["Plugins.CW.Core.CustomerSubsidies.History.Season.NoActivity"] = "No top-ups or orders in this season.",

            // Bulk ordering
            ["Plugins.CW.Core.BulkOrder.HeaderLink"] = "Bulk ordering",

            ["Plugins.CW.Core.BulkOrder.PageTitle"] = "Bulk Ordering",

            ["Plugins.CW.Core.BulkOrder.Filter"] = "Show",
            ["Plugins.CW.Core.BulkOrder.Order"] = "From order",
            ["Plugins.CW.Core.BulkOrder.Filter.AllProducts"] = "All products",
            ["Plugins.CW.Core.BulkOrder.Filter.LatestOrdered"] = "Recently ordered",
            ["Plugins.CW.Core.BulkOrder.Filter.BestSellers"] = "Best sellers",
            ["Plugins.CW.Core.BulkOrder.Order.Choose"] = "Any recent order",
            ["Plugins.CW.Core.BulkOrder.AllCategories"] = "All categories",
            ["Plugins.CW.Core.BulkOrder.NoProducts"] = "No products match your selection",
            ["Plugins.CW.Core.BulkOrder.QuantityInCart"] = "{0} in cart",
            ["Plugins.CW.Core.BulkOrder.Previous"] = "Previous",
            ["Plugins.CW.Core.BulkOrder.Next"] = "Next",
            ["Plugins.CW.Core.BulkOrder.PageOf"] = "Page {0} of {1} ({2} products)",
            ["Plugins.CW.Core.BulkOrder.LoadFailed"] = "The list could not be loaded. Please try again.",
            ["Plugins.CW.Core.BulkOrder.PriceRange"] = "{0} – {1}",

            ["Plugins.CW.Core.BulkOrder.First"] = "First page",
            ["Plugins.CW.Core.BulkOrder.Last"] = "Last page",
            ["Plugins.CW.Core.BulkOrder.GoToPage"] = "Go to page {0}",
            ["Plugins.CW.Core.BulkOrder.PagerLabel"] = "Product list pages",

            ["Plugins.CW.Core.BulkOrder.Grid.Quantity"] = "Quantity",
            ["Plugins.CW.Core.BulkOrder.Grid.Unavailable"] = "Not available",
            ["Plugins.CW.Core.BulkOrder.Grid.NoCombinations"] = "No variants match the options selected above",
            ["Plugins.CW.Core.BulkOrder.Grid.Demoted"] = "Choose {0} above — too many variants to show at once",
            ["Plugins.CW.Core.BulkOrder.Grid.TooManyCells"] = "This product has more variants than the grid is configured to show",
            ["Plugins.CW.Core.BulkOrder.Grid.PinAny"] = "Any",
            ["Plugins.CW.Core.BulkOrder.Grid.PinNotSupportedYet"] = "Not available on this screen",
            ["Plugins.CW.Core.BulkOrder.Grid.NotOrderable"] = "This product is not currently available to order",
            ["Plugins.CW.Core.BulkOrder.Grid.AppliesTo"] = "Applies to {0} only",
            ["Plugins.CW.Core.BulkOrder.Grid.ChooseFirst"] = "Choose {0} first",
            ["Plugins.CW.Core.BulkOrder.Grid.NotACombination"] = "Not sold in this combination",

            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.GiftCard"] = "Gift cards need a recipient and a message, so they are ordered from the product page.",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.Rental"] = "Rentals need start and end dates, so they are ordered from the product page.",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.CustomerEntersPrice"] = "This product needs you to name your price, so it is ordered from the product page.",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.Recurring"] = "Subscriptions are ordered on their own, from the product page.",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.Link"] = "Open the product page",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.RequiredTextAttribute"] = "“{0}” needs details we cannot take on this screen, so this product is ordered from its product page.",
            ["Plugins.CW.Core.BulkOrder.Grid.RequiresText"] = "Product page only",
            ["Plugins.CW.Core.BulkOrder.Grid.RequiresText.Notice"] = "“{0}” cannot be entered on this screen, so the cells that need it are ordered from the product page.",
            ["Plugins.CW.Core.BulkOrder.Grid.AxisNone"] = "None",
            ["Plugins.CW.Core.BulkOrder.Grid.PinChoose"] = "Choose…",
            ["Plugins.CW.Core.BulkOrder.Grid.ChooseAbove"] = "Choose above",
            ["Plugins.CW.Core.BulkOrder.Grid.ChooseAbove.Notice"] = "Choose “{0}” above to enter quantities.",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.Grouped"] = "This is a group of products; each one is ordered from its own page.",
            ["Plugins.CW.Core.BulkOrder.Grid.Unsupported.BuyDisabled"] = "This product cannot be purchased online.",
            ["Plugins.CW.Core.BulkOrder.TypedLost"] = "{0} quantities you had typed were on products that are no longer shown. They were not added to the cart.",
            ["Plugins.CW.Core.BulkOrder.Grid.NoQuantity"] = "None",

            ["Plugins.CW.Core.BulkOrder.AddToCart.Button"] = "Add to cart",
            ["Plugins.CW.Core.BulkOrder.AddToCart.AddAll"] = "Add everything on this page",
            ["Plugins.CW.Core.BulkOrder.AddToCart.NotAvailable"] = "This product is no longer available.",
            ["Plugins.CW.Core.BulkOrder.AddToCart.Nothing"] = "Enter a quantity first.",
            ["Plugins.CW.Core.BulkOrder.AddToCart.Added"] = "{0} added to your cart.",
            ["Plugins.CW.Core.BulkOrder.AddToCart.SomeFailed"] = "{0} added to your cart. The rest are marked with the reason.",
            ["Plugins.CW.Core.BulkOrder.AddToCart.Failed"] = "Nothing could be added. Each line is marked with the reason.",
            ["Plugins.CW.Core.BulkOrder.AddToCart.ButtonFor"] = "Add to cart: {0}",
            ["Plugins.CW.Core.BulkOrder.AddToCart.Busy"] = "Still adding the previous lines. Try again in a moment.",
            ["Plugins.CW.Core.BulkOrder.AddToCart.TooManyLines"] = "Too many lines at once: add at most {0} at a time.",
            ["Plugins.CW.Core.BulkOrder.InvalidQuantity"] = "Enter a whole number of 0 or more.",
            ["Plugins.CW.Core.BulkOrder.Grid.Busy"] = "The options are still being updated. Try again in a moment.",
            ["Plugins.CW.Core.BulkOrder.Grid.Failed"] = "The options could not be updated, so they are back as they were.",
            ["Plugins.CW.Core.BulkOrder.Grid.QuantitiesLost"] = "{0} quantities you had typed have no box in the new options and were cleared.",
            ["Plugins.CW.Core.BulkOrder.PinsLost"] = "Options you chose on the previous page are not kept when you change page.",

            //sort options - resource names follow nop's enum convention so GetLocalizedEnumAsync finds them
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.Position"] = "Position",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.NameAsc"] = "Name: A to Z",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.NameDesc"] = "Name: Z to A",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.SkuAsc"] = "SKU: A to Z",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.SkuDesc"] = "SKU: Z to A",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.PriceAsc"] = "Price: low to high",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.PriceDesc"] = "Price: high to low",
            ["Enums.Nop.Plugin.CW.Core.Domain.BulkOrder.BulkOrderSorting.CreatedOn"] = "Newest first",

            //Activity log

            ["Plugins.CW.Core.ActivityLog.AddNewBranch"] = "Added a new branch (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.EditBranch"] = "Edited a branch (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteBranch"] = "Deleted a branch (ID = {0})",

            ["Plugins.CW.Core.ActivityLog.AddNewDepartment"] = "Added a new department (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.EditDepartment"] = "Edited a department (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteDepartment"] = "Deleted a department (ID = {0})",

            ["Plugins.CW.Core.ActivityLog.AddNewCostCenter"] = "Added a new cost centre (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.EditCostCenter"] = "Edited a cost centre (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteCostCenter"] = "Deleted a cost centre (ID = {0})",

            ["Plugins.CW.Core.ActivityLog.AddNewPackNameList"] = "Added a new packaging name list ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditPackNameList"] = "Edited an packaging name list ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeletePackNameList"] = "Deleted an packaging name list ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditPackNameListDefault"] = "Edited the default packaging name list for a branch ('{0}')",

            ["Plugins.CW.Core.ActivityLog.AddNewPackName"] = "Added a new packaging name ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditPackName"] = "Edited an packaging name ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeletePackName"] = "Deleted an packaging name ('{0}')",

            ["Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Customer"] = "PackName selection by store owner (Email: {0}, ID = {1})",
            ["Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Branch"] = "Started packaging name selection (Branch: {0}, ID = {1})",
            ["Plugins.CW.Core.ActivityLog.PackNameSelection.Finished.Customer"] = "PackName selection by store owner was finished (Email: {0}, ID = {1})",
            ["Plugins.CW.Core.ActivityLog.PackNameSelection.Finished.Branch"] = "Finished customer packaging name selection (Branch: {0}, ID = {1})",

            ["Plugins.CW.Core.ActivityLog.AddNewSubsidy"] = "Added a new subsidy ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditSubsidy"] = "Edited an subsidy ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeleteSubsidy"] = "Deleted an subsidy ('{0}')",

            ["Plugins.CW.Core.ActivityLog.AddNewProductGroup"] = "Added a new product group ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditProductGroup"] = "Edited a product group ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeleteProductGroup"] = "Deleted a product group ('{0}')",
            ["Plugins.CW.Core.ActivityLog.AddNewSubsidyProductGroup"] = "Added a new subsidy product group mapping ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditSubsidyProductGroup"] = "Edited an subsidy product group mapping ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeleteSubsidyProductGroup"] = "Deleted an subsidy product group mapping ('{0}')",

            //{0} is the SubsidyPackName mapping id from BOTH callers, and {1} the amount submitted -
            //not the balance after the add. Before this, admin logged the topup row id and the
            //storefront logged the mapping id under this one key.
            ["Plugins.CW.Core.ActivityLog.EditSubsidyTopup"] = "Topped up subsidy for assignment {0} by {1}",
            //Four placeholders: an earlier version passed four arguments to a one-placeholder string, so three
            //were discarded and the log said nothing useful. string.Format ignores extra arguments, so
            //it never threw - which is why it went unnoticed.
            ["Plugins.CW.Core.ActivityLog.EditProductGroupTopup"] = "Topped up product group {1} for assignment {0} by {2} units and {3} credit",

            ["Plugins.CW.Core.ActivityLog.AddSubsidyVariation"] = "Added an subsidy variation ('{0}')",
            ["Plugins.CW.Core.ActivityLog.EditSubsidyVariation"] = "Edited an subsidy variation ('{0}')",

            ["Plugins.CW.Core.ActivityLog.AddPackNameListManager"] = "Added {1} manager(s) to packName list {0}",
            ["Plugins.CW.Core.ActivityLog.DeletePackNameListManager"] = "Removed manager mapping {0} from packName list {1}",
            ["Plugins.CW.Core.ActivityLog.AddDepartmentManager"] = "Added {1} manager(s) to department {0}",
            ["Plugins.CW.Core.ActivityLog.DeleteDepartmentManager"] = "Removed manager mapping {0} from department {1}",
            ["Plugins.CW.Core.ActivityLog.EditStoreSettings"] = "Edited the CW settings of store {0}",
            ["Plugins.CW.Core.ActivityLog.EditDefaultStoreSettings"] = "Edited the CW default settings for all stores",
            ["Plugins.CW.Core.ActivityLog.GeneratePackNames"] = "Generated packNames in the background for store {0}",
            ["Plugins.CW.Core.ActivityLog.GeneratePackNameLists"] = "Generated packName lists in the background for store {0}",
            ["Plugins.CW.Core.ActivityLog.ImportPackNames"] = "Imported {0} packname(s) from Excel",
            ["Plugins.CW.Core.ActivityLog.AddSubsidyPackName"] = "Assigned {1} packName(s) to subsidy {0}",
            ["Plugins.CW.Core.ActivityLog.DeleteSubsidyPackName"] = "Removed packName {1} from subsidy {0}",
            ["Plugins.CW.Core.ActivityLog.AddProductGroupProduct"] = "Added {1} product(s) to product group {0}",
            ["Plugins.CW.Core.ActivityLog.DeleteProductGroupProduct"] = "Removed product {1} from product group {0}",

            // Common
            ["Admin.Common.Actions"] = "Actions",
            ["Admin.Common.Create"] = "Create",
        });
    }

    #endregion

    #region Methods

    public override async Task InstallAsync()
    {
        await AddOrUpdateLocaleResources();

        await InstallOrUpdateAsync();

        await base.InstallAsync();

        await _cwDatabaseService.UpdateDatabaseScriptsAsync();
    }

    /// <summary>
    /// Update plugin
    /// </summary>
    /// <param name="currentVersion">Current version of plugin</param>
    /// <param name="targetVersion">New version of plugin</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
        {
            await AddOrUpdateLocaleResources();
        }

        await InstallOrUpdateAsync();

        await base.UpdateAsync(currentVersion, targetVersion);

        await _cwDatabaseService.UpdateDatabaseScriptsAsync();
    }

    public override async Task UninstallAsync()
    {
        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.Core");

        //settings
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(CWCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(CWCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await base.UninstallAsync();
    }

    #endregion

    #region Widget

    /// <summary>
    /// Gets widget zones where this widget should be rendered
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the widget zones
    /// </returns>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(_widgetComponents.Keys.ToList());
    }

    /// <summary>
    /// Gets a type of a view component for displaying widget
    /// </summary>
    /// <param name="widgetZone">Name of the widget zone</param>
    /// <returns>View component type</returns>
    public Type GetWidgetViewComponent(string widgetZone)
    {
        return _widgetComponents.GetValueOrDefault(widgetZone);
    }

    /// <summary>
    /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
    /// </summary>
    public bool HideInWidgetList => true;

    #endregion
}