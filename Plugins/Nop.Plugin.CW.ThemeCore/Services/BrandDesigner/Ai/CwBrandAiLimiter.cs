using Nop.Core.Caching;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// The guards on AI spending: a cap per day for each connection, and a burst limit per admin.
/// </summary>
public interface ICwBrandAiLimiter
{
    /// <summary>Count one generation.</summary>
    /// <returns>A locale resource key when a limit is reached; otherwise null</returns>
    Task<string> TryCountAsync(BrandAiConnection connection, int customerId);
}

/// <inheritdoc />
/// <remarks>
/// Counters live in the cache, so they reset on an application restart, and with a distributed cache the count is
/// approximate. Both are acceptable for a cost guard.
/// </remarks>
public class CwBrandAiLimiter : ICwBrandAiLimiter
{
    #region Constants

    private const int MAX_PER_MINUTE = 10;

    private static CacheKey DailyKey => new("CW.ThemeCore.ai.daily.{0}.{1}");
    private static CacheKey BurstKey => new("CW.ThemeCore.ai.burst.{0}.{1}");

    #endregion

    #region Fields

    private readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public CwBrandAiLimiter(IStaticCacheManager staticCacheManager)
    {
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Utilities

    private async Task<bool> TryIncrementAsync(CacheKey key, int limit)
    {
        var count = await _staticCacheManager.GetAsync(key, 0);
        if (limit > 0 && count >= limit)
            return false;

        await _staticCacheManager.SetAsync(key, count + 1);
        return true;
    }

    #endregion

    #region Methods

    public async Task<string> TryCountAsync(BrandAiConnection connection, int customerId)
    {
        var now = DateTime.UtcNow;

        var burstKey = _staticCacheManager.PrepareKeyForDefaultCache(BurstKey, customerId, now.ToString("yyyyMMddHHmm"));
        burstKey.CacheTime = 2;
        if (!await TryIncrementAsync(burstKey, MAX_PER_MINUTE))
            return BrandAiMessages.Burst;

        var dailyKey = _staticCacheManager.PrepareKeyForDefaultCache(DailyKey, now.ToString("yyyyMMdd"), connection.OwnerStoreId);
        dailyKey.CacheTime = 25 * 60;
        return await TryIncrementAsync(dailyKey, connection.DailyLimit) ? null : BrandAiMessages.DailyLimit;
    }

    #endregion
}
