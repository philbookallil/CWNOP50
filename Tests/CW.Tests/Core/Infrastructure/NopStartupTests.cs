using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Infrastructure;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Events;
using NUnit.Framework;

namespace CW.Tests.Core.Infrastructure;

/// <summary>
/// The plugin's registrations: every CW type a registered service, an event consumer, a controller or a view component
/// asks for is registered too, so nothing fails when nop resolves it. nop's own services are not checked here; the site
/// start-up does that.
/// </summary>
[TestFixture]
public class NopStartupTests
{
    private static readonly Type[] PluginTypes = typeof(NopStartup).Assembly.GetTypes();

    private static IServiceCollection Registrations()
    {
        var services = new ServiceCollection();
        new NopStartup().ConfigureServices(services, null!);

        return services;
    }

    /// <summary>
    /// The CW types the widest public constructor of a class asks for (Lazy and IEnumerable unwrapped)
    /// </summary>
    private static IEnumerable<Type> PluginDependencies(Type type)
    {
        var constructor = type.GetConstructors().OrderByDescending(ctor => ctor.GetParameters().Length).FirstOrDefault();

        foreach (var parameter in constructor?.GetParameters() ?? [])
        {
            var dependency = parameter.ParameterType;
            if (dependency.IsGenericType && (dependency.GetGenericTypeDefinition() == typeof(Lazy<>) || dependency.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                dependency = dependency.GetGenericArguments()[0];

            if (dependency.Assembly == typeof(NopStartup).Assembly)
                yield return dependency;
        }
    }

    private static IEnumerable<string> MissingDependencies(IEnumerable<Type> resolvedTypes)
    {
        var registered = Registrations().Select(descriptor => descriptor.ServiceType).ToHashSet();

        return resolvedTypes
            .SelectMany(type => PluginDependencies(type).Where(dependency => !registered.Contains(dependency))
                .Select(dependency => $"{type.Name} needs {dependency.Name}"))
            .ToList();
    }

    [Test]
    public void Every_registered_service_has_its_plugin_dependencies_registered()
    {
        var implementations = Registrations()
            .Where(descriptor => descriptor.ImplementationType != null)
            .Select(descriptor => descriptor.ImplementationType!);

        MissingDependencies(implementations).Should().BeEmpty();
    }

    [Test]
    public void Every_event_consumer_has_its_plugin_dependencies_registered()
    {
        var consumers = PluginTypes.Where(type => type is { IsClass: true, IsAbstract: false }
            && type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IConsumer<>)));

        consumers.Should().NotBeEmpty();
        MissingDependencies(consumers).Should().BeEmpty();
    }

    [Test]
    public void Every_controller_and_view_component_has_its_plugin_dependencies_registered()
    {
        var mvcTypes = PluginTypes.Where(type => type is { IsClass: true, IsAbstract: false }
            && (typeof(ControllerBase).IsAssignableFrom(type) || typeof(ViewComponent).IsAssignableFrom(type)));

        mvcTypes.Should().NotBeEmpty();
        MissingDependencies(mvcTypes).Should().BeEmpty();
    }

    [Test]
    public void Every_global_MVC_filter_is_registered_with_its_plugin_dependencies()
    {
        var services = Registrations();
        services.AddOptions();
        using var provider = services.BuildServiceProvider();
        var filterTypes = provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters
            .OfType<TypeFilterAttribute>()
            .Select(filter => filter.ImplementationType)
            .Where(type => type.Assembly == typeof(NopStartup).Assembly)
            .ToList();

        filterTypes.Should().Contain([typeof(ReturnRequestAdminFilter), typeof(PackNameCheckoutRedirectFilter), typeof(AddShipmentDestinationFilter), typeof(PackNameOrderAdminGuardFilter)]);
        MissingDependencies(filterTypes).Should().BeEmpty();
    }

    [Test]
    public void The_cart_override_sits_on_the_list_overload_every_caller_reaches()
    {
        var listOverload = typeof(ShoppingCartOverrideService).GetMethod(nameof(ShoppingCartOverrideService.GetShoppingCartAsync),
            [typeof(Customer), typeof(List<int>), typeof(int), typeof(int?), typeof(DateTime?), typeof(DateTime?), typeof(int?)]);
        var singleTypeOverload = typeof(ShoppingCartOverrideService).GetMethod(nameof(ShoppingCartOverrideService.GetShoppingCartAsync),
            [typeof(Customer), typeof(ShoppingCartType), typeof(int), typeof(int?), typeof(DateTime?), typeof(DateTime?), typeof(int?)]);

        listOverload!.DeclaringType.Should().Be<ShoppingCartOverrideService>();
        singleTypeOverload!.DeclaringType.Should().NotBe<ShoppingCartOverrideService>("nop's single-type overload forwards to the list one");
    }

