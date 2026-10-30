namespace Nop.Plugin.CW.Core.Domain.Common;

public partial interface INamedEntity
{
    /// <summary>
    /// Gets or sets the entity identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the entity name
    /// </summary>
    public string Name { get; set; }
}
