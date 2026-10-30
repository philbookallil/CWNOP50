using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Settings;

public partial class MultiStoreSettingsValidator : BaseNopValidator<MultiStoreSettingsModel>
{
    public MultiStoreSettingsValidator(ILocalizationService localizationService)
    {
        //posted on the default-settings screen (no store), and on a store's screen only with its override ticked;
        //read, and shown, only while packnames and subsidies are on, managers add packnames and pick subsidies, and the
        //Excel import is on: the condition the settings screens show the row under
        RuleFor(x => x.AllowSubsidyCountByExcel)
            .InclusiveBetween(1, PackNameImportDefaults.MaxSubsidyGroups)
            .When(x => x.PackNameEnable && x.SubsidyEnable && x.AllowManagersToAddPackNames && x.AllowSubsidySelection && x.AllowToAddByExcel
                && (x.StoreId == 0 || x.AllowSubsidyCountByExcel_OverrideForStore))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel.Range"));
    }
}