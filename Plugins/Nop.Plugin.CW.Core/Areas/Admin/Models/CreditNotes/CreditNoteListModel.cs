using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

/// <summary>
/// Represents a credit note list model
/// </summary>
public partial record CreditNoteListModel : BasePagedListModel<CreditNoteModel>;

