using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Logging;
using Nop.Services.Orders;
using System.Collections.Concurrent;
using System.Reflection;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Represents a filter that blocks the action when a CW feature is disabled for the resolved store
/// </summary>
public class CheckCWFeatureFilter : IAsyncActionFilter
{
    #region Nested

    /// <summary>
    /// Resolves the store an entity-scoped source points at
    /// </summary>
    /// <param name="DefaultParameterName">Parameter name used when the attribute does not set one</param>
    /// <param name="GetStoreIdAsync">Resolves the entity's store id, or null when the entity does not exist</param>
    protected sealed record EntityStoreSource(string DefaultParameterName, Func<IServiceProvider, int, Task<int?>> GetStoreIdAsync);

    #endregion

    #region Constants

    private const string DEFAULT_STORE_PARAMETER = "storeId";
    private const string DEFAULT_ORDER_PARAMETER = "orderId";

    #endregion

    #region Private Fields

    /// <summary>
    /// Caches model-property lookups so the reflection cost is paid once per (type, name)
    /// for the lifetime of the process, not once per request.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> _propertyCache = new();

    /// <summary>
    /// Every entity-scoped store source. Adding one is a single entry here plus a member on
    /// <see cref="CWFeatureStoreSource"/> - no new constructor dependency and no new switch case.
    /// Services resolve lazily, so an action only pays for the one service its own source needs.
    /// A source with no entry here has nowhere to fall through to, so it fails closed.
    /// </summary>
    private static readonly IReadOnlyDictionary<CWFeatureStoreSource, EntityStoreSource> _entitySources =
        new Dictionary<CWFeatureStoreSource, EntityStoreSource>
        {
            [CWFeatureStoreSource.Branch] = new("branchId", async (sp, id) =>
                (await sp.GetRequiredService<IBranchService>().GetBranchByIdAsync(id))?.StoreId),

            [CWFeatureStoreSource.PackNameList] = new("listId", async (sp, id) =>
                (await sp.GetRequiredService<IPackNameService>().GetPackNameListByIdAsync(id))?.StoreId),

            [CWFeatureStoreSource.PackName] = new("packNameId", async (sp, id) =>
            {
                var packNameService = sp.GetRequiredService<IPackNameService>();
                var packName = await packNameService.GetPackNameByIdAsync(id);

                return packName is null
                    ? null
                    : (await packNameService.GetPackNameListByIdAsync(packName.PackNameListId))?.StoreId;
            }),

            [CWFeatureStoreSource.Department] = new("departmentId", async (sp, id) =>
                (await sp.GetRequiredService<IMultiStoreService>().GetDepartmentByIdAsync(id))?.StoreId),

            [CWFeatureStoreSource.Subsidy] = new("subsidyId", async (sp, id) =>
                (await sp.GetRequiredService<ISubsidyService>().GetSubsidyByIdAsync(id))?.StoreId),

            [CWFeatureStoreSource.ProductGroup] = new("productGroupId", async (sp, id) =>
                (await sp.GetRequiredService<ISubsidyService>().GetProductGroupByIdAsync(id))?.StoreId),

            [CWFeatureStoreSource.SubsidyPackName] = new("subsidyPackNameId", async (sp, id) =>
            {
                var subsidyService = sp.GetRequiredService<ISubsidyService>();
                var subsidyPackName = await subsidyService.GetSubsidyPackNameByIdAsync(id);

                return subsidyPackName is null
                    ? null
                    : (await subsidyService.GetSubsidyByIdAsync(subsidyPackName.SubsidyId))?.StoreId;
            })
        };

    #endregion

    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IServiceProvider _serviceProvider;

    protected readonly CWFeature[] _features;
    protected readonly CWFeatureStoreSource _storeSource;
    protected readonly string _parameterName;
    protected readonly string _redirectRouteName;
    protected readonly bool _allowDefaultStore;

    #endregion

    #region Ctor

