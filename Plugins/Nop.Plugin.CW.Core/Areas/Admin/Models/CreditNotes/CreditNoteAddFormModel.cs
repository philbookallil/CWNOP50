using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

/// <summary>
/// Model record for the Credit Note add Form
/// </summary>
public record CreditNoteAddFormModel : BaseNopModel
{
    public int CustomerId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CreditNotes.AddForm.Order")]
    [Required]
    public int OrderId { get; set; }

    public IList<SelectListItem> AvailableOrders { get; set; } = new List<SelectListItem>();

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CreditNotes.AddForm.Description")]
    [Required]
    public string Description { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CreditNotes.AddForm.Amount")]
    [Required]
    public decimal Amount { get; set; }

}
