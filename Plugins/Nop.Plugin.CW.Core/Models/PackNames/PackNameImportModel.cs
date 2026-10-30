using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.PackNames;

/// <summary>
/// The Excel import page, a manager's on the storefront or an administrator's in the admin area: what the template
/// holds, so the page can explain it, and where the page's link and posts go
/// </summary>
public record PackNameImportModel : BaseNopModel
{
    /// <summary>
    /// The headers of the template's columns, in order
    /// </summary>
    public IList<string> Columns { get; set; } = new List<string>();

    /// <summary>
    /// The most rows one file may hold
    /// </summary>
    public int MaxRows { get; set; }

    /// <summary>
    /// How dates are written when typed as text
    /// </summary>
    public string DateFormat { get; set; }

    /// <summary>
    /// The store an administrator imports into, posted with the file; 0 on a manager's page, whose store comes from
    /// the manager
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// The name of the store an administrator imports into
    /// </summary>
    public string StoreName { get; set; }

    /// <summary>
    /// Where the template is downloaded from
    /// </summary>
    public string TemplateUrl { get; set; }

    /// <summary>
    /// Where the page's script posts the file to import
    /// </summary>
    public string ImportUrl { get; set; }

    /// <summary>
    /// Where the page posts the same file again for its error report
    /// </summary>
    public string ErrorReportUrl { get; set; }
}