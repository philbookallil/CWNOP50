using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Background;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.Catalog;
using Nop.Plugin.CW.Core.Services.Common;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.DataBase;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Bulk;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Plugin.CW.Core.Services.Plugins;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Plugin.CW.Core.Services.Shipping;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Web.Areas.Admin.Factories;

namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// NopStartup for CW Core plugin
/// </summary>
/// <remarks>
/// An override of a nop service is registered twice: under nop's interface, which replaces nop's class, and under its
/// CW interface, which the CW services use.
/// </remarks>
public class NopStartup : INopStartup
{
    /// <summary>
    /// Add and configure the plugin's services
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="configuration">Configuration of the application</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ICustomerService, CustomerOverrideService>();
        services.AddScoped<ICustomerOverrideService, CustomerOverrideService>();

        services.AddScoped<IMultiStoreService, MultiStoreService>();
        services.AddScoped<IPackNameContext, PackNameContext>();
        services.AddScoped<IPackNameService, PackNameService>();
        services.AddScoped<IPackNameCatalogGate, PackNameCatalogGate>();
        services.AddScoped<ICWDatabaseService, CWDatabaseService>();
        services.AddScoped<IShoppingCartService, ShoppingCartOverrideService>();
        services.AddScoped<IShoppingCartOverrideService, ShoppingCartOverrideService>();
        services.AddScoped<IAclService, AclOverrideService>();
        services.AddScoped<IAclOverrideService, AclOverrideService>();

        services.AddScoped<ICWPdfService, CWPdfService>();
        services.AddScoped<IPdfService, PdfOverrideService>();
        services.AddScoped<ICreditNotesService, CreditNotesService>();
        services.AddScoped<IExchangeService, ExchangeService>();
        services.AddScoped<IExchangeWorkflowMessageService, ExchangeWorkflowMessageService>();
        services.AddScoped<IPackNameWorkflowMessageService, PackNameWorkflowMessageService>();
        services.AddScoped<IMessageTokenProvider, MessageTokenProviderOverride>();
        services.AddScoped<IMessageTokenProviderOverride, MessageTokenProviderOverride>();
        services.AddScoped<IOrderTotalCalculationService, OrderTotalCalculationOverrideService>();
        services.AddScoped<IOrderTotalCalculationOverrideService, OrderTotalCalculationOverrideService>();
        services.AddScoped<IOrderProcessingService, OrderProcessingOverrideService>();
        services.AddScoped<IOrderProcessingOverrideService, OrderProcessingOverrideService>();
        services.AddScoped<ICWReturnRequestService, CWReturnRequestService>();

        services.AddScoped<ISubsidyService, SubsidyService>();
        services.AddScoped<ISubsidyOrderingService, SubsidyOrderingService>();
        services.AddScoped<ISubsidyCreditService, SubsidyCreditService>();
        services.AddScoped<ISubsidyReportingService, SubsidyReportingService>();

        services.AddScoped<ICWOrderService, CWOrderService>();
        services.AddScoped<ICWAddressService, CWAddressService>();

        services.AddScoped<IPackNameShippingAssignmentService, PackNameShippingAssignmentService>();

        services.AddScoped<ISubsidyCatalogCartService, SubsidyCatalogCartService>();

        services.AddScoped<IOrderReportService, OrderReportOverrideService>();

        services.AddScoped<IProductService, ProductOverrideService>();

        services.AddScoped<IPluginService, PluginOverrideService>();

        services.AddScoped<ICWFeatureService, CWFeatureService>();
        services.AddScoped<ICWFeatureBlockResultBuilder, CWFeatureBlockResultBuilder>();
        services.AddScoped<ICWFeatureGuard, CWFeatureGuard>();

        services.AddScoped<IReturnCaseService, ReturnCaseService>();
        services.AddScoped<IExchangeMessageService, ExchangeMessageService>();
        services.AddScoped<IExchangesDisplayService, ExchangesDisplayService>();

        services.AddScoped<IShipmentService, ShipmentOverrideService>();

        services.AddScoped<IBulkOrderService, BulkOrderService>();

        // Settlement pipeline. The sources are registered as their own concrete types and composed
        // explicitly here; the pipeline still orders them by Priority at walk time.
        //
        // They are deliberately NOT registered as ISettlementSource. Nop's admin warning
        // (CommonModelFactory.PreparePluginsOverrideSameInterfaceWarningModelAsync) flags any Nop.*
        // service type carrying more than one registration, and cannot tell a multi-implementation
        // strategy apart from two plugins fighting over one interface — it reported CW.Core as
        // overriding ISettlementSource against itself. Adding a source means a registration below
        // plus a line in the composition.
        services.AddScoped<ISubsidyAllocationService, SubsidyAllocationService>();
        services.AddScoped<CreditNoteSettlementSource>();
        services.AddScoped<SubsidySettlementSource>();
        services.AddScoped<ISettlementPipeline>(sp => new SettlementPipeline(
        [
            sp.GetRequiredService<CreditNoteSettlementSource>(),
            sp.GetRequiredService<SubsidySettlementSource>()
        ]));

