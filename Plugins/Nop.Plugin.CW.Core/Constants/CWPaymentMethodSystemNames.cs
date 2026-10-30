namespace Nop.Plugin.CW.Core.Constants
{
    /// <summary>
    /// Payment-method system names used by the CW store-credit / subsidy flows
    /// </summary>
    public class CWPaymentMethodSystemNames
    {
        /// <summary>Order paid entirely with credit notes</summary>
        public const string CREDIT_NOTE = "CreditNote";

        /// <summary>Order paid with subsidy</summary>
        public const string SUBSIDY = "Subsidy";

        /// <summary>Order paid with a combination of subsidy and credit notes</summary>
        public const string SUBSIDY_AND_CREDIT_NOTE = "Subsidy & CreditNote";
    }
}
