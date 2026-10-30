using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;

public record FeeDisplayModel
{
    public FeeType FeeType { get; set; }

    public decimal FeeAmount { get; set; }

    /// <summary>
    /// The amount formatted for the page that shows it
    /// </summary>
    public string FeeAmountDisplay { get; set; }

    public string FeeConcept { get; set; }
}