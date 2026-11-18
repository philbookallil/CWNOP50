using System.Text.Json;
using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// The texts and the <c>sale.order/create</c> values the order send builds. Pure: no nop, no Odoo.
/// </summary>
[TestFixture]
public class OdooOrderBuilderTests
{
    #region Utilities

    private static OdooSaleOrderDraft Draft()
    {
        var draft = new OdooSaleOrderDraft
        {
            DestinationKey = 7,
            Origin = "nop 1042",
            CustomerPartnerId = 31,
            ShippingPartnerId = 61,
            OrderDateUtc = new DateTime(2026, 11, 13, 9, 5, 0, DateTimeKind.Utc)
        };
        draft.Lines.Add(new OdooSaleOrderLineDraft { ProductId = 61, Quantity = 2, PriceUnit = 30m, Name = "POLO (S)" });
        draft.Lines.Add(new OdooSaleOrderLineDraft { ProductId = 1, Quantity = 1, PriceUnit = 9.5m, Name = "Standard", IsDelivery = true });

        return draft;
    }

    #endregion

    [Test]
    public void Origin_is_the_order_number_with_the_destination_only_when_there_are_several()
    {
        OdooOrderBuilder.Origin("1042", 7, severalDestinations: false).Should().Be("nop 1042");
        OdooOrderBuilder.Origin("1042", 7, severalDestinations: true).Should().Be("nop 1042 / 7");
    }

    [Test]
    public void HtmlToText_keeps_one_line_per_break_and_decodes_entities()
    {
        OdooOrderBuilder.HtmlToText("Size: S<br />Print name: J. Smith &amp; Co<br><br/>")
            .Should().Be("Size: S\nPrint name: J. Smith & Co");

        OdooOrderBuilder.HtmlToText(null).Should().BeEmpty();
    }

    [Test]
    public void PackNameInfo_leaves_out_empty_parts()
    {
        OdooOrderBuilder.PackNameInfo("J. Smith", "Depot East", "", "4100")
            .Should().Be("For: J. Smith · Branch: Depot East · CC: 4100");

        OdooOrderBuilder.PackNameInfo(null, " ", null, null).Should().BeEmpty();
    }

    [Test]
    public void LineName_puts_name_attributes_packname_and_PO_on_their_own_lines()
    {
        OdooOrderBuilder.LineName("POLO", "Size: S<br />Print name: JS", "For: J. Smith", "PO-7")
            .Should().Be("POLO\nSize: S\nPrint name: JS\nFor: J. Smith\nPO: PO-7");

        OdooOrderBuilder.LineName("POLO", "", null, null).Should().Be("POLO");
    }

    [Test]
    public void CommonPurchaseOrder_is_set_only_when_every_line_has_the_same_one()
    {
        OdooOrderBuilder.CommonPurchaseOrder(new[] { "PO-7", " PO-7 " }).Should().Be("PO-7");
        OdooOrderBuilder.CommonPurchaseOrder(new[] { "PO-7", "PO-8" }).Should().BeNull();
        OdooOrderBuilder.CommonPurchaseOrder(new[] { "PO-7", null! }).Should().BeNull();
        OdooOrderBuilder.CommonPurchaseOrder(Array.Empty<string>()).Should().BeNull();
    }

    [Test]
    public void UnitPrice_is_the_line_total_over_the_quantity_to_cents()
    {
        OdooOrderBuilder.UnitPrice(10m, 3).Should().Be(3.33m);
        OdooOrderBuilder.UnitPrice(0.05m, 2).Should().Be(0.03m);
        OdooOrderBuilder.UnitPrice(10m, 0).Should().Be(0m);
    }

    [Test]
    public void CreateValues_has_the_three_partners_origin_date_and_lines()
    {
        var values = OdooOrderBuilder.CreateValues(Draft());

        values["partner_id"].Should().Be(31);
        values["partner_invoice_id"].Should().Be(31);
        values["partner_shipping_id"].Should().Be(61);
        values["origin"].Should().Be("nop 1042");
        values["date_order"].Should().Be("2026-11-13 09:05:00");
        values.Should().NotContainKey("client_order_ref");
        values.Should().NotContainKey("note");
    }

    [Test]
    public void CreateValues_sends_the_PO_and_the_note_only_when_there_are_some()
    {
        var draft = Draft();
        draft.ClientOrderRef = "PO-7";
        draft.NoteLines.Add("nop order 1042.");
        draft.NoteLines.Add("Subsidy <paid> 10.00");

        var values = OdooOrderBuilder.CreateValues(draft);

        values["client_order_ref"].Should().Be("PO-7");
        values["note"].Should().Be("<p>nop order 1042.</p><p>Subsidy &lt;paid&gt; 10.00</p>");
    }

    [Test]
    public void CreateValues_serialises_lines_as_one2many_create_commands()
    {
        var json = JsonSerializer.Serialize(OdooOrderBuilder.CreateValues(Draft()));
        using var document = JsonDocument.Parse(json);
        var lines = document.RootElement.GetProperty("order_line");

        lines.GetArrayLength().Should().Be(2);

        var first = lines[0];
        first[0].GetInt32().Should().Be(0);
        first[1].GetInt32().Should().Be(0);
        first[2].GetProperty("product_id").GetInt32().Should().Be(61);
        first[2].GetProperty("product_uom_qty").GetDecimal().Should().Be(2m);
        first[2].GetProperty("price_unit").GetDecimal().Should().Be(30m);
        first[2].GetProperty("sequence").GetInt32().Should().Be(10);
        first[2].TryGetProperty("is_delivery", out _).Should().BeFalse();

        lines[1][2].GetProperty("is_delivery").GetBoolean().Should().BeTrue();
    }
}