    public CheckCWFeatureFilter(CWFeature[] features,
        CWFeatureStoreSource storeSource,
        string parameterName,
        string redirectRouteName,
        bool allowDefaultStore,
        ICWFeatureService cwFeatureService,
        IServiceProvider serviceProvider)
    {
        _features = features;
        _storeSource = storeSource;
        _parameterName = parameterName;
        _redirectRouteName = redirectRouteName;
        _allowDefaultStore = allowDefaultStore;

        _cwFeatureService = cwFeatureService;
        _serviceProvider = serviceProvider;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Reads an integer from the action arguments, a bound model property, the route values,
    /// the query string or the form body
    /// </summary>
    /// <param name="context">Action executing context</param>
    /// <param name="name">Parameter, property, route, query-string or form key</param>
    /// <returns>The value, or null when it cannot be resolved</returns>
    protected virtual int? ResolveInt(ActionExecutingContext context, string name)
    {
        //1. a direct action parameter, e.g. Subsidies(int storeId)
        if (context.ActionArguments.TryGetValue(name, out var direct) && TryToInt(direct, out var directValue))
            return directValue;

        //2. a property on a bound complex model, e.g. SubsidySearchModel.StoreId.
        //   Arguments are visited in parameter order, so if two models both expose the
        //   property the first parameter wins.
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var type = argument.GetType();

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                continue;

            var property = _propertyCache.GetOrAdd((type, name), key => key.Item1.GetProperty(key.Item2,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));

            if (property is not null && TryToInt(property.GetValue(argument), out var propertyValue))
                return propertyValue;
        }

        //3. a route value, e.g. admin/multistore/edit/{id}
        if (context.RouteData.Values.TryGetValue(name, out var routeValue) && TryToInt(routeValue, out var routeIntValue))
            return routeIntValue;

        //4. the query string, for actions that read it without declaring a parameter
        if (context.HttpContext.Request.Query.TryGetValue(name, out var queryValue)
            && TryToInt(queryValue.ToString(), out var queryIntValue))
            return queryIntValue;

        //5. the form body, for POSTs where the id is a form field that is not on the bound model
        //   (e.g. AssociateCustomerToPackNamePopup posts BranchId but binds a model without it).
        //   Reading the form throws when the request exceeds the form or multipart limits, and a
        //   gate must not turn that into a 500 - treat it as unresolved and fail closed instead.
        try
        {
            if (context.HttpContext.Request.HasFormContentType
                && context.HttpContext.Request.Form.TryGetValue(name, out var formValue)
                && TryToInt(formValue.ToString(), out var formIntValue))
                return formIntValue;
        }
        catch (Exception)
        {
            //unreadable form: fall through to null
        }

        return null;
    }

    protected static bool TryToInt(object value, out int result)
    {
        result = 0;

        switch (value)
        {
            case null:
                return false;
            case int intValue:
                result = intValue;
                return true;
            default:
                return int.TryParse(value.ToString(), out result);
        }
    }

    /// <summary>
    /// Gets the parameter name configured on the attribute, or the source's default
    /// </summary>
    protected virtual string ParameterNameOrDefault(string defaultName)
        => string.IsNullOrEmpty(_parameterName) ? defaultName : _parameterName;

    protected virtual async Task<bool> LogUnresolvedStoreAsync(ActionExecutingContext context, string parameterName)
    {
        //Fail closed. An action that cannot say which store it is acting on is a bug,
        //and guessing would let a disabled feature through.
        await _serviceProvider.GetRequiredService<ILogger>().WarningAsync(
            $"{nameof(CheckCWFeatureFilter)}: could not resolve store id from '{parameterName}' for " +
            $"{context.ActionDescriptor.DisplayName}. Blocking the request.");

        return false;
    }

