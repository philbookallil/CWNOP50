namespace Nop.Plugin.CW.Core.Domain.Settings;

/// <summary>
/// Represents a CW feature that can be enabled or disabled per store
/// </summary>
public enum CWFeature
{
    /// <summary>PackNames (MultiStoreSettings.PackNameEnable)</summary>
    PackName = 1,

    /// <summary>Subsidies (MultiStoreSettings.SubsidyEnable)</summary>
    Subsidy = 2,

    /// <summary>Exchanges and returns (MultiStoreSettings.ExchangesEnable)</summary>
    Exchanges = 3,

    /// <summary>Credit notes (MultiStoreSettings.CreditNotesEnable)</summary>
    CreditNotes = 4,

    /// <summary>Bulk ordering (MultiStoreSettings.BulkOrderEnable)</summary>
    BulkOrder = 5
}