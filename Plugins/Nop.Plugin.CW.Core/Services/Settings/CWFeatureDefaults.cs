using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.Settings;

public static class CWFeatureDefaults
{
    public static CacheKey FeaturesForAnyStoreCacheKey => new("CW.feature.anystore.all");

    public static string FeaturePrefix => "CW.feature.";
}