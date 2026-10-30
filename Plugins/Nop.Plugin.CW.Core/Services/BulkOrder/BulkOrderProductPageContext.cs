using Nop.Plugin.CW.Core.Models.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// One request's answer to "does the bulk grid take over this product page, and what does it show".
/// Registered scoped, so it is per request.
/// </summary>
/// <remarks>
/// Three parties need the same answer at three different moments of one request and none of them can ask
/// the others: the ModelPrepared consumer, which runs in a result filter before any view; the
/// PageRenderingEvent consumer, which runs while the HEAD is rendering and decides whether to load the
/// stylesheet; and the view component, which runs while the BODY is rendering. Resolving three times would
/// build the grid three times.
///
/// A scoped service rather than HttpContext.Items: it is typed, it needs no IHttpContextAccessor, and the
/// lifetime is stated by the registration instead of by convention.
/// </remarks>
public class BulkOrderProductPageContext
{
    /// <summary>
    /// The grid to render, or null — which means either "not a product page" or "leave this page alone".
    /// Nothing downstream needs to tell those apart: both render nothing and suppress nothing.
    /// </summary>
    public BulkOrderGridModel Grid { get; set; }

    /// <summary>
    /// Set by the first widget zone that renders the grid, so the two fallback zones behind it are no-ops.
    /// The component is registered on three zones and at most one of them may draw.
    /// </summary>
    public bool Rendered { get; set; }
}