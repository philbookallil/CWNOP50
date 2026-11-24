using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// A person who approves orders from an e-mail link. Not a customer: an approver needs no account.
/// </summary>
public class Approver : BaseEntity, ISoftDeletedEntity
{
    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the name shown in e-mails and on the admin pages
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address the approval requests go to; an address names one live approver of a store
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the approver receives requests
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the approver has been deleted
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}
