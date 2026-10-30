using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// The recorded outcome of resolving a <see cref="ReturnCase"/>. Written once; its existence is what
/// makes resolving a case a second time a no-op.
/// </summary>
public class ReturnResolution : BaseEntity
{
    /// <summary>
    /// Gets or sets the return case this resolves
    /// </summary>
    public int ReturnCaseId { get; set; }

    /// <summary>
    /// Gets or sets when the case was resolved (UTC)
    /// </summary>
    public DateTime ResolvedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the staff member who resolved it
    /// </summary>
    public int? ResolvedByCustomerId { get; set; }

    /// <summary>
    /// Gets or sets the amount still owed by the customer at the point of resolution
    /// </summary>
    public decimal BalanceDue { get; set; }

    /// <summary>
    /// Gets or sets the resolution status identifier
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets the resolution status
    /// </summary>
    public ReturnResolutionStatus Status
    {
        get => (ReturnResolutionStatus)StatusId;
        set => StatusId = (int)value;
    }
}