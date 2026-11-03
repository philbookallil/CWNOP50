namespace Nop.Plugin.Integration.Odoo.Models;

/// <summary>
/// JSON answer of the Run Diagnostics button on the Configure page.
/// </summary>
public class DiagnosticsModel
{
    public string OdooUrl { get; set; }

    public string DatabaseName { get; set; }

    public bool ApiKeyExists { get; set; }

    public string ConnectionStatus { get; set; }

    public string TestResultMessage { get; set; }
}
