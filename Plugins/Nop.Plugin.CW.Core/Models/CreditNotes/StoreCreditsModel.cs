using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.CreditNotes;

public record StoreCreditsModel : BaseNopModel
{
    public StoreCreditSectionModel StoreCredit { get; set; } = new();

    public partial record StoreCreditSectionModel : BaseNopModel
    {
        /// <summary>True when the customer has any current balance or any usage history.</summary>
        public bool HasStoreCredit { get; set; }

        /// <summary>Formatted total of the remaining balance across all the customer's credit notes.</summary>
        public string AvailableBalance { get; set; }

        /// <summary>Credit notes still carrying a positive balance, oldest first (the order they are used in).</summary>
        public IList<CreditNoteLineModel> IssuedCreditNotes { get; set; } = new List<CreditNoteLineModel>();

        /// <summary>Ledger of credit applied to orders (consumption history), newest first.</summary>
        public IList<CreditLedgerEntryModel> Ledger { get; set; } = new List<CreditLedgerEntryModel>();
    }

    public partial record CreditNoteLineModel : BaseNopModel
    {
        public string Name { get; set; }
        public int OrderId { get; set; }
        public string CreatedOn { get; set; }
        public string Amount { get; set; }
        public string Remaining { get; set; }
    }

    public partial record CreditLedgerEntryModel : BaseNopModel
    {
        public string Name { get; set; }
        public int OrderId { get; set; }
        public string CreatedOn { get; set; }
        public string AmountApplied { get; set; }
    }
}
