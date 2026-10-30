namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// What a bulk action did
/// </summary>
public class PackNameBulkResult
{
    /// <summary>
    /// Gets or sets why the whole request was refused before anything was written; null when it was not
    /// </summary>
    public string RefusedMessage { get; set; }

    /// <summary>
    /// Gets or sets how many packnames the action changed
    /// </summary>
    public int Changed { get; set; }

    /// <summary>
    /// Gets or sets how many packnames already were as the action would leave them (already on, already off, already in
    /// the list)
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// Gets the packnames the action left alone because a rule refused them, with the reason
    /// </summary>
    public IList<PackNameBulkSkip> Skipped { get; } = new List<PackNameBulkSkip>();

    /// <summary>
    /// Gets or sets how many packnames were not reached because the action stopped on an error; what was done before the
    /// error stays done
    /// </summary>
    public int NotProcessed { get; set; }

    /// <summary>
    /// Gets or sets the message that says the action stopped part way; null when it did not
    /// </summary>
    public string StoppedMessage { get; set; }
}

/// <summary>
/// A packname a bulk action left alone
/// </summary>
/// <param name="PackNameId">The packname identifier</param>
/// <param name="Name">The packname's name, or its identifier when it could not be found</param>
/// <param name="Reason">Why it was left alone</param>
public record PackNameBulkSkip(int PackNameId, string Name, string Reason);