using Microsoft.AspNetCore.Html;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;

/// <summary>
/// One row of the admin fees panel. The amount is priced by ExchangeService and passed in — this model
/// carries no rule and does no arithmetic, which is what stops the screen becoming another fee walk.
/// </summary>
public record FeeConceptComponentModel : BaseNopModel
{
    public decimal FeeAmount { get; set; }

    public IHtmlContent Label { get; set; }

    public IHtmlContent TextBox { get; set; }

    public IHtmlContent FeeConceptHiddenField { get; set; }

    public Currency Currency { get; set; }

    /// <summary>
    /// The live rule behind this row, or null when there is none. Rendered as hidden inputs so the browser
    /// can re-price the row as staff type — never read back on POST; the server always re-prices from the
    /// rule itself.
    /// </summary>
    public FeeConceptItem Rule { get; set; }
}