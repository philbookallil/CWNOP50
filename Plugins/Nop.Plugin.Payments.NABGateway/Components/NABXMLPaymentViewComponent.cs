using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.NABGateway.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.NABGateway.Components;

/// <summary>
/// Renders the public credit card form on the checkout payment-info step.
/// </summary>
[ViewComponent(Name = NABXMLPaymentDefaults.ViewComponentName)]
public class NABXMLPaymentViewComponent : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new PaymentInfoModel();

        //years — current year + 24
        for (var i = 0; i < 25; i++)
        {
            var year = DateTime.Now.AddYears(i).ToString("yyyy");
            model.ExpireYears.Add(new SelectListItem { Text = year, Value = year });
        }

        //months — zero-padded display, numeric value
        for (var i = 1; i <= 12; i++)
        {
            model.ExpireMonths.Add(new SelectListItem { Text = i.ToString("D2"), Value = i.ToString() });
        }

        //postback values (the form is re-rendered on validation failure)
        if (!Request.IsGetRequest())
        {
            var form = await Request.ReadFormAsync();

            model.CardholderName = form["CardholderName"];
            model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];

            var selectedMonth = model.ExpireMonths.FirstOrDefault(x =>
                x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedMonth != null)
                selectedMonth.Selected = true;

            var selectedYear = model.ExpireYears.FirstOrDefault(x =>
                x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedYear != null)
                selectedYear.Selected = true;
        }

        return View("~/Plugins/Payments.NABGateway/Views/PaymentInfo.cshtml", model);
    }
}
