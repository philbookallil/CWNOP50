using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;

public record FeeConceptItem
{
    public string Concept { get; set; }
    public FeeRuleOperator FeeOperator
    {
        get
        {
            return (FeeRuleOperator)this.FeeOperatorId;
        }
    }
    public FeeType FeeType
    {
        get
        {
            return (FeeType)this.FeeTypeId;
        }
    }

    public int FeeOperatorId { get; set; }
    public int FeeTypeId { get; set; }
    public decimal FeeAmount { get; set; }
    public int FeeStoreId { get; set; }
    public int FeeUnitsQuantity { get; set; }

    public FeeConceptItem()
    {
    }

    public FeeConceptItem(Domain.Exchanges.FeeRule feeRule, string concept)
    {
        this.Concept = concept;
        this.FeeAmount = feeRule.Amount;
        this.FeeTypeId = feeRule.FeeTypeId;
        this.FeeOperatorId = feeRule.OperatorId;
        this.FeeStoreId = feeRule.StoreId;
        this.FeeUnitsQuantity = feeRule.UnitsQuantity;
    }
}