using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Models.ShoppingCart;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public record PackNameShoppingCartModel : ShoppingCartModel
{
    public bool HasItems { get; set; }

    public new List<PackNameShoppingCartItemModel> Items { get; set; } = [];

    public IList<PackNameGroupModel> GroupedItems { get; set; } = [];

    #region Nested Classes

    public partial record PackNameShoppingCartItemModel : CWCartItemModel
    {
        public PackNameShoppingCartItemModel(ShoppingCartModel.ShoppingCartItemModel source)
                : base(source) { }

        public PackNameShoppingCartItemModel() { }

        public PackNameModel PackNameModel { get; set; } = new();

        public IList<SubsidyItemBreakdownModel> SubsidyBreakdown { get; set; } = [];
    }

    #endregion
}
