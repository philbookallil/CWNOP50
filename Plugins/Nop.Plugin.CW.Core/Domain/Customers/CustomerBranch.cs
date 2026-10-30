using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Customers;

public class CustomerBranch : BaseEntity
{
    /// <summary>
    /// Gets or sets the branch identifier where the user belongs 
    /// </summary>
    public int? BranchId { get; set; }

    /// <summary>
    /// Gets or sets the JimCardCode
    /// </summary>
    public string JimCardCode { get; set; }

    /// <summary>
    /// Gets or sets the date and time of last modified
    /// </summary>
    public DateTime LastModifiedDateUtc { get; set; }
}
