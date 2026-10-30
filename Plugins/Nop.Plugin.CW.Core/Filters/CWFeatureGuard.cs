using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;

namespace Nop.Plugin.CW.Core.Filters;

public class CWFeatureGuard : ICWFeatureGuard
{
    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ICWFeatureBlockResultBuilder _blockResultBuilder;

    #endregion

    #region Ctor

    public CWFeatureGuard(ICWFeatureService cwFeatureService,
        ICWFeatureBlockResultBuilder blockResultBuilder)
    {
        _cwFeatureService = cwFeatureService;
        _blockResultBuilder = blockResultBuilder;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns a block result when the feature is disabled for the store, otherwise null
    /// </summary>
    public async Task<IActionResult> BlockIfDisabledAsync(CWFeature feature, int storeId, HttpRequest request)
    {
        return await _cwFeatureService.IsEnabledAsync(feature, storeId)
            ? null
            : await _blockResultBuilder.CreateAsync(request);
    }

    #endregion
}
