namespace Nop.Plugin.CW.Core.Models.PackNames;

public record PackNameInfoModel
{
    #region Properties

    public string Name { get; set; }

    public string Colour { get; set; }

    public bool IsGroup { get; set; }

    public bool IsCustomer { get; set; }

    public bool IsManager { get; set; }

    public string Branch { get; set; }

    public string Department { get; set; }

    public string CostCenter { get; set; }

    public string POEntry { get; set; }

    /// <summary>True when the PackName has at least one subsidy — controls flyout trigger visibility.</summary>
    public bool HasSubsidies { get; set; }

    /// <summary>False when the packname's subsidy ordering period has closed: the strip shows the notice.</summary>
    public bool CanOrder { get; set; } = true;

    /// <summary>False when the packname has no address its orders can be shipped to: the strip shows a warning.</summary>
    public bool HasShippingAddress { get; set; } = true;

    #endregion
}
