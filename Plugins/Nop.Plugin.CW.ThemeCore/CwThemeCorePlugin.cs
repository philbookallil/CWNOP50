using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Logging;
using Nop.Data;
using Nop.Plugin.CW.ThemeCore.Components;
using Nop.Plugin.CW.ThemeCore.Services;
using Nop.Plugin.CW.ThemeCore.Services.Security;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.CW.ThemeCore;

/// <summary>
/// Central CW theme framework plugin. Renders per-store accent-colour custom properties
/// into the page head (Capability 1) and exposes an admin settings page.
/// </summary>
public class CwThemeCorePlugin : BasePlugin, IWidgetPlugin, IMiscPlugin, IConsumer<AdminMenuCreatedEvent>
{
    #region Fields

    protected readonly IAdminMenu _adminMenu;
    protected readonly IRepository<ActivityLogType> _activityLogTypeRepository;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly ICwThemeCssFileService _cssFileService;
    protected readonly IWebHelper _webHelper;
    protected readonly WidgetSettings _widgetSettings;

    #endregion

    #region Ctor

    public CwThemeCorePlugin(
        IAdminMenu adminMenu,
        IRepository<ActivityLogType> activityLogTypeRepository,
        ILocalizationService localizationService,
        ISettingService settingService,
        ICwThemeCssFileService cssFileService,
        IWebHelper webHelper,
        WidgetSettings widgetSettings)
    {
        _adminMenu = adminMenu;
        _activityLogTypeRepository = activityLogTypeRepository;
        _localizationService = localizationService;
        _settingService = settingService;
        _cssFileService = cssFileService;
        _webHelper = webHelper;
        _widgetSettings = widgetSettings;
    }

    #endregion

    #region Widget

    /// <summary>
    /// Render into the core head zone so the :root{} block loads on any active theme.
    /// </summary>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.HeadHtmlTag });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        ArgumentNullException.ThrowIfNull(widgetZone);

        if (widgetZone.Equals(PublicWidgetZones.HeadHtmlTag))
            return typeof(CwThemeHeadViewComponent);

