/*
 * The approval card of the admin order page: approve, decline, resend and change approvers.
 *
 * The card sits inside nop's order form, so its buttons post by AJAX instead of with forms of their own. The
 * actions answer an anonymous object, so its members arrive lowercase: { success, message }. On success the page
 * reloads, which shows the order's new status and notes.
 *
 * The handlers are delegated from the card, which is rendered with the page and never replaced, and bound once
 * (the .off first keeps it so if the script is ever loaded twice).
 */
(function () {
    'use strict';

    $(function () {
        var $card = $('.cw-order-approval').off('.cwapproval');
        var $cfg = $card.find('.cw-order-approval-config');

        if ($cfg.length === 0)
            return;

        var orderId = $cfg.data('orderId');

        function post(url, data, confirmText) {
            if (!confirm(confirmText))
                return;

            data.orderId = orderId;

            $.ajax({
                cache: false,
                type: 'POST',
                url: url,
                data: addAntiForgeryToken(data),
                success: function (answer) {
                    if (answer && answer.success) {
                        location.reload();
                        return;
                    }

                    display_nop_error({ error: (answer && answer.message) || $cfg.data('errorText') });
                },
                error: function () {
                    display_nop_error({ error: $cfg.data('errorText') });
                }
            });
        }

        $card.on('click.cwapproval', '.cw-approval-approve', function () {
            post($cfg.data('approveUrl'), {}, $cfg.data('confirmApprove'));
        });

        $card.on('click.cwapproval', '.cw-approval-decline', function () {
            post($cfg.data('declineUrl'), { reason: $('#cw-approval-reason').val() }, $cfg.data('confirmDecline'));
        });

        $card.on('click.cwapproval', '.cw-approval-resend', function () {
            post($cfg.data('resendUrl'), {}, $cfg.data('confirmResend'));
        });

        $card.on('click.cwapproval', '.cw-approval-change', function () {
            post($cfg.data('changeUrl'), {
                emails: $('#cw-approval-emails').val(),
                startedFrom: $('#cw-approval-emails').prop('defaultValue')
            }, $cfg.data('confirmChange'));
        });
    });
})();
