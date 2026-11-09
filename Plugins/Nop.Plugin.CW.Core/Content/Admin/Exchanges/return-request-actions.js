/*
 * The admin return page: the Requested action dropdown follows the Reason for return, as the storefront wizard does.
 * Server values come from the .cw-return-actions-config div (CLAUDE.md 9.14). The dropdown and the config div are
 * only on the page while the return can still be edited.
 */
(function () {
    var $cfg = $('.cw-return-actions-config');
    if (!$cfg.length)
        return;

    var $reason = $('#ReturnRequestReasonId');
    var $action = $('#ReturnRequestActionId');

    //the dropdowns are part of the page, rendered once, so a namespaced rebind is enough
    $reason.off('.cwreturnactions').on('change.cwreturnactions', function () {
        var chosen = $action.val();

        $.ajax({
            cache: false,
            type: 'GET',
            url: $cfg.data('url'),
            data: { returnRequestId: $cfg.data('returnRequestId'), reasonId: $reason.val() },
            success: function (items) {
                $action.empty();
                $.each(items, function (i, item) {
                    $('<option></option>').val(item.Value).text(item.Text).appendTo($action);
                });

                //keep the chosen action when the new reason offers it; otherwise "select an action", so staff choose again
                $action.val($action.find('option[value="' + chosen + '"]').length ? chosen : '0');
                $action.trigger('change');
            },
            error: function () {
                display_nop_error({ error: $cfg.data('errorText') });
            }
        });
    });
})();