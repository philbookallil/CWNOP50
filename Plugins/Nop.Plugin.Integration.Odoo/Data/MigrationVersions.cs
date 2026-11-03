namespace Nop.Plugin.Integration.Odoo.Data;

/// <summary>
/// Centralised timestamp constants for FluentMigrator migrations in this plugin.
/// </summary>
/// <remarks>
/// Format <c>yyyy/MM/dd HH:mm:ss</c>. New migrations append a new <c>const string</c> here and
/// reference it from their <c>[NopMigration]</c> attribute.
/// </remarks>
public static class MigrationVersions
{
    /// <summary>
    /// Initial schema: <c>Odoo_Log</c>, <c>Odoo_LogEvent</c> and <c>Odoo_StoreMapping</c> with their indexes.
    /// </summary>
    public const string InitialSchema = "2026/10/26 00:00:00";
}
