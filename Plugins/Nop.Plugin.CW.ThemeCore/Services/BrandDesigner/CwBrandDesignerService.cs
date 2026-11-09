using System.Diagnostics;
using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;
using Nop.Services.Localization;
using Nop.Services.Logging;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// The Brand Designer's service. A preset name returns that preset; with no connection the offline generator answers,
/// labelled as such; otherwise the AI answers, and if the AI fails the admin sees an error and the form stays as it
/// was. Offline output is never passed off as AI output.
/// </summary>
public class CwBrandDesignerService : ICwBrandDesignerService
{
    #region Constants

    private const int AUDIT_PROMPT_LENGTH = 200;

    #endregion

    #region Fields

    private readonly ICustomerActivityService _customerActivityService;
    private readonly ICwBrandAiClientResolver _clientResolver;
    private readonly ICwBrandAiLimiter _limiter;
    private readonly ICwBrandAiSettingsService _aiSettingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly MockBrandGenerator _mock = new();

    #endregion

    #region Ctor

    public CwBrandDesignerService(
        ICustomerActivityService customerActivityService,
        ICwBrandAiClientResolver clientResolver,
        ICwBrandAiLimiter limiter,
        ICwBrandAiSettingsService aiSettingsService,
        ILocalizationService localizationService,
        ILogger logger,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _customerActivityService = customerActivityService;
        _clientResolver = clientResolver;
        _limiter = limiter;
        _aiSettingsService = aiSettingsService;
        _localizationService = localizationService;
        _logger = logger;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    private async Task<BrandGenerateResult> ErrorAsync(string resourceKey, BrandAiConnection connection) => new()
    {
        Source = BrandResultSource.Ai,
        Provider = connection.Provider.Name,
        Error = await _localizationService.GetResourceAsync(resourceKey)
    };

    private Task AuditAsync(BrandAiConnection connection, BrandAiRequest request, int storeScope, BrandAiAnswer answer, long milliseconds, string outcome)
    {
        var prompt = request.Prompt ?? string.Empty;
        var keySource = connection.OwnerStoreId == 0 ? "global" : "own";

        return _customerActivityService.InsertActivityAsync(CwThemeCoreDefaults.AiGenerateActivityType,
            $"scope {storeScope}, key {keySource}, {connection.Provider.Name}, model {connection.Model}, region {request.Region ?? "none"}, " +
            $"tokens {answer?.InputTokens}/{answer?.OutputTokens}/{answer?.CachedTokens}, {milliseconds} ms, {outcome}: " +
            prompt[..Math.Min(prompt.Length, AUDIT_PROMPT_LENGTH)]);
    }

    private async Task<BrandGenerateResult> GenerateWithAiAsync(BrandAiConnection connection, BrandAiRequest request, int storeScope)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var limit = await _limiter.TryCountAsync(connection, customer.Id);
        if (limit is not null)
            return await ErrorAsync(limit, connection);

        var clock = Stopwatch.StartNew();
        try
        {
            var answer = await _clientResolver.Get(connection.Provider.ClientKind).GenerateAsync(connection, request);
            var style = BrandAiStyleValidator.Apply(answer.Style, request.Current, request.Region, out var invalid);
            if (style is null)
                throw new BrandAiException(BrandAiMessages.InvalidOutput, "no usable colour in the answer");

            await AuditAsync(connection, request, storeScope, answer, clock.ElapsedMilliseconds, "ok");

            var result = new BrandGenerateResult
            {
                Style = style,
                Summary = answer.Summary,
                Source = BrandResultSource.Ai,
                Provider = connection.Provider.Name
            };
            if (invalid.Count > 0)
            {
                result.Notes.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Ai.Note.InvalidColours"),
                    string.Join(", ", invalid)));
            }

            return result;
        }
        catch (BrandAiException e)
        {
            await _logger.WarningAsync($"Brand Designer AI ({connection.Provider.Name}, {connection.Model}): {e.Message}. {e.Detail}");
            await AuditAsync(connection, request, storeScope, null, clock.ElapsedMilliseconds, "failed");

            return await ErrorAsync(e.Message, connection);
        }
    }

    #endregion

    #region Methods

    public IList<BrandPreset> GetPresets() => BrandPresets.All;

    public async Task<BrandGenerateResult> GenerateAsync(string prompt, BrandStyle current, string region = null)
    {
        //a vibe chip's whole point is a known result: free, instant, and no AI call
        var preset = BrandPresets.All.FirstOrDefault(p => string.Equals(p.Name, prompt?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
            return new BrandGenerateResult { Style = _mock.Generate(prompt, current, region), Source = BrandResultSource.Preset };

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var connection = await _aiSettingsService.GetConnectionAsync(storeScope);
        if (connection is null)
            return new BrandGenerateResult { Style = _mock.Generate(prompt, current, region), Source = BrandResultSource.Offline };

        if (string.IsNullOrWhiteSpace(prompt))
            return await ErrorAsync(BrandAiMessages.EmptyPrompt, connection);

        return await GenerateWithAiAsync(connection, new BrandAiRequest(prompt.Trim(), current, region), storeScope);
    }

    public BrandStyle Random() => _mock.Random();

    public async Task<string> TestConnectionAsync()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var connection = await _aiSettingsService.GetConnectionAsync(storeScope);
        if (connection is null)
            return await _localizationService.GetResourceAsync(BrandAiMessages.NoConnection);

        try
        {
            await _clientResolver.Get(connection.Provider.ClientKind).TestAsync(connection);
            return null;
        }
        catch (BrandAiException e)
        {
            await _logger.WarningAsync($"Brand Designer AI test ({connection.Provider.Name}, {connection.Model}): {e.Message}. {e.Detail}");
            return await _localizationService.GetResourceAsync(e.Message);
        }
    }

    #endregion
}
