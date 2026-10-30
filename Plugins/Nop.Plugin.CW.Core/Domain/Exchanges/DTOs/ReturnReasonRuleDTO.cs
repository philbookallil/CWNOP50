namespace Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;

public class ReturnReasonRuleDTO : ReturnReasonRule
{
    public string ReturnReason { get; set; }

    public int DisplayOrder { get; set; }

    public int LinkedActionsCount { get; set; }

    public string LinkedActionsNames { get; set; }

}
