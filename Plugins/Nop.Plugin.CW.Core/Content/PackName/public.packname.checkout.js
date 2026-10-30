/**
 * PackNameCheckout loading module.
 * Call startRequest() before any AJAX call, endRequest() in every complete/error callback.
 * The overlay appears on the first call and disappears only when all in-flight requests finish.
 */
var PackNameCheckout = (function ($) {
    'use strict';

    var _active = 0;
    var _$overlay = null;
    var _$checkoutBtn = null;

    function _show() {
        if (_$overlay) _$overlay.addClass('active');
        if (_$checkoutBtn) _$checkoutBtn.prop('disabled', true);
    }

    function _hide() {
        if (_$overlay) _$overlay.removeClass('active');
        if (_$checkoutBtn) _$checkoutBtn.prop('disabled', false);
    }

    function init() {
        _$overlay = $('#packname-checkout-loading');
        _$checkoutBtn = $('#checkout');
    }

    function startRequest() {
        if (!_$overlay) init();
        _active++;
        if (_active === 1) _show();
    }

    function endRequest() {
        _active = Math.max(0, _active - 1);
        if (_active === 0) _hide();
    }

    function isBusy() {
        return _active > 0;
    }

    return {
        init: init,
        startRequest: startRequest,
        endRequest: endRequest,
        isBusy: isBusy
    };
}(jQuery));