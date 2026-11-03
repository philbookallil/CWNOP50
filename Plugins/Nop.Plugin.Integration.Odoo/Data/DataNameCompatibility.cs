using Nop.Data.Mapping;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data;

public class DataNameCompatibility : INameCompatibility
{

    /// <summary>
    /// Gets the table names mapping to maintain compatibility with existing database tables
    /// </summary>
    public Dictionary<Type, string> TableNames => new()
    {
        { typeof(OdooLog),  "Odoo_Log" },
        { typeof(OdooLogEvent), "Odoo_LogEvent" },
        { typeof(OdooStoreMapping), "Odoo_StoreMapping" },
    };

    /// <summary>
    /// Gets the column names mapping (empty for this plugin as no column renames are needed)
    /// </summary>
    public Dictionary<(Type, string), string> ColumnName => new();
}