namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// What happened to one Odoo record in a sync run.
/// </summary>
internal enum SyncOutcome
{
    /// <summary>Anything that did not complete as a create or update: unchanged, not applicable
    /// (archived, missing parent, no name, …) or failed (logged as an error). The default, so a
    /// record whose processing stops early or throws is counted here without extra code.</summary>
    Skipped,
    Created,
    Updated
}

/// <summary>
/// Counts a sync run's outcomes. Usage per record: start from <see cref="SyncOutcome.Skipped"/>, set
/// Created/Updated as the LAST step of the success path, and call <see cref="Add"/> in a <c>finally</c> —
/// so every record is counted exactly once and Created + Updated + Skipped = Processed.
/// </summary>
internal sealed class SyncTally
{
    public int Created { get; private set; }
    public int Updated { get; private set; }
    public int Skipped { get; private set; }

    public void Add(SyncOutcome outcome)
    {
        switch (outcome)
        {
            case SyncOutcome.Created: Created++; break;
            case SyncOutcome.Updated: Updated++; break;
            default: Skipped++; break;
        }
    }
}
