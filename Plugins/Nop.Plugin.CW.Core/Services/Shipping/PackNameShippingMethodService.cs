using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Services.Shipping;

namespace Nop.Plugin.CW.Core.Services.Shipping;

/// <summary>
/// Asks the freight plugin for its shipping options with a request that carries one empty item and no address: the
/// plugin refuses a request without items and rates nothing from the item itself, so the answer is every method it has
/// for the store
/// </summary>
public class PackNameShippingMethodService : IPackNameShippingMethodService
{
    #region Fields

    protected readonly IShippingPluginManager _shippingPluginManager;

    #endregion

    #region Ctor

    public PackNameShippingMethodService(IShippingPluginManager shippingPluginManager)
    {
        _shippingPluginManager = shippingPluginManager;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the names of the shipping methods the freight plugin offers in a store, in the plugin's own order
    /// </summary>
    /// <param name="storeId">Store identifier; 0 for the current store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the method names; empty when the freight plugin is not active for the store
    /// </returns>
    public virtual async Task<IList<string>> GetShippingMethodNamesAsync(int storeId)
    {
        var freightPlugins = await _shippingPluginManager.LoadActivePluginsAsync(storeId: storeId, systemName: CWCoreDefaults.FreightPluginSystemName);
        var freightPlugin = freightPlugins.FirstOrDefault();

        if (freightPlugin == null)
            return new List<string>();

        var request = new GetShippingOptionRequest { StoreId = storeId };
        request.Items.Add(new GetShippingOptionRequest.PackageItem(new ShoppingCartItem(), new Product(), 1));

        var response = await freightPlugin.GetShippingOptionsAsync(request);

        if (!response.Success)
            return new List<string>();

        return response.ShippingOptions.Select(option => option.Name).Distinct().ToList();
    }

    /// <summary>
    /// Keeps, of the shipping method names given, those the freight plugin offers in a store, in the order given, and
    /// joins them the way the store and branch settings hold them
    /// </summary>
    /// <param name="shippingMethodNames">The names chosen; may be null</param>
    /// <param name="storeId">Store identifier; 0 for the current store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the comma-separated names; null when none of them is offered
    /// </returns>
    public virtual async Task<string> KeepOfferedShippingMethodsAsync(IEnumerable<string> shippingMethodNames, int storeId)
    {
        if (shippingMethodNames == null)
            return null;

        //the checkout matches these names exactly against the plugin's options, so a name typed into the picker
        //that the plugin does not offer would leave the packname with nothing to choose
        var offeredNames = await GetShippingMethodNamesAsync(storeId);
        var keptNames = shippingMethodNames.Where(offeredNames.Contains).Distinct().ToList();

        return keptNames.Count == 0 ? null : string.Join(", ", keptNames);
    }

    #endregion
}