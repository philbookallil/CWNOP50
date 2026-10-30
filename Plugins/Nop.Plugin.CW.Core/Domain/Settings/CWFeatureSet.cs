namespace Nop.Plugin.CW.Core.Domain.Settings;

/// <summary>
/// Represents the enabled state of every CW feature for a single store
/// </summary>
public readonly record struct CWFeatureSet(bool PackName, bool Subsidy, bool Exchanges, bool CreditNotes, bool BulkOrder)
{
    /// <summary>Gets a value indicating whether the specified feature is enabled</summary>
    public bool this[CWFeature feature] => feature switch
    {
        CWFeature.PackName => PackName,
        CWFeature.Subsidy => Subsidy,
        CWFeature.Exchanges => Exchanges,
        CWFeature.CreditNotes => CreditNotes,
        CWFeature.BulkOrder => BulkOrder,
        _ => false
    };

    /// <summary>
    /// Creates a feature set from store settings, applying feature implications
    /// </summary>
    public static CWFeatureSet From(MultiStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CWFeatureSet(
            PackName: settings.PackNameEnable,
            Subsidy: settings.SubsidyEnable && settings.PackNameEnable,
            Exchanges: settings.ExchangesEnable,
            CreditNotes: settings.CreditNotesEnable,
            BulkOrder: settings.BulkOrderEnable);
    }
}