using FluentValidation;
using Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Validators;

/// <summary>
/// Validates that each accent colour is empty or a valid hex value (3, 4, 6 or 8 hex digits, no '#').
/// </summary>
public class CwThemeSettingsValidator : BaseNopValidator<CwThemeSettingsModel>
{
    private const string HEX_PATTERN = "^([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$";

    public CwThemeSettingsValidator(ILocalizationService localizationService)
    {
        RuleFor(m => m.PrimaryColor)
            .Matches(HEX_PATTERN)
            .When(m => !string.IsNullOrWhiteSpace(m.PrimaryColor))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.Color.Invalid"));

        RuleFor(m => m.PrimaryTintColor)
            .Matches(HEX_PATTERN)
            .When(m => !string.IsNullOrWhiteSpace(m.PrimaryTintColor))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.Color.Invalid"));

        RuleFor(m => m.SuccessColor)
            .Matches(HEX_PATTERN)
            .When(m => !string.IsNullOrWhiteSpace(m.SuccessColor))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.Color.Invalid"));

        RuleFor(m => m.DangerColor)
            .Matches(HEX_PATTERN)
            .When(m => !string.IsNullOrWhiteSpace(m.DangerColor))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.Color.Invalid"));

        RuleFor(m => m.WarningColor)
            .Matches(HEX_PATTERN)
            .When(m => !string.IsNullOrWhiteSpace(m.WarningColor))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.Color.Invalid"));

        RuleFor(m => m.DarkColor)
            .Matches(HEX_PATTERN)
            .When(m => !string.IsNullOrWhiteSpace(m.DarkColor))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.Color.Invalid"));

        RuleFor(m => m.ButtonRadius)
            .InclusiveBetween(0, 999)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Fields.ButtonRadius.Invalid"));
    }
}
