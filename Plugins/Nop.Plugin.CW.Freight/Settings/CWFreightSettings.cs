using Nop.Core.Configuration;

namespace Nop.Plugin.CW.Freight.Settings;

public class CWFreightSettings : ISettings
{
    public bool LimitMethodsToCreated { get; set; }
}