    /// <summary>
    /// Resolves the store the action is acting on
    /// </summary>
    /// <param name="context">Action executing context</param>
    /// <returns>
    /// The parameter name that was consulted, for logging, and the store identifier
    /// or null when it cannot be resolved
    /// </returns>
    protected virtual async Task<(string ParameterName, int? StoreId)> ResolveStoreIdAsync(ActionExecutingContext context)
    {
        switch (_storeSource)
        {
            case CWFeatureStoreSource.Parameter:
                {
                    var name = ParameterNameOrDefault(DEFAULT_STORE_PARAMETER);

                    return (name, ResolveInt(context, name));
                }
            case CWFeatureStoreSource.Order:
                {
                    var name = ParameterNameOrDefault(DEFAULT_ORDER_PARAMETER);
                    var orderId = ResolveInt(context, name);
                    var order = orderId is null or 0
                        ? null
                        : await _serviceProvider.GetRequiredService<IOrderService>().GetOrderByIdAsync(orderId.Value);

                    return (name, order?.StoreId);
                }
            default:
                {
                    //an unregistered source resolves nothing and therefore fails closed
                    if (!_entitySources.TryGetValue(_storeSource, out var source))
                        return (_storeSource.ToString(), null);

                    var name = ParameterNameOrDefault(source.DefaultParameterName);
                    var entityId = ResolveInt(context, name);

                    return entityId is null or 0
                        ? (name, null)
                        : (name, await source.GetStoreIdAsync(_serviceProvider, entityId.Value));
                }
        }
    }

    /// <summary>
    /// Gets a value indicating whether at least one of the configured features is enabled for the store
    /// </summary>
    protected virtual async Task<bool> HasAnyFeatureAsync(int storeId)
    {
        var features = await _cwFeatureService.GetFeaturesAsync(storeId);

        foreach (var feature in _features)
            if (features[feature])
                return true;

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether at least one of the configured features is enabled
    /// </summary>
    /// <param name="context">Action executing context</param>
    /// <returns>
    /// A task whose result is true when the action may proceed.
    /// Unresolvable store identifiers fail closed (false).
    /// </returns>
    protected virtual async Task<bool> IsAllowedAsync(ActionExecutingContext context)
    {
        //An attribute with no StoreSource is a coding mistake, not a data problem: log it as an error
        //so it is distinguishable from a legitimately unresolvable request.
        if (_storeSource == CWFeatureStoreSource.NotSet)
        {
            await _serviceProvider.GetRequiredService<ILogger>().ErrorAsync(
                $"{nameof(CheckCWFeatureFilter)}: [CheckCWFeature] on {context.ActionDescriptor.DisplayName} " +
                $"does not set {nameof(CheckCWFeatureAttribute.StoreSource)}. Blocking the request.");

            return false;
        }

        //no single store to resolve
        if (_storeSource == CWFeatureStoreSource.AnyStore)
        {
            var anywhere = await _cwFeatureService.GetFeaturesForAnyStoreAsync();

            foreach (var feature in _features)
                if (anywhere[feature])
                    return true;

            return false;
        }

        if (_storeSource == CWFeatureStoreSource.CurrentStore)
        {
            var currentStore = await _serviceProvider.GetRequiredService<IStoreContext>().GetCurrentStoreAsync();

            return await HasAnyFeatureAsync(currentStore.Id);
        }

        var (parameterName, storeId) = await ResolveStoreIdAsync(context);

        //Store id 0 is a real scope in nop (the all-stores defaults), so it is only accepted
        //when the action opts in via AllowDefaultStore. This applies to every resolved source,
        //not just Parameter.
        if (storeId is null || (storeId == 0 && !_allowDefaultStore))
            return await LogUnresolvedStoreAsync(context, parameterName);

        return await HasAnyFeatureAsync(storeId.Value);
    }

    #endregion

    #region Methods

    public virtual async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        //nothing to check before the database exists (installation)
        if (!DataSettingsManager.IsDatabaseInstalled())
        {
            await next();
            return;
        }

        //another filter has already short-circuited the pipeline (permissions, antiforgery, …)
        if (context.Result is not null)
            return;

        if (await IsAllowedAsync(context))
        {
            await next();
            return;
        }

        context.Result = await _serviceProvider.GetRequiredService<ICWFeatureBlockResultBuilder>()
            .CreateAsync(context.HttpContext.Request, _redirectRouteName);
    }

    #endregion
}