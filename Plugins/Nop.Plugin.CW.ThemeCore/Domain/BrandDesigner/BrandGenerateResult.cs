namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>Where a generated style came from.</summary>
public enum BrandResultSource
{
    Preset,
    Ai,
    Offline
}

/// <summary>
/// What one Generate returns to the Brand Designer page.
/// </summary>
public class BrandGenerateResult
{
    /// <summary>The style to apply. Null with <see cref="Error"/>, which leaves the form unchanged.</summary>
    public BrandStyle Style { get; set; }

    /// <summary>Validation notes, shown beside the summary.</summary>
    public IList<string> Notes { get; set; } = new List<string>();

    /// <summary>One sentence from the AI: what changed and why.</summary>
    public string Summary { get; set; }

    public BrandResultSource Source { get; set; }

    /// <summary>The preset name of the provider that answered; empty when no provider did.</summary>
    public string Provider { get; set; }

    /// <summary>An admin-readable message when generation failed.</summary>
    public string Error { get; set; }

    /// <summary>The offline generator answered. The page reads this rather than the enum, which arrives as a number.</summary>
    public bool IsOffline => Source == BrandResultSource.Offline;
}
