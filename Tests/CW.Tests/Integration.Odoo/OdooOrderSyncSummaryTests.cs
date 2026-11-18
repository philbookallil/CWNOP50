using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// What a row of the Orders page says about an order and which button it offers: Sync for a live order not yet
/// fully confirmed, Cancel in Odoo for a cancelled or deleted order still open there, nothing otherwise.
/// </summary>
[TestFixture]
public class OdooOrderSyncSummaryTests
{
    #region Utilities

    private static OdooOrderMapping Row(OdooOrderStatus status, int? odooOrderId = null) =>
        new() { Status = status, OdooOrderId = odooOrderId };

    private static OdooOrderSyncSummary For(bool cancelledOrDeleted, params OdooOrderMapping[] rows) =>
        OdooOrderSyncSummary.For(cancelledOrDeleted, rows);

    #endregion

    [Test]
    public void A_live_order_never_sent_offers_Sync()
    {
        For(false).Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.NotSent, CanSend: true, CanCancel: false));
    }

    [Test]
    public void A_live_order_with_every_Odoo_order_confirmed_is_Sent_and_offers_nothing()
    {
        For(false, Row(OdooOrderStatus.Confirmed, 1), Row(OdooOrderStatus.Confirmed, 2))
            .Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.Sent, false, false));
    }

    [Test]
    public void A_failed_destination_wins_over_held_and_offers_Sync()
    {
        For(false, Row(OdooOrderStatus.Confirmed, 1), Row(OdooOrderStatus.Held), Row(OdooOrderStatus.Failed, 3))
            .Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.Failed, true, false));
    }

    [Test]
    public void A_held_destination_offers_Sync()
    {
        For(false, Row(OdooOrderStatus.Held)).Status.Should().Be(OdooOrderSyncSummary.Held);
        For(false, Row(OdooOrderStatus.Held)).CanSend.Should().BeTrue();
    }

    [Test]
    public void A_destination_pending_or_created_is_in_progress_and_offers_Sync()
    {
        For(false, Row(OdooOrderStatus.Confirmed, 1), Row(OdooOrderStatus.Pending))
            .Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.InProgress, true, false));
        For(false, Row(OdooOrderStatus.Created, 4)).Status.Should().Be(OdooOrderSyncSummary.InProgress);
    }

    [Test]
    public void A_cancelled_order_still_open_in_Odoo_offers_Cancel_only()
    {
        For(true, Row(OdooOrderStatus.Confirmed, 1))
            .Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.ToCancel, CanSend: false, CanCancel: true));

        // A failed confirmation still left an order in Odoo
        For(true, Row(OdooOrderStatus.Failed, 2)).CanCancel.Should().BeTrue();
    }

    [Test]
    public void A_cancelled_order_with_nothing_open_in_Odoo_offers_nothing()
    {
        For(true, Row(OdooOrderStatus.Cancelled, 1))
            .Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.CancelledInOdoo, false, false));

        For(true).Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.NotSentCancelled, false, false));

        // Held or failed before Odoo created anything: nothing to cancel there
        For(true, Row(OdooOrderStatus.Held), Row(OdooOrderStatus.Failed))
            .Should().Be(new OdooOrderSyncSummary(OdooOrderSyncSummary.NotSentCancelled, false, false));
    }
}
