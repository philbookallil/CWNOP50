using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Catalog;

/// <summary>
/// The per-product override for the bulk grid on the product details page.
/// </summary>
public record BulkOrderProductPageModel : BaseNopModel
{
    public int ProductId { get; set; }

    /// <summary>
    /// "" follows the store, "1" forces on, "0" forces off. A string rather than a bool? because it is what
    /// the GenericAttribute stores and what the radio group posts; converting twice would only add a place
    /// for the three states to become two.
    /// </summary>
    [NopResourceDisplayName("Plugins.CW.Core.Admin.BulkOrder.ShowOnProductPage")]
    public string ShowOnProductPage { get; set; }

    /// <summary>What the store setting currently says, so "Follow the store" can state which way that is.</summary>
    public bool StoreDefault { get; set; }
}