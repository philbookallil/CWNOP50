using System.Text;
using System.Text.RegularExpressions;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Configuration;

namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Default implementation: reads the per-store <see cref="CwThemeSettings"/> and emits a
/// <c>:root{}</c> block of CSS custom properties. No LESS/compiler (unlike the NT dynamic
/// pipeline) — plain CSS variables. Settings are already cached by <see cref="ISettingService"/>,
/// so generating this short string per request is cheap and needs no extra cache layer.
/// </summary>
public partial class CwThemeCssService : ICwThemeCssService
{
    #region Fields

    protected readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public CwThemeCssService(ISettingService settingService)
    {
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    [GeneratedRegex("^[0-9a-fA-F]{3,8}$")]
    private static partial Regex HexColorRegex();

    private static bool IsHexColor(string value)
        => !string.IsNullOrWhiteSpace(value) && HexColorRegex().IsMatch(value.Trim());

    private static void AppendVar(StringBuilder builder, string name, string hexNoHash)
    {
        if (IsHexColor(hexNoHash))
            builder.Append(name).Append(":#").Append(hexNoHash.Trim()).Append(';');
    }

    #endregion

    #region Methods

    /// <summary>
    /// Generate the :root{} custom-property block for the given store.
    /// </summary>
    public virtual async Task<string> GenerateRootCssAsync(int storeId)
    {
        var settings = await _settingService.LoadSettingAsync<CwThemeSettings>(storeId);

        var builder = new StringBuilder();
        builder.Append(":root{");
        var startLength = builder.Length;

        //accent colours — unless the admin opted out for this store (colour control left to the theme)
        if (!settings.DisableAccentColors)
        {
            AppendVar(builder, CwThemeCoreDefaults.CssVarPrimary, settings.PrimaryColor);
            AppendVar(builder, CwThemeCoreDefaults.CssVarPrimaryTint, settings.PrimaryTintColor);
            AppendVar(builder, CwThemeCoreDefaults.CssVarSuccess, settings.SuccessColor);
            AppendVar(builder, CwThemeCoreDefaults.CssVarDanger, settings.DangerColor);
            AppendVar(builder, CwThemeCoreDefaults.CssVarWarning, settings.WarningColor);
            AppendVar(builder, CwThemeCoreDefaults.CssVarDark, settings.DarkColor);
        }

        //button shape — a non-colour brand knob, emitted regardless of the colour opt-out (0 = fall back to theme)
        if (settings.ButtonRadius > 0)
            builder.Append(CwThemeCoreDefaults.CssVarButtonRadius).Append(':').Append(settings.ButtonRadius).Append("px;");

        //nothing valid to emit
        if (builder.Length == startLength)
            return string.Empty;

        builder.Append('}');
        return builder.ToString();
    }

    #endregion
}
