namespace Nop.Plugin.CW.ThemeCore.Components;

/// <summary>View model for the optional second (co-brand) header logo.</summary>
public class CwSecondLogoModel
{
    /// <summary>Resolved image URL of the second logo.</summary>
    public string ImageUrl { get; set; }

    /// <summary>Optional link URL; when empty the logo renders as a plain image.</summary>
    public string LinkUrl { get; set; }

    /// <summary>Alt/title text (defaults to the store name).</summary>
    public string AltText { get; set; }
}
