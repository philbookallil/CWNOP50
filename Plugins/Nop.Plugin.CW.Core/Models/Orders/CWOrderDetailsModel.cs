using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Models.Orders;

/// <summary>
/// nop's order details model with the packname groups and subsidy totals of the order.
/// </summary>
/// <remarks>
/// nop's CustomProperties holds strings only, so the details travel on a derived model, the same way
/// PackNameMiniShoppingCartModel extends nop's mini cart model. The view reads them with
/// <c>Model as CWOrderDetailsModel</c>, so a plain OrderDetailsModel still renders.
/// </remarks>
public record CWOrderDetailsModel : OrderDetailsModel
{
    /// <summary>
    /// Copies nop's prepared model.
    /// </summary>
    /// <param name="model">The model nop prepared</param>
    public CWOrderDetailsModel(OrderDetailsModel model) : base(model)
    {
    }

    /// <summary>The packname groups and subsidy totals; empty for an order with no packname lines.</summary>
    public PackNameOrderDetailsModel PackNameDetails { get; set; } = new();
}