/*
 * The subsidy card of Promotions > Reminders: each follow-up's lead time shows only while that follow-up is enabled.
 * The card is drawn once per page and never refilled, so the handler is bound once, from the card itself.
 */
(function () {
    'use strict';

    function toggleBlock(checkbox) {
        $('#' + checkbox.getAttribute('data-followup-block')).toggle(checkbox.checked);
    }

    $(function () {
        var $card = $('#cw-subsidy-expiry-reminders');

        if (!$card.length)
            return;

        $card.off('.cwSubsidyReminders').on('change.cwSubsidyReminders', '.cw-subsidy-followup-enable', function () {
            toggleBlock(this);
        });

        $card.find('.cw-subsidy-followup-enable').each(function () {
            toggleBlock(this);
        });
    });
})();