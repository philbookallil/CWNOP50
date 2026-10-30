namespace Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;

public record FeesAndChargesListModel
{
    public int ItemsQuantity { get; set; }

    public IList<FeeConceptItem> FeesItems { get; set; }

    public FeesAndChargesListModel(int itemsQuantity, IList<FeeConceptItem> feesItems)
    {
        this.ItemsQuantity = itemsQuantity;
        this.FeesItems = feesItems;
    }
}