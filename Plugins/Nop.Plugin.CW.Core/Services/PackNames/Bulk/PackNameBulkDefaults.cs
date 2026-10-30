namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// The limits of the admin packname bulk actions
/// </summary>
public static class PackNameBulkDefaults
{
    /// <summary>
    /// The most packnames one bulk action takes
    /// </summary>
    public const int MaxPackNames = 500;

    /// <summary>
    /// How many packnames are written in one transaction, so the rows a batch locks are released within a second
    /// </summary>
    public const int WriteBatchSize = 10;
}