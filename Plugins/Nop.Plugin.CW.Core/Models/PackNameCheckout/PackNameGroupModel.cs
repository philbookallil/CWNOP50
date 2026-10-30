namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public record PackNameCartItemWithBox
{
    public PackNameShoppingCartModel.PackNameShoppingCartItemModel Item { get; set; }

    public PackNameBoxModel Box { get; set; }
}

public record PackNameGroupModel
{
    /// <summary>First item in the group — used for the product/packname header.</summary>
    public PackNameShoppingCartModel.PackNameShoppingCartItemModel Header { get; set; }

    /// <summary>All items in this group, each paired with its packname shipping box.</summary>
    public IList<PackNameCartItemWithBox> Items { get; set; } = [];

    public int CartThumbPictureSize { get; set; }
}
