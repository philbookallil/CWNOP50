namespace Nop.Plugin.CW.Core.Services.Exchanges;

public class ReturnRequestWorkflowResult
{
    #region Ctor

    public ReturnRequestWorkflowResult()
    {
        Errors = new List<string>();
    }

    #endregion

    #region Properties

    public IList<string> Errors { get; set; }

    public bool NeedsCompletion { get; set; }

    public bool OrderWasPlaced { get; set; }

    public bool CreditNoteWasCreated { get; set; }

    /// <summary>
    /// Subsidy consumed by the returned line was given back in kind — units, group credit or global
    /// credit — so the replacement (or the customer's next order) can draw on it.
    /// </summary>
    public bool SubsidyWasRestored { get; set; }

    /// <summary>
    /// A Credit-Note-mode line carried fees but had no money to deduct them from — the returned line was
    /// paid by subsidy, which went back in kind. Nothing was collected; Sales Support adjust the fee
    /// quantities if the fees should not stand.
    /// </summary>
    public bool FeesNotDeductible { get; set; }

    #endregion
}