    [TestCase("AclOverrideService", "IAclService", "IAclOverrideService")]
    [TestCase("CustomerOverrideService", "ICustomerService", "ICustomerOverrideService")]
    [TestCase("ShoppingCartOverrideService", "IShoppingCartService", "IShoppingCartOverrideService")]
    [TestCase("OrderTotalCalculationOverrideService", "IOrderTotalCalculationService", "IOrderTotalCalculationOverrideService")]
    [TestCase("OrderProcessingOverrideService", "IOrderProcessingService", "IOrderProcessingOverrideService")]
    [TestCase("MessageTokenProviderOverride", "IMessageTokenProvider", "IMessageTokenProviderOverride")]
    [TestCase("ProductOverrideService", "IProductService", null)]
    [TestCase("ShipmentOverrideService", "IShipmentService", null)]
    [TestCase("PdfOverrideService", "IPdfService", null)]
    [TestCase("OrderReportOverrideService", "IOrderReportService", null)]
    [TestCase("PluginOverrideService", "IPluginService", null)]
    [TestCase("CustomerRegistrationOverrideService", "ICustomerRegistrationService", null)]
    [TestCase("CustomerOverrideModelFactory", "ICustomerModelFactory", "ICustomerOverrideModelFactory")]
    [TestCase("ReturnRequestOverrideModelFactory", "IReturnRequestModelFactory", "IReturnRequestOverrideModelFactory")]
    [TestCase("ShoppingCartOverrideModelFactory", "IShoppingCartModelFactory", "IShoppingCartOverrideModelFactory")]
    [TestCase("OrderOverrideModelFactory", "IOrderModelFactory", null)]
    public void Every_override_replaces_nops_service_and_keeps_its_CW_interface(string implementation, string nopInterface, string? cwInterface)
    {
        var interfaces = Registrations()
            .Where(descriptor => descriptor.ImplementationType?.Name == implementation)
            .Select(descriptor => descriptor.ServiceType)
            .ToList();

        interfaces.Should().Contain(type => type.Name == nopInterface
            && (type.Namespace!.StartsWith("Nop.Services") || type.Namespace.StartsWith("Nop.Web")));
        if (cwInterface != null)
            interfaces.Should().Contain(type => type.Name == cwInterface && type.Assembly == typeof(NopStartup).Assembly);
    }

    [Test]
    public void The_cart_model_factory_replaces_the_storefronts_not_the_admins()
    {
        //nop has an admin IShoppingCartModelFactory of the same name; CW's cart factory takes the storefront's
        Registrations().Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Web.Factories.IShoppingCartModelFactory)
            && descriptor.ImplementationType!.Name == "ShoppingCartOverrideModelFactory");
    }

    [Test]
    public void The_order_model_factory_replaces_the_storefronts_not_the_admins()
    {
        //nop has an admin IOrderModelFactory of the same name; CW's order details factory takes the storefront's
        Registrations().Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Web.Factories.IOrderModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.OrderOverrideModelFactory));
    }

    [Test]
    public void The_my_subsidies_factory_is_registered()
    {
        Registrations().Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Factories.ICustomerSubsidiesModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.CustomerSubsidiesModelFactory));
    }

    [Test]
    public void The_packname_checkout_factory_is_registered()
    {
        Registrations().Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Factories.IPackNameCheckoutModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.PackNameCheckoutModelFactory));
    }

    [Test]
    public void The_storefront_and_admin_packname_order_factories_are_each_registered_to_their_own()
    {
        //the two share a name in different namespaces; each interface gets the factory of its own side
        var registrations = Registrations();

        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Factories.IPackNameOrderModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.PackNameOrderModelFactory));
        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Areas.Admin.Factories.IPackNameOrderModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Areas.Admin.Factories.PackNameOrderModelFactory));
    }

    [Test]
    public void The_storefront_and_admin_return_request_factories_are_each_registered_to_their_own()
    {
        //nop's storefront and admin return request factories share a name, and so do CW's; each side gets its own
        var registrations = Registrations();

        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Web.Factories.IReturnRequestModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.ReturnRequestOverrideModelFactory));
        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Factories.IReturnRequestOverrideModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.ReturnRequestOverrideModelFactory));
        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Web.Areas.Admin.Factories.IReturnRequestModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Areas.Admin.Factories.ReturnRequestOverrideModelFactory));
    }

    [Test]
    public void The_returns_wizard_factories_are_registered()
    {
        var registrations = Registrations();

        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Factories.IExchangesModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.ExchangesModelFactory));
        registrations.Should().Contain(descriptor => descriptor.ServiceType == typeof(Nop.Plugin.CW.Core.Factories.IReturnRequestItemModelFactory)
            && descriptor.ImplementationType == typeof(Nop.Plugin.CW.Core.Factories.ReturnRequestItemModelFactory));
    }
}
