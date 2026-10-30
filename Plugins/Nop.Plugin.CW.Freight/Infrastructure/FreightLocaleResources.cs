namespace Nop.Plugin.CW.Freight.Infrastructure;

/// <summary>
/// The plugin's locale resources, installed on install and by an update whose version contains DATA
/// </summary>
public static class FreightLocaleResources
{
    /// <summary>
    /// Prefix of every resource key the plugin owns; uninstall deletes by it
    /// </summary>
    public const string Prefix = "Plugins.CW.Freight";

    /// <summary>
    /// Gets the resources, key to English text
    /// </summary>
    public static Dictionary<string, string> All => new()
    {
        ["Plugins.CW.Freight.Fields.Name"] = "Name",
        ["Plugins.CW.Freight.Fields.Name.Hint"] = "The name of this shipping method.",
        ["Plugins.CW.Freight.Fields.Description"] = "Description",
        ["Plugins.CW.Freight.Fields.Description.Hint"] = "A short description of this shipping method.",
        ["Plugins.CW.Freight.Fields.DisplayOrder"] = "Display order",
        ["Plugins.CW.Freight.Fields.DisplayOrder.Hint"] = "Display order of this shipping method.",
        ["Plugins.CW.Freight.Fields.Rate"] = "Rate",
        ["Plugins.CW.Freight.Fields.Rate.Hint"] = "The shipping rate for this method.",
        ["Plugins.CW.Freight.Fields.LowestOrderTotal"] = "Lowest order total",
        ["Plugins.CW.Freight.Fields.LowestOrderTotal.Hint"] = "Minimum order total for this rate to apply.",
        ["Plugins.CW.Freight.Fields.HighestOrderTotal"] = "Highest order total",
        ["Plugins.CW.Freight.Fields.HighestOrderTotal.Hint"] = "Maximum order total for this rate to apply.",
        ["Plugins.CW.Freight.Fields.ApplyPerBranch"] = "Apply per branch",
        ["Plugins.CW.Freight.Fields.ApplyPerBranch.Hint"] = "When enabled, the rate is applied per branch.",
        ["Plugins.CW.Freight.Fields.LimitMethodsToCreated"] = "Limit shipping methods to configured ones",
        ["Plugins.CW.Freight.Fields.LimitMethodsToCreated.Hint"] = "When enabled, only configured shipping methods are shown to customers.",
        ["Plugins.CW.Freight.Fields.SearchStoreId"] = "Store",
        ["Plugins.CW.Freight.Fields.SearchStoreId.Hint"] = "Select a store to search for shipping methods.",
        ["Plugins.CW.Freight.Fields.SelectedStoreIds"] = "Stores",
        ["Plugins.CW.Freight.Fields.SelectedStoreIds.Hint"] = "Stores this shipping method is offered in. Leave empty to offer it in every store.",
        ["Plugins.CW.Freight.Fields.SelectedCustomerRoleIds"] = "Customer roles",
        ["Plugins.CW.Freight.Fields.SelectedCustomerRoleIds.Hint"] = "Customer roles this shipping method is available to.",
        ["Plugins.CW.Freight.Fields.RestrictedCountryIds"] = "Restricted countries",
        ["Plugins.CW.Freight.Fields.RestrictedCountryIds.Hint"] = "Countries where this shipping method is NOT available. Leave empty to offer it in every country."
    };
}