        return null;
    }

    /// <summary>
    /// Infrastructure widget — hidden from the admin widget list (managed automatically).
    /// </summary>
    public bool HideInWidgetList => true;

    #endregion

    #region Admin menu

    public async Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
    {
        var themeMenu = new AdminMenuItem
        {
            SystemName = CwThemeCoreDefaults.AdminMenuSystemName,
            Title = await _localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Admin.Menu"),
            Url = _adminMenu.GetMenuItemUrl("CwThemeCore", "Configure"),
            IconClass = "far fa-dot-circle",
            Visible = true,
            PermissionNames = new List<string> { CwThemeCorePermissionConfigManager.MANAGE }
        };

        //anchor under the CW.Core menu group if present (soft dependency), else at root
        var coreMenu = eventMessage.RootMenuItem.GetItemBySystemName(CwThemeCoreDefaults.CwCoreMenuSystemName);
        if (coreMenu is not null)
            coreMenu.ChildNodes.Add(themeMenu);
        else
            eventMessage.RootMenuItem.ChildNodes.Add(themeMenu);
    }

    #endregion

    #region Methods

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/CwThemeCore/Configure";
    }

    public override async Task InstallAsync()
    {
        //provisional default palette (pending design) — hex without '#'
        await _settingService.SaveSettingAsync(new CwThemeSettings
        {
            PrimaryColor = "00549f",
            PrimaryTintColor = "eaf4fc",
            SuccessColor = "28a745",
            DangerColor = "dc3545",
            WarningColor = "ffc107",
            DarkColor = "343a40",
            ButtonRadius = 8,
            LogoPictureId = 0,
            DisableAccentColors = false
        });

        //no connection yet: the Brand Designer runs in offline mode until an admin sets one
        await _settingService.SaveSettingAsync(new CwThemeAiSettings());

        //activate the widget so the head injection renders without manual toggling
        if (!_widgetSettings.ActiveWidgetSystemNames.Contains(CwThemeCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add(CwThemeCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await InstallActivityLogTypeAsync();
        await AddOrUpdateLocaleResourceAsync();

        await base.InstallAsync();
    }

    /// <summary>
    /// Update plugin
    /// </summary>
    /// <param name="currentVersion">Current version of plugin</param>
    /// <param name="targetVersion">New version of plugin</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
        {
            await InstallActivityLogTypeAsync();
            await AddOrUpdateLocaleResourceAsync();
        }

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    private async Task InstallActivityLogTypeAsync()
    {
        if (await _activityLogTypeRepository.Table.AnyAsync(type => type.SystemKeyword == CwThemeCoreDefaults.AiGenerateActivityType))
            return;

        await _activityLogTypeRepository.InsertAsync(new ActivityLogType
        {
            SystemKeyword = CwThemeCoreDefaults.AiGenerateActivityType,
            Enabled = true,
            Name = "Generate a brand style with AI"
        });
    }

    private async Task AddOrUpdateLocaleResourceAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.CW.ThemeCore.Admin.Menu"] = "CW Theme",
            ["Plugins.CW.ThemeCore.Configure.Title"] = "CW Theme settings",
            ["Plugins.CW.ThemeCore.Configure.FormErrors"] = "The settings were not saved. Correct the fields marked below and save again.",
            ["Plugins.CW.ThemeCore.Configure.AccentColours"] = "Accent colours",
            ["Plugins.CW.ThemeCore.Configure.AccentColours.Hint"] = "These colours are exposed to all CW plugins and themes as CSS custom properties (--cw-*). Values are per store.",
            ["Plugins.CW.ThemeCore.Configure.Logo"] = "Logo",
            ["Plugins.CW.ThemeCore.Configure.Preview"] = "Live preview",
            ["Plugins.CW.ThemeCore.Fields.PrimaryColor"] = "Primary colour",
            ["Plugins.CW.ThemeCore.Fields.PrimaryColor.Hint"] = "The main brand/accent colour (CSS var --cw-primary).",
            ["Plugins.CW.ThemeCore.Fields.PrimaryTintColor"] = "Primary tint",
            ["Plugins.CW.ThemeCore.Fields.PrimaryTintColor.Hint"] = "A light tint of the primary colour, used for subtle backgrounds (--cw-primary-tint).",
            ["Plugins.CW.ThemeCore.Fields.SuccessColor"] = "Success colour",
            ["Plugins.CW.ThemeCore.Fields.SuccessColor.Hint"] = "Positive/success accent (--cw-success).",
            ["Plugins.CW.ThemeCore.Fields.DangerColor"] = "Danger colour",
            ["Plugins.CW.ThemeCore.Fields.DangerColor.Hint"] = "Error/danger accent (--cw-danger).",
            ["Plugins.CW.ThemeCore.Fields.WarningColor"] = "Warning colour",
            ["Plugins.CW.ThemeCore.Fields.WarningColor.Hint"] = "Warning accent (--cw-warning).",
            ["Plugins.CW.ThemeCore.Fields.DarkColor"] = "Dark colour",
            ["Plugins.CW.ThemeCore.Fields.DarkColor.Hint"] = "Dark ink/surface accent (--cw-dark).",
            ["Plugins.CW.ThemeCore.Fields.ButtonRadius"] = "Button radius (px)",
            ["Plugins.CW.ThemeCore.Fields.ButtonRadius.Hint"] = "Corner radius of buttons in pixels (--cw-btn-radius): about 2 is square, 8 rounded, 999 a full pill. The Brand designer sets this from prompts like \"square buttons\" or \"pill buttons\". 0 leaves it to the theme.",
            ["Plugins.CW.ThemeCore.Fields.ButtonRadius.Invalid"] = "Enter a whole number between 0 and 999.",
            ["Plugins.CW.ThemeCore.Fields.LogoPictureId"] = "Logo",
            ["Plugins.CW.ThemeCore.Fields.LogoPictureId.Hint"] = "The store logo used by CW themes.",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoPictureId"] = "Second logo",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoPictureId.Hint"] = "An optional co-brand logo shown on the right of the header, alongside the primary logo (per store). Leave empty for none.",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoLinkUrl"] = "Second logo link",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoLinkUrl.Hint"] = "Optional URL the second logo links to (e.g. a partner site). Leave blank to render it as a plain image.",
            ["Plugins.CW.ThemeCore.Fields.ReverseLogos"] = "Reverse logo positions",
            ["Plugins.CW.ThemeCore.Fields.ReverseLogos.Hint"] = "Swap the header logos left-to-right: the second (co-brand) logo shows on the left and the primary logo on the right. Per store.",
            ["Plugins.CW.ThemeCore.Fields.Color.Invalid"] = "Enter a valid hex colour (e.g. 00549f), without the '#'.",
            ["Plugins.CW.ThemeCore.Fields.DisableAccentColors"] = "Don't apply accent colours",
            ["Plugins.CW.ThemeCore.Fields.DisableAccentColors.Hint"] = "When enabled, CW.ThemeCore does not emit the accent-colour variables (--cw-*), leaving colour control entirely to the active theme. Custom CSS is unaffected.",
            ["Plugins.CW.ThemeCore.Configure.CustomCss"] = "Custom CSS",
            ["Plugins.CW.ThemeCore.Fields.CustomCss"] = "Custom CSS",
            ["Plugins.CW.ThemeCore.Fields.CustomCss.Hint"] = "Store-specific CSS applied to the storefront. Sanitized on save (dangerous constructs are always removed); served from a generated, cache-busted file.",
            ["Plugins.CW.ThemeCore.Fields.CustomCssUrlPolicy"] = "url() policy",
            ["Plugins.CW.ThemeCore.Fields.CustomCssUrlPolicy.Hint"] = "How url(...) in Custom CSS is treated: Allow (any), DataAndSameOriginOnly (data:/relative only), or Block (strip all). @import, expression(), javascript:/vbscript:, behavior: are always blocked.",
            ["Plugins.CW.ThemeCore.Configure.Layouts"] = "Layouts",
            ["Plugins.CW.ThemeCore.Configure.Layouts.Hint"] = "Choose the layout for each storefront section. Options come from the active theme; selections are per store.",
            ["Plugins.CW.ThemeCore.Configure.BrandDesigner"] = "Brand designer (preview)",
            ["Plugins.CW.ThemeCore.Configure.BrandDesigner.Hint"] = "Set up the look of your storefront here - pick a vibe, fine-tune the accent colours and button shape, choose a layout for each page, and set your logos. The preview shows your live storefront and updates instantly as you make changes; switch on \"Edit elements\" to click a part of the preview and restyle it, or describe a change and let AI suggest one. Click Save to apply everything to the store.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant"] = "AI assistant",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Hint"] = "Connect the Brand designer to an AI service. Without a connection it runs in offline mode, with rule-based suggestions.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Save"] = "Save AI settings",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Saved"] = "AI settings saved.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Test"] = "Test connection",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.TestOk"] = "The connection works.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.RemoveKey"] = "Remove the saved key",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.KeySaved"] = "Key saved (ends in {0}).",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.NoConnection"] = "No connection: offline mode.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.UsingGlobal"] = "This store uses the connection set at All stores.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.SetAtAllStores"] = "Set at All stores.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.NoProvider"] = "None (offline mode)",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Failed"] = "The request could not be completed. Reload the page and try again.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Working"] = "Asking the AI...",
            ["Plugins.CW.ThemeCore.Ai.Note.InvalidColours"] = "These colours in the answer were not valid and kept their current value: {0}.",
            ["Plugins.CW.ThemeCore.Ai.Error.KeyRejected"] = "API key rejected - check the AI assistant settings.",
            ["Plugins.CW.ThemeCore.Ai.Error.AccountProblem"] = "The API key's account can't make this request - check billing and permissions.",
            ["Plugins.CW.ThemeCore.Ai.Error.InvalidRequest"] = "The AI request was invalid.",
            ["Plugins.CW.ThemeCore.Ai.Error.ModelRejected"] = "The provider rejected the request - check the model name.",
            ["Plugins.CW.ThemeCore.Ai.Error.RateLimited"] = "AI is busy - try again in a minute.",
            ["Plugins.CW.ThemeCore.Ai.Error.FreeLimit"] = "The provider's limit is reached. On a free tier this can last until the limit resets.",
            ["Plugins.CW.ThemeCore.Ai.Error.Unavailable"] = "AI service unavailable - try again.",
            ["Plugins.CW.ThemeCore.Ai.Error.Unreachable"] = "Can't reach the AI server at this address - is it running?",
            ["Plugins.CW.ThemeCore.Ai.Error.Refused"] = "The AI declined this request - try rewording it.",
            ["Plugins.CW.ThemeCore.Ai.Error.CutOff"] = "The AI's answer was cut off - try a shorter request.",
            ["Plugins.CW.ThemeCore.Ai.Error.InvalidOutput"] = "The AI's answer could not be used - try again, or try another model.",
            ["Plugins.CW.ThemeCore.Ai.Error.DailyLimit"] = "Daily AI limit reached.",
            ["Plugins.CW.ThemeCore.Ai.Error.Burst"] = "Too many requests - wait a minute and try again.",
            ["Plugins.CW.ThemeCore.Ai.Error.EmptyPrompt"] = "Describe the look you want first.",
            ["Plugins.CW.ThemeCore.Ai.Error.InvalidUrl"] = "Enter a full https address. Plain http is allowed only for localhost.",
            ["Plugins.CW.ThemeCore.Ai.Error.UnknownProvider"] = "Pick a provider from the list.",
            ["Plugins.CW.ThemeCore.Ai.Error.ModelRequired"] = "Enter the model name.",
            ["Plugins.CW.ThemeCore.Ai.Error.NoConnection"] = "No AI connection is set for this store.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.OverrideForStore"] = "Use this store's own connection",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.FreeCloudNotice"] = "This is a free cloud service. The provider may use what you send to improve its models, and its limits can change without notice.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.PromptHint"] = "Describe the look - don't paste customer details.",
            ["Plugins.CW.ThemeCore.Configure.AiAssistant.Offline"] = "Offline mode",
            ["Plugins.CW.ThemeCore.Fields.AiProvider"] = "Provider",
            ["Plugins.CW.ThemeCore.Fields.AiProvider.Hint"] = "The AI service the Brand designer uses. Picking one fills in the address, model and timeout, which you can then change. With no provider the Brand designer runs in offline mode.",
            ["Plugins.CW.ThemeCore.Fields.AiBaseUrl"] = "Address",
            ["Plugins.CW.ThemeCore.Fields.AiBaseUrl.Hint"] = "The provider's API address. It must use https, except for a server on this machine (localhost).",
            ["Plugins.CW.ThemeCore.Fields.AiModel"] = "Model",
            ["Plugins.CW.ThemeCore.Fields.AiModel.Hint"] = "The model name, exactly as the provider writes it.",
            ["Plugins.CW.ThemeCore.Fields.AiApiKey"] = "API key",
            ["Plugins.CW.ThemeCore.Fields.AiApiKey.Hint"] = "Stored encrypted and never shown again. Leave empty to keep the saved key.",
            ["Plugins.CW.ThemeCore.Fields.AiTimeoutSeconds"] = "Timeout (seconds)",
            ["Plugins.CW.ThemeCore.Fields.AiTimeoutSeconds.Hint"] = "How long to wait for an answer before showing an error.",
            ["Plugins.CW.ThemeCore.Fields.AiEffort"] = "Effort",
            ["Plugins.CW.ThemeCore.Fields.AiEffort.Hint"] = "Claude only: how much work the model puts into an answer. Higher effort costs more and takes longer. Applies to all stores.",
            ["Plugins.CW.ThemeCore.Fields.AiDailyLimit"] = "Daily limit",
            ["Plugins.CW.ThemeCore.Fields.AiDailyLimit.Hint"] = "The most AI generations per day for each connection. 0 means no limit. Applies to all stores."
        });
    }

    public override async Task UninstallAsync()
    {
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(CwThemeCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(CwThemeCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        //remove generated Custom CSS files
        await _cssFileService.DeleteAllAsync();

        await _settingService.DeleteSettingAsync<CwThemeSettings>();
        await _settingService.DeleteSettingAsync<CwThemeAiSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.ThemeCore");

        await base.UninstallAsync();
    }

    #endregion
}
