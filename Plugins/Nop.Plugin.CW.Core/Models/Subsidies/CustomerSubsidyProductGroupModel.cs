using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// One product group of an assignment, with units and credit shown only where the group allocates them.
/// </summary>
public record CustomerSubsidyProductGroupModel : BaseNopModel
{
    public string Name { get; set; }

    public string Icon { get; set; }

    public string Colour { get; set; }

    public bool HasUnits { get; set; }

    public int TotalUnits { get; set; }

    public int UsedUnits { get; set; }

    public int RemainingUnits { get; set; }

    public int TopupUnits { get; set; }

    public bool HasCredit { get; set; }

    public string TotalCreditFormatted { get; set; }

    public string UsedCreditFormatted { get; set; }

    public string RemainingCreditFormatted { get; set; }

    public string TopupCreditFormatted { get; set; }
}