        services.AddScoped<IBulkOrderGridBuilder, BulkOrderGridBuilder>();
        services.AddScoped<IBulkOrderAvailabilityProvider, BulkOrderAvailabilityProvider>();
        services.AddScoped<IBulkOrderPricingService, BulkOrderPricingService>();
        services.AddScoped<IBulkOrderProductPageService, BulkOrderProductPageService>();
        services.AddScoped<IBulkOrderEntryService, BulkOrderEntryService>();
        services.AddScoped<IPackNameLandingService, PackNameLandingService>();
        services.AddScoped<IPackNameProvisioningService, PackNameProvisioningService>();
        services.AddScoped<IPackNameShippingMethodService, PackNameShippingMethodService>();
        services.AddScoped<IPackNameRegistrationService, PackNameRegistrationService>();
        services.AddScoped<IPackNameImportService, PackNameImportService>();
        services.AddScoped<IPackNameBulkService, PackNameBulkService>();

        services.AddScoped<ICustomerRegistrationService, CustomerRegistrationOverrideService>();

        //scoped == per request, which is the whole point: the consumer resolves it, the head decides on the
        //stylesheet from it, and the widget zone renders from it, all in one request and without rebuilding
        services.AddScoped<BulkOrderProductPageContext>();

        //admin factories
        services.AddScoped<ICustomerModelFactory, CustomerOverrideModelFactory>();
        services.AddScoped<ICustomerOverrideModelFactory, CustomerOverrideModelFactory>();

        services.AddScoped<IBranchModelFactory, BranchModelFactory>();
        services.AddScoped<IMultiStoreModelFactory, MultiStoreModelFactory>();
        services.AddScoped<IPackNameModelFactory, PackNameModelFactory>();
        services.AddScoped<ICWSettingModelFactory, CWSettingModelFactory>();
        services.AddScoped<ICWAddressModelFactory, CWAddressModelFactory>();

        services.AddScoped<ICreditNotesModelFactory, CreditNotesModelFactory>();
        services.AddScoped<IReturnRequestModelFactory, ReturnRequestOverrideModelFactory>();
        services.AddScoped<IReturnRequestOverrideModelFactory, ReturnRequestOverrideModelFactory>();
        services.AddScoped<IExchangeSettingsModelFactory, ExchangeSettingsModelFactory>();

        services.AddScoped<ISubsidyModelFactory, SubsidyModelFactory>();

        services.AddScoped<IPackNameOrderModelFactory, PackNameOrderModelFactory>();
        services.AddScoped<IReturnRequestExchangeModelFactory, ReturnRequestExchangeModelFactory>();

        //public store factories
        services.AddScoped<Factories.IPackNameModelFactory, Factories.PackNameModelFactory>();
        services.AddScoped<Factories.IShoppingCartOverrideModelFactory, Factories.ShoppingCartOverrideModelFactory>();
        services.AddScoped<Web.Factories.IShoppingCartModelFactory, Factories.ShoppingCartOverrideModelFactory>();
        services.AddScoped<Factories.IPackNameCheckoutModelFactory, Factories.PackNameCheckoutModelFactory>();
        services.AddScoped<Factories.IPackNameOrderModelFactory, Factories.PackNameOrderModelFactory>();
        services.AddScoped<Web.Factories.IOrderModelFactory, Factories.OrderOverrideModelFactory>();
        services.AddScoped<Factories.ISubsidyCatalogModelFactory, Factories.SubsidyCatalogModelFactory>();
        services.AddScoped<Factories.ICustomerSubsidiesModelFactory, Factories.CustomerSubsidiesModelFactory>();
        services.AddScoped<Factories.IExchangesModelFactory, Factories.ExchangesModelFactory>();
        services.AddScoped<Factories.IReturnRequestItemModelFactory, Factories.ReturnRequestItemModelFactory>();
        services.AddScoped<Web.Factories.IReturnRequestModelFactory, Factories.ReturnRequestOverrideModelFactory>();
        services.AddScoped<Factories.IReturnRequestOverrideModelFactory, Factories.ReturnRequestOverrideModelFactory>();
        services.AddScoped<Factories.IBulkOrderModelFactory, Factories.BulkOrderModelFactory>();

        // - Business Logic Layer classes (global - not specific to any plugin)
        services.AddScoped<ICustomersLogic, CustomersLogic>();
        services.AddScoped<IExchangesLogic, ExchangesLogic>();
        services.AddScoped<IReturnSubmissionLogic, ReturnSubmissionLogic>();
        services.AddScoped<IReturnResolutionLogic, ReturnResolutionLogic>();
        services.AddScoped<IBulkOrderLogic, BulkOrderLogic>();

        // Providers
        services.AddScoped<IPackNamePanelProvider, SubsidyPackNamePanelProvider>();

        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedHostedService>();

        services.AddScoped<ViewPathsRegistrar>();
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationExpanders.Add(new ViewLocationExpander());
        });

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<ReturnRequestAdminFilter>(1);
            options.Filters.Add<PackNameCheckoutRedirectFilter>(1);
            options.Filters.Add<AddShipmentDestinationFilter>(1);
            options.Filters.Add<PackNameOrderAdminGuardFilter>(1);
            options.Filters.Add<ReturnRequestSubmitFilter>(1);
        });
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>
    /// Gets the order of this startup configuration: after nop's own registrations, so the plugin's replace them
    /// </summary>
    public int Order { get { return DependencyRegistrarConstants.OrderAfterBaseRegistrar; } }
}