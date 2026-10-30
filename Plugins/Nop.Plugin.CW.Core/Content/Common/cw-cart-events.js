/*
 * One signal for "the cart changed", raised on document as cwCartChangedEvent.
 *
 * Several parts of a page show figures that depend on the cart: the subsidy bar and the product-group
 * headers on the subsidy catalogue, the subsidy panel in the packname info strip and the cart badges in the
 * packname selector. Rather than each part watching one kind of request, every change to the cart raises
 * this event and each part listens to it, so an add, a removal from the flyout and a change at the checkout
 * all refresh the same figures.
 *
 * Raising it:
 *  - nop's AjaxCart adds (product boxes and the product page) are hooked here, once;
 *  - every other change calls CWCart.notifyChanged() after the server has answered with success.
 */
(function ($) {
    'use strict';

    window.CWCart = window.CWCart || {};

    CWCart.changedEvent = 'cwCartChangedEvent';

    CWCart.notifyChanged = function () {
        $(document).trigger(CWCart.changedEvent);
    };

    $(function () {
        //AjaxCart exists only on storefront pages, and it must be hooked only once.
        if (!window.AjaxCart || AjaxCart.cwCartChangedHooked)
            return;

        AjaxCart.cwCartChangedHooked = true;

        var originalSuccessProcess = AjaxCart.success_process;

        AjaxCart.success_process = function (response) {
            originalSuccessProcess.call(AjaxCart, response);

            //A refused add answers success = false, and a redirect leaves the page: neither changed this page's cart.
            if (response && response.success && !response.redirect && response.updatetopcartsectionhtml)
                CWCart.notifyChanged();
        };
    });
})(jQuery);