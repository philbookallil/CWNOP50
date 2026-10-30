using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents an impersonation token
/// </summary>
public class ImpersonationToken : BaseEntity
{
    /// <summary>
    /// A string in GUID format that serves as the TOKEN
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user (NOP customer) that requests the impersonation
    /// </summary>
    public int OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user (NOP customer) that is going to be impersonated
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the date the TOKEN was created
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the ID of the Store for which the Token is being created
    /// </summary>
    public int StoreId { get; set; }
}