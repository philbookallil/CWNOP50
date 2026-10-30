using Nop.Web.Framework.UI.Paging;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

public record SubsidyCatalogCommand : BasePageableModel
{
    /// <summary>Gets or sets the product view mode (grid or list).</summary>
    public string ViewMode { get; set; }
}
