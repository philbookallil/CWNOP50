using Nop.Core;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Domain.Customers.DTOs;

public class CustomerDTO : BaseEntity
{
    /// <summary>
    /// Gets or sets the email
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the first name
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the Branch
    /// </summary>
    public string Branch { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the customer is active
    /// </summary>
    public bool Active { get; set; }

    public PackName PackName { get; set; }
}
