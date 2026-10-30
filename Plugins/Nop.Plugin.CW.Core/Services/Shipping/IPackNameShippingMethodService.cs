namespace Nop.Plugin.CW.Core.Services.Shipping;

/// <summary>
/// The shipping methods the packname checkout can offer, under the names the freight plugin gives them
/// </summary>
public interface IPackNameShippingMethodService
{
    /// <summary>
    /// Gets the names of the shipping methods the freight plugin offers in a store, in the plugin's own order
    /// </summary>
    /// <param name="storeId">Store identifier; 0 for the current store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the method names; empty when the freight plugin is not active for the store
    /// </returns>
    Task<IList<string>> GetShippingMethodNamesAsync(int storeId);

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
    Task<string> KeepOfferedShippingMethodsAsync(IEnumerable<string> shippingMethodNames, int storeId);
}