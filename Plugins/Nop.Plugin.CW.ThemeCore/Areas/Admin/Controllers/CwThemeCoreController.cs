using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Domain.Layout;
using Nop.Plugin.CW.ThemeCore.Services;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;
using Nop.Plugin.CW.ThemeCore.Services.Layout;
using Nop.Plugin.CW.ThemeCore.Services.Preview;
using Nop.Plugin.CW.ThemeCore.Services.Security;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Catalog;
using Nop.Services.Media;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Controllers;

[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public class CwThemeCoreController : BasePluginController
{
    #region Constants

    /// <summary>How long a live-preview token stays valid.</summary>
    private static readonly TimeSpan PreviewTokenLifetime = TimeSpan.FromMinutes(60);

    /// <summary>How long before expiry the page swaps its token for a fresh one (<see cref="RefreshPreviewToken"/>).</summary>
    private static readonly TimeSpan PreviewTokenRefreshMargin = TimeSpan.FromMinutes(5);

    #endregion

    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly ICwCssSanitizer _cssSanitizer;
    protected readonly ICwThemeCssFileService _cssFileService;
    protected readonly IThemeLayoutService _layoutService;
    protected readonly IThemeLayoutManifestService _manifestService;
    protected readonly ICwBrandDesignerService _brandDesigner;
    protected readonly ICwBrandAiSettingsService _aiSettingsService;
    protected readonly IThemeBrandPreviewService _brandPreview;
    protected readonly ICwPreviewTokenService _previewTokenService;
    protected readonly IStoreService _storeService;
    protected readonly ICategoryService _categoryService;
    protected readonly IProductService _productService;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly IPictureService _pictureService;

    #endregion

    #region Ctor

    public CwThemeCoreController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService,
        IStoreContext storeContext,
        ICwCssSanitizer cssSanitizer,
        ICwThemeCssFileService cssFileService,
        IThemeLayoutService layoutService,
        IThemeLayoutManifestService manifestService,
        ICwBrandDesignerService brandDesigner,
        ICwBrandAiSettingsService aiSettingsService,
        IThemeBrandPreviewService brandPreview,
        ICwPreviewTokenService previewTokenService,
        IStoreService storeService,
        ICategoryService categoryService,
        IProductService productService,
        IUrlRecordService urlRecordService,
        IPictureService pictureService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
        _storeContext = storeContext;
        _cssSanitizer = cssSanitizer;
        _cssFileService = cssFileService;
        _layoutService = layoutService;
        _manifestService = manifestService;
        _brandDesigner = brandDesigner;
        _aiSettingsService = aiSettingsService;
        _brandPreview = brandPreview;
        _previewTokenService = previewTokenService;
        _storeService = storeService;
        _categoryService = categoryService;
        _productService = productService;
        _urlRecordService = urlRecordService;
        _pictureService = pictureService;
    }

    #endregion

    #region Methods

    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public async Task<IActionResult> Configure()
    {
        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<CwThemeSettings>(storeScope);

        var model = new CwThemeSettingsModel
        {
            PrimaryColor = settings.PrimaryColor,
            PrimaryTintColor = settings.PrimaryTintColor,
            SuccessColor = settings.SuccessColor,
            DangerColor = settings.DangerColor,
            WarningColor = settings.WarningColor,
            DarkColor = settings.DarkColor,
            ButtonRadius = settings.ButtonRadius,
            LogoPictureId = settings.LogoPictureId,
            SecondLogoPictureId = settings.SecondLogoPictureId,
            SecondLogoLinkUrl = settings.SecondLogoLinkUrl,
            ReverseLogos = settings.ReverseLogos,
            DisableAccentColors = settings.DisableAccentColors,
            CustomCss = settings.CustomCss,
            CustomCssUrlPolicy = settings.CustomCssUrlPolicy,
            ActiveStoreScopeConfiguration = storeScope
        };

        if (storeScope > 0)
        {
            model.PrimaryColor_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.PrimaryColor, storeScope);
            model.PrimaryTintColor_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.PrimaryTintColor, storeScope);
            model.SuccessColor_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SuccessColor, storeScope);
            model.DangerColor_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.DangerColor, storeScope);
            model.WarningColor_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.WarningColor, storeScope);
            model.DarkColor_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.DarkColor, storeScope);
            model.ButtonRadius_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ButtonRadius, storeScope);
            model.LogoPictureId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.LogoPictureId, storeScope);
            model.SecondLogoPictureId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SecondLogoPictureId, storeScope);
            model.SecondLogoLinkUrl_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SecondLogoLinkUrl, storeScope);
            model.ReverseLogos_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ReverseLogos, storeScope);
            model.DisableAccentColors_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.DisableAccentColors, storeScope);
            model.CustomCss_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.CustomCss, storeScope);
            model.CustomCssUrlPolicy_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.CustomCssUrlPolicy, storeScope);
        }

        //the scope's storefront theme, its manifest and the store information settings — resolved once here and
        //handed to the helpers below, which all need some of them
        var storeInformationSettings = await _settingService.LoadSettingAsync<StoreInformationSettings>(storeScope);
        var previewTheme = ThemeOf(storeInformationSettings);
        var manifest = previewTheme is null ? null : await _manifestService.GetManifestAsync(previewTheme);

        await PrepareLayoutSectionsAsync(model, previewTheme, manifest, storeScope);

        model.AiCard = await PrepareAiCardAsync(storeScope);

        model.BrandPresets = _brandDesigner.GetPresets()
            .Select(p => new BrandPresetChipModel { Name = p.Name, PrimaryColor = p.Palette?.PrimaryColor })
            .ToList();

        //resolve the active theme's preview (accurate mock + picker) — or none for a non-CW theme (generic fallback)
        var preview = await _brandPreview.GetPreviewAsync(previewTheme);
        model.PreviewThemeName = previewTheme;
        model.HasThemePreview = preview.HasPreview;
        model.PreviewCss = preview.Css;
        model.PreviewViews = preview.Views
            .Select(v => new PreviewViewModel { Key = v.Key, Title = v.Title, Html = v.Html })
            .ToList();

        //live preview: real-storefront iframe (Home/Category/Product/Cart/Checkout) of the configured store
        if (model.HasThemePreview)
        {
            await PrepareLivePreviewAsync(model, manifest, storeScope);
            await PrepareLogoPreviewAsync(model, settings, storeInformationSettings);
        }

        return View("~/Plugins/CW.ThemeCore/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public async Task<IActionResult> Configure(CwThemeSettingsModel model)
    {
        if (!ModelState.IsValid)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Configure.FormErrors"));
            return await Configure();
        }

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<CwThemeSettings>(storeScope);

        settings.PrimaryColor = NormalizeHex(model.PrimaryColor);
        settings.PrimaryTintColor = NormalizeHex(model.PrimaryTintColor);
        settings.SuccessColor = NormalizeHex(model.SuccessColor);
        settings.DangerColor = NormalizeHex(model.DangerColor);
        settings.WarningColor = NormalizeHex(model.WarningColor);
        settings.DarkColor = NormalizeHex(model.DarkColor);
        settings.ButtonRadius = Math.Clamp(model.ButtonRadius, 0, 999);
        settings.LogoPictureId = model.LogoPictureId;
        settings.SecondLogoPictureId = model.SecondLogoPictureId;
        settings.SecondLogoLinkUrl = model.SecondLogoLinkUrl;
        settings.ReverseLogos = model.ReverseLogos;
        settings.DisableAccentColors = model.DisableAccentColors;
        settings.CustomCss = model.CustomCss;
        settings.CustomCssUrlPolicy = model.CustomCssUrlPolicy;

        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.PrimaryColor, model.PrimaryColor_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.PrimaryTintColor, model.PrimaryTintColor_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SuccessColor, model.SuccessColor_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.DangerColor, model.DangerColor_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.WarningColor, model.WarningColor_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.DarkColor, model.DarkColor_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.ButtonRadius, model.ButtonRadius_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.LogoPictureId, model.LogoPictureId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SecondLogoPictureId, model.SecondLogoPictureId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SecondLogoLinkUrl, model.SecondLogoLinkUrl_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.ReverseLogos, model.ReverseLogos_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.DisableAccentColors, model.DisableAccentColors_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.CustomCss, model.CustomCss_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.CustomCssUrlPolicy, model.CustomCssUrlPolicy_OverrideForStore, storeScope, false);

        //persist per-store layout selections (dynamic settings-by-key)
        await SaveLayoutSectionsAsync(model, storeScope);

        //clear settings cache so the injected :root{} block and layout classes reflect the change
        await _settingService.ClearCacheAsync();

        //regenerate the sanitized Custom CSS files for every store (a default change affects non-overridden stores)
        await _cssFileService.GenerateForAllStoresAsync();

        //surface what the sanitizer removed from the CSS the admin is editing
        var sanitizeResult = _cssSanitizer.Sanitize(model.CustomCss, model.CustomCssUrlPolicy);
        foreach (var warning in sanitizeResult.Warnings)
            _notificationService.WarningNotification(warning);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #region Guided Brand Designer (MVP)

    /// <summary>
    /// Generate a brand style from a plain-English prompt (or a preset name), using the style currently
    /// in the form as the base for tweak-only prompts. Returns a BrandGenerateResult.
    /// </summary>
    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public async Task<IActionResult> GenerateBrand(BrandGenerateRequest request)
    {
        return Json(await _brandDesigner.GenerateAsync(request?.Prompt, request?.Current, request?.Region));
    }

    /// <summary>A fresh random style ("Surprise me").</summary>
    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public IActionResult RandomBrand()
    {
        return Json(_brandDesigner.Random());
    }

    /// <summary>
    /// Save the AI assistant card. The key carries billing, so this needs more than the rest of the page: an admin who
    /// can use the designer cannot change where its key goes.
    /// </summary>
    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    //the parameter must not be called "model": the form has a field named Model, and MVC would read that as the
    //parameter's prefix and bind nothing
    public async Task<IActionResult> SaveAiSettings(BrandAiSettingsModel aiSettings)
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var refused = await _aiSettingsService.SaveAsync(new BrandAiSettingsInput
        {
            Provider = aiSettings.Provider,
            BaseUrl = aiSettings.BaseUrl,
            Model = aiSettings.Model,
            ApiKey = aiSettings.ApiKey,
            RemoveKey = aiSettings.RemoveKey,
            TimeoutSeconds = aiSettings.TimeoutSeconds,
            Effort = aiSettings.Effort,
            DailyLimit = aiSettings.DailyLimit,
            OverrideForStore = aiSettings.OverrideForStore
        }, storeScope);

        return refused is null
            ? Json(new { success = true })
            : Json(new { success = false, message = await _localizationService.GetResourceAsync(refused) });
    }

    /// <summary>One minimal request with the connection in effect for the admin's store scope.</summary>
    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public async Task<IActionResult> TestAiConnection()
    {
        var failure = await _brandDesigner.TestConnectionAsync();

        return Json(new { success = failure is null, message = failure });
    }

    #endregion

    #region Live preview

    /// <summary>
    /// A fresh live-preview token for the store the page is previewing. The page's own token is minted when it
    /// renders and lives <see cref="PreviewTokenLifetime"/>; an admin can keep the designer open longer than
    /// that, so the page asks for a new one before loading a view once its token is close to expiring.
    /// Returns a null token when <paramref name="storeId"/> is no longer the store this admin's scope previews
    /// (the scope was switched in another tab) — the page then needs a reload anyway.
    /// </summary>
    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public async Task<IActionResult> RefreshPreviewToken(int storeId)
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var store = await ResolvePreviewStoreAsync(storeScope);

        return Json(new { token = store?.Id == storeId ? MintPreviewToken(store.Id) : null });
    }

    #endregion

    #endregion

    #region Utilities

    private async Task<BrandAiCardModel> PrepareAiCardAsync(int storeScope)
    {
        var view = await _aiSettingsService.GetViewAsync(storeScope);

        return new BrandAiCardModel
        {
            Provider = view.Provider,
            BaseUrl = view.BaseUrl,
            Model = view.Model,
            KeyLastFour = view.KeyLastFour,
            TimeoutSeconds = view.TimeoutSeconds,
            Effort = view.Effort,
            DailyLimit = view.DailyLimit,
            OverrideForStore = view.OverrideForStore,
            IsStoreScope = storeScope > 0,
            IsFreeCloud = BrandAiProviders.Find(view.Provider)?.Kind == BrandAiProviderKind.FreeCloud,
            Providers = BrandAiProviders.All
        };
    }

    /// <summary>
    /// Strip a leading '#' and whitespace so colours are stored consistently as bare hex.
    /// </summary>
    private static string NormalizeHex(string value)
        => string.IsNullOrWhiteSpace(value) ? value : value.Trim().TrimStart('#');

    /// <summary>
    /// The theme whose layout manifest the settings page edits for a store scope: that store's
    /// configured storefront theme (StoreInformationSettings.DefaultStoreTheme). Null when unset.
    /// </summary>
    private async Task<string> ResolveThemeForScopeAsync(int storeScope)
        => ThemeOf(await _settingService.LoadSettingAsync<StoreInformationSettings>(storeScope));

    /// <summary>The configured storefront theme in already-loaded store information settings. Null when unset.</summary>
    private static string ThemeOf(StoreInformationSettings storeInformationSettings)
        => string.IsNullOrWhiteSpace(storeInformationSettings.DefaultStoreTheme) ? null : storeInformationSettings.DefaultStoreTheme;

    /// <summary>Build one layout-selector row per section the active theme's manifest declares.</summary>
    private async Task PrepareLayoutSectionsAsync(CwThemeSettingsModel model, string theme, ThemeLayoutManifest manifest, int storeScope)
    {
        if (theme is null || manifest is null)
            return;

        foreach (var section in manifest.Sections)
        {
            model.LayoutSections.Add(new LayoutSectionModel
            {
                SectionKey = section.Key,
                Title = section.Title,
                SelectedLayoutKey = await _layoutService.GetSelectedLayoutKeyAsync(theme, section.Key, storeScope),
                OverrideForStore = await _layoutService.IsOverriddenForStoreAsync(theme, section.Key, storeScope),
                Options = section.Layouts
                    .Select(l => new LayoutOptionModel { Key = l.Key, Title = l.Title, Wireframe = l.Wireframe })
                    .ToList()
            });
        }
    }

    /// <summary>
    /// Resolve the logo image URLs shown in the preview header, mirroring what the live store renders (decision
    /// 2026-09-14): the PRIMARY logo comes from nop core <see cref="StoreInformationSettings.LogoPictureId"/>
    /// (what the CW.Standard header actually renders via the core LogoViewComponent), the SECOND (co-brand) logo
    /// from this plugin's <see cref="CwThemeSettings.SecondLogoPictureId"/>. Left empty when not configured, in
    /// which case the mock keeps its placeholder text.
    /// </summary>
    private async Task PrepareLogoPreviewAsync(CwThemeSettingsModel model, CwThemeSettings settings, StoreInformationSettings storeInformationSettings)
    {
        if (storeInformationSettings.LogoPictureId > 0)
            model.PreviewPrimaryLogoUrl = await _pictureService.GetPictureUrlAsync(storeInformationSettings.LogoPictureId, showDefaultPicture: false);
        if (settings.SecondLogoPictureId > 0)
            model.PreviewSecondLogoUrl = await _pictureService.GetPictureUrlAsync(settings.SecondLogoPictureId, showDefaultPicture: false);
    }

    /// <summary>
    /// The store the live preview frames: the store being configured, or for the all-stores scope the store the
    /// admin is currently on. Null when it has no resolvable public URL (the preview then stays on the mock).
    /// </summary>
    private async Task<Store> ResolvePreviewStoreAsync(int storeScope)
    {
        var targetStoreId = storeScope > 0 ? storeScope : (await _storeContext.GetCurrentStoreAsync()).Id;
        var store = await _storeService.GetStoreByIdAsync(targetStoreId);

        return store is not null && Uri.TryCreate(store.Url, UriKind.Absolute, out _) ? store : null;
    }

    /// <summary>A signed preview token for <paramref name="storeId"/>, pinned to this admin page's origin.</summary>
    private string MintPreviewToken(int storeId)
        => _previewTokenService.Mint(storeId, $"{Request.Scheme}://{Request.Host.Value}", PreviewTokenLifetime);

    /// <summary>
    /// Build the live-preview URLs (Home / Category / Product / Cart / Checkout on the configured store's own
    /// domain), the signed token the page appends to them, and the layout-class map, so the admin can frame the
    /// real storefront and drive it over postMessage. The URLs carry no token: the page adds the current one when
    /// it loads a view, so a refreshed token (<see cref="RefreshPreviewToken"/>) applies to every view.
    /// Best-effort: anything that can't be resolved just leaves that view on the mock.
    /// </summary>
    private async Task PrepareLivePreviewAsync(CwThemeSettingsModel model, ThemeLayoutManifest manifest, int storeScope)
    {
        var store = await ResolvePreviewStoreAsync(storeScope);
        if (store is null)
            return; //no resolvable public URL → mock only

        var storeOrigin = new Uri(store.Url).GetLeftPart(UriPartial.Authority);   // scheme://host[:port]
        var urls = new Dictionary<string, string> { ["home"] = $"{storeOrigin}/" };

        //representative category (first for the store) → the "categories" view
        var categories = await _categoryService.GetAllCategoriesAsync(storeId: store.Id);
        var categorySeName = await FirstSeNameAsync(categories.FirstOrDefault());
        if (!string.IsNullOrEmpty(categorySeName))
            urls["categories"] = $"{storeOrigin}/{categorySeName}";

        //representative product (first individually-visible for the store) → the "product" view
        var products = await _productService.SearchProductsAsync(pageIndex: 0, pageSize: 1, storeId: store.Id, visibleIndividuallyOnly: true);
        var productSeName = await FirstSeNameAsync(products.FirstOrDefault());
        if (!string.IsNullOrEmpty(productSeName))
            urls["product"] = $"{storeOrigin}/{productSeName}";

        //Cart & Checkout (Phase 3): live via the per-store hidden "preview customer" — the framing middleware
        //impersonates it (seeded dummy cart) so these pages render populated through nop's normal pipeline.
        urls["cart"] = $"{storeOrigin}/cart";
        urls["checkout"] = $"{storeOrigin}/onepagecheckout";

        //layout class map from the manifest: sectionKey → (layoutKey → cssClass)
        var layoutClasses = new Dictionary<string, IDictionary<string, string>>();
        foreach (var section in manifest?.Sections ?? Enumerable.Empty<ThemeLayoutSection>())
            layoutClasses[section.Key] = section.Layouts.ToDictionary(l => l.Key, l => l.CssClass ?? string.Empty);

        model.LivePreviewEnabled = true;
        model.PreviewStoreId = store.Id;
        model.PreviewStoreOrigin = storeOrigin;
        model.PreviewUrls = urls;
        model.PreviewToken = MintPreviewToken(store.Id);
        model.PreviewTokenRefreshAfterSeconds = (int)(PreviewTokenLifetime - PreviewTokenRefreshMargin).TotalSeconds;
        model.PreviewLayoutClasses = layoutClasses;
    }

    /// <summary>The SEO slug of an entity, or null when the entity is null.</summary>
    private async Task<string> FirstSeNameAsync<T>(T entity) where T : BaseEntity, ISlugSupported
        => entity is null ? null : await _urlRecordService.GetSeNameAsync(entity);

    /// <summary>Persist the posted per-section layout selections for the current store scope.</summary>
    private async Task SaveLayoutSectionsAsync(CwThemeSettingsModel model, int storeScope)
    {
        if (model.LayoutSections is null || model.LayoutSections.Count == 0)
            return;

        var theme = await ResolveThemeForScopeAsync(storeScope);
        if (theme is null)
            return;

        foreach (var row in model.LayoutSections)
            await _layoutService.SetSelectedLayoutKeyAsync(theme, row.SectionKey, row.SelectedLayoutKey, row.OverrideForStore, storeScope);
    }

    #endregion
}
