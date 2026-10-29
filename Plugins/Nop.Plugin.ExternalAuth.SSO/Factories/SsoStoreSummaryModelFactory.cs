using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Default implementation of <see cref="ISsoStoreSummaryModelFactory"/>.
/// </summary>
public class SsoStoreSummaryModelFactory : ISsoStoreSummaryModelFactory
{
    #region Fields

    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ISettingService _settingService;
    protected readonly ISsoMonitoringService _monitoringService;
    protected readonly ISsoStoreSummaryService _summaryService;

    #endregion

    #region Ctor

    public SsoStoreSummaryModelFactory(
        IDateTimeHelper dateTimeHelper,
        ISettingService settingService,
        ISsoMonitoringService monitoringService,
        ISsoStoreSummaryService summaryService)
    {
        _dateTimeHelper = dateTimeHelper;
        _settingService = settingService;
        _monitoringService = monitoringService;
        _summaryService = summaryService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The health badge of a store: <c>Off</c> when SSO is not on for it (the monitoring service skips
    /// such stores), otherwise the worst of its findings.
    /// </summary>
    internal static (string status, string notes) GetHealth(bool ssoOn, IReadOnlyCollection<SsoMonitoringFinding> findings)
    {
        if (!ssoOn)
            return ("Off", string.Empty);

        if (findings.Count == 0)
            return ("Ok", string.Empty);

        return (findings.Any(f => f.IsFailure) ? "Failure" : "Warning",
            string.Join("\n", findings.OrderByDescending(f => f.IsFailure).Select(f => f.Text)));
    }

    private async ValueTask<SsoStoreSummaryModel> MapToRowAsync(SsoStoreSummary s,
        ILookup<int, SsoMonitoringFinding> findingsByStore, int warningDays)
    {
        var dateSetup = await FormatLocalDateAsync(s.DateSetupUtc);
        var lastLogin = await FormatLocalDateAsync(s.LastSuccessfulLoginUtc);

        string certText;
        var certNearExpiry = false;
        var certExpired = false;
        if (!s.IdpCertificateNotAfter.HasValue)
        {
            certText = "—";
        }
        else
        {
            certText = s.IdpCertificateNotAfter.Value.ToString("yyyy-MM-dd");
            var remaining = s.IdpCertificateNotAfter.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                certExpired = true;
            else if (remaining <= TimeSpan.FromDays(warningDays))
                certNearExpiry = true;
        }

        var (healthStatus, healthNotes) = GetHealth(s.IsEnabled && s.Protocol != SsoProtocol.Off,
            findingsByStore[s.StoreId].ToList());

        return new SsoStoreSummaryModel
        {
            Id = s.StoreId,
            StoreId = s.StoreId,
            StoreName = s.StoreName,
            Protocol = s.Protocol.ToString(),
            IsEnabled = s.IsEnabled,
            DisplayOrder = s.DisplayOrder,
            DateSetup = dateSetup,
            NumberOfUsers = s.NumberOfUsers,
            LastSuccessfulLogin = lastLogin,
            HealthStatus = healthStatus,
            HealthNotes = healthNotes,
            CertificateNotAfter = certText,
            CertificateNearExpiry = certNearExpiry,
            CertificateExpired = certExpired
        };
    }

    private async ValueTask<string> FormatLocalDateAsync(DateTime? utc)
    {
        if (!utc.HasValue) return "—";
        var local = await _dateTimeHelper.ConvertToUserTimeAsync(utc.Value, DateTimeKind.Utc);
        return local.ToString("yyyy-MM-dd HH:mm");
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<SsoStoreSummarySearchModel> PrepareSearchModelAsync(SsoStoreSummarySearchModel searchModel)
    {
        searchModel.SetGridPageSize();
        return Task.FromResult(searchModel);
    }

    /// <inheritdoc />
    public async Task<SsoStoreSummaryListModel> PrepareListModelAsync(SsoStoreSummarySearchModel searchModel)
    {
        var summaries = await _summaryService.ListAsync();

        var ordered = summaries
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.StoreName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var paged = new PagedList<SsoStoreSummary>(ordered, searchModel.Page - 1, searchModel.PageSize);

        // One pass over every store, the same findings System → Warnings shows; the certificate cell
        // uses the same warning window so the two never disagree.
        var findingsByStore = (await _monitoringService.GetFindingsAsync()).ToLookup(f => f.StoreId);
        var warningDays = (await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0)).CertificateWarningDays;

        return await new SsoStoreSummaryListModel().PrepareToGridAsync(searchModel, paged, () =>
            paged.SelectAwait(s => MapToRowAsync(s, findingsByStore, warningDays)));
    }

    /// <inheritdoc />
    public async Task<AddSsoAccountModel> PrepareAddModelAsync()
    {
        var unconfigured = await _summaryService.ListUnconfiguredAsync();

        var model = new AddSsoAccountModel
        {
            AvailableStores = unconfigured
                .Select(s => new SelectListItem { Value = s.StoreId.ToString(), Text = s.StoreName })
                .ToList()
        };

        return model;
    }

    #endregion
}
