using Nop.Core.Infrastructure;
using Nop.Services.Plugins;

namespace CW.Common.Helpers;

public class PluginHelper
{
    public static async Task<bool> IsPluginInstalledAsync(string pluginSystemName)
    {
        var pluginService = EngineContext.Current.Resolve<IPluginService>();

        if (pluginService == null)
            return false;

        var pluginDescriptorResult = await pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>(pluginSystemName, LoadPluginsMode.All);

        return pluginDescriptorResult != null && pluginDescriptorResult.Installed;
    }

    // The synchronous IsPluginInstalled (blocking on .Result) was removed on 2026-10-22: no CW plugin called it, and
    // blocking a request thread on async I/O starves the thread pool under load. Use IsPluginInstalledAsync.
}