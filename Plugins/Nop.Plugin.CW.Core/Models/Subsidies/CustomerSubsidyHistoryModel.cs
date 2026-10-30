using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// One movement on an assignment, written as a sentence, newest first.
/// </summary>
public record CustomerSubsidyHistoryModel : BaseNopModel
{
    public string CreatedOn { get; set; }

    public string What { get; set; }

    public int OrderId { get; set; }

    /// <summary>
    /// Units moved, signed ("+ 5", "− 2"); empty when the row moves no units.
    /// </summary>
    public string Units { get; set; }

    /// <summary>
    /// Credit moved, signed and formatted; empty when the row moves no money.
    /// </summary>
    public string Credit { get; set; }

    public bool AssignmentRemoved { get; set; }

    /// <summary>
    /// True when the order is the viewing customer's own, the only case nop's order page lets them open it.
    /// A manager reading an employee's page, or an employee whose manager placed the order, sees the row unlinked.
    /// </summary>
    public bool CanOpenOrder { get; set; }